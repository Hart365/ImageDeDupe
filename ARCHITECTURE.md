# Design, Code, & Architecture Documentation

This document covers the architectural patterns, algorithm details, performance optimizations, and accessibility features implemented in the **ImageDeDupe** application.

---

## 1. Architectural Overview

ImageDeDupe is structured around clean **Separation of Concerns** using a decoupled service model:

```
[ View (MainWindow.xaml) ] <---> [ View-Codebehind (MainWindow.xaml.cs) ]
                                              │
                     ┌────────────────────────┴────────────────────────┐
                     ▼                                                 ▼
          [ Services (ImageScanner) ]                      [ Helpers (ImageHasher) ]
                     │                                                 │
                     └────────────────────────┬────────────────────────┘
                                              ▼
                                 [ Models (ImageFile / Group) ]
```

### Component Details
- **View (WPF)**: UI layout defined in `MainWindow.xaml` styled by `AccessibilityStyles.xaml`. Views bind to data collections and trigger scanner tasks asynchronously.
- **Models**:
  - `ImageFile`: Represents a physical image, holding file details, EXIF data, GPS coordinates, camera model, and cached hashes.
  - `DuplicateImage`: Wraps an `ImageFile` that has been flagged as a duplicate, storing its status and similarity percentage.
  - `DuplicateGroup`: Groups a single original "KEEP" `ImageFile` with an observable collection of its `DuplicateImage` matches.
- **Services (`ImageScanner`)**: Coordinates multi-threaded folder scanning, metadata loading, hash compilation, and pairwise comparison of candidates.
- **Helpers (`ImageHasher`)**: The library containing pure functions for bitmap operations, distance calculations, and string similarity metrics.

---

## 2. Visual Similarity Algorithm

The visual comparison pipeline uses a hybrid structural-color analysis to achieve high reliability across formats, sizes, and aspect ratios:

### Step 1: Pre-Scale
The WIC decoder loads the image at a low resolution (`256px` wide) directly from the disk stream. This is scaled to `128x128` pixels using a transform buffer.

### Step 2: Double-Axis Difference Hashing (128-Bit dHash)
- **Horizontal dHash (64-bit)**:
  - Downscales the pixel array to a `9x8` block.
  - Converts it to grayscale using standard luminance coefficients: `Y = 0.299R + 0.587G + 0.114B`.
  - Compares adjacent horizontal pixels (`x` and `x + 1`). If `left > right`, the corresponding bit is set to `1` in the 64-bit unsigned integer (`hashH`).
- **Vertical dHash (64-bit)**:
  - Downscales the pixel array to an `8x9` block.
  - Converts it to grayscale.
  - Compares adjacent vertical pixels (`y` and `y + 1`). If `top > bottom`, the corresponding bit is set to `1` in the 64-bit unsigned integer (`hashV`).
- **Hamming Distance Comparison**: Similarity is computed using bitwise XOR and `BitOperations.PopCount`.
  $$\text{StructureSimilarity} = \frac{128 - \text{HammingDistance}(hash1, hash2)}{128} \times 100\%$$

### Step 3: Color Signature Hashing (48-Byte Grid)
- Downscales the scaled image to a `4x4` block.
- Extracts the RGB components of each of the 16 pixels, creating a 48-byte array representing the spatial distribution of colors.
- **RMS Distance Comparison**: Computes the Root Mean Square (RMS) color distance:
  $$\text{ColorSimilarity} = 100\% - \left(\frac{\text{RMSDistance}}{128}\right) \times 100\%$$

### Step 4: Hybrid Scoring
Weights the structural similarity at **75%** and the color similarity at **25%** to generate the final percentage. This prevents false positives on images with similar colors but completely different structures (or vice versa).

---

## 3. Performance & Memory Optimizations

WPF applications loading hundreds of large high-resolution images are prone to high memory overheads and UI thread freezing. We implemented the following solutions:

### Low-Resolution Codec Decoding (Savings: ~99%)
- **Visual Hashing**: When loading images to generate signatures, the decoder uses `DecodePixelWidth = 256`. The WIC codec natively decodes only the lowest mipmap level, avoiding reading full-resolution raw bytes into RAM.
- **Gallery Thumbnails**: The gallery thumbnails decode at `DecodePixelWidth = 120`.
- **Preview Pane**: The side-pane preview decodes at `DecodePixelWidth = 1000`.
- **Impact**: Loading 100 images of 12MP (4000x3000) at full resolution requires **4.8 GB of RAM**. Decoding at low resolution requires less than **30 MB**, representing a 99.3% reduction in memory pressure and a 10x-20x increase in speed.

### Non-Locking File Streams
Both the thumbnail converter and the preview pane decode bitmaps using `BitmapCacheOption.OnLoad` and close the underlying `FileStream` immediately. This ensures that the user can sweep (move or delete) files instantly without encountering "file in use by another process" exceptions.

### Resizable Layout Column Management
The side-panel resizes via a `GridSplitter`. The column definition toggles its `MinWidth` dynamically in code-behind: when collapsed, `MinWidth = 0` and `Width = 0` to completely hide the column; when expanded, `MinWidth = 240` to enforce a minimum boundary while preserving the user's dragged width.

---

## 4. Accessibility Architecture

We built the application to comply with WCAG 2.2 AA and AAA requirements:

- **Contrast Ratios (AAA Compliant)**:
  - All text is set against deep backgrounds to maintain ratios exceeding **7.3:1** (AAA standard is 7:1).
  - Primary "Start Scan" buttons use dark slate text (`#0F172A`) on a soft cyan background (`#38BDF8`), resulting in a **9:1 contrast ratio** (previous builds had 2.11:1).
- **Focus Visual System**:
  - Implemented custom `AccessibleFocusVisual` using a double-layered, high-contrast dashed amber outline (`#F59E0B`).
  - Applied focus templates to all buttons, text boxes, and interactive elements.
- **Keyboard Operability**:
  - Cards in the gallery support tab focus (`Focusable="True"`).
  - Pressing **Space** or **Enter** on any card triggers `Thumbnail_KeyDown` to show the preview.
  - Checkboxes can be toggled using standard Spacebar clicks.
- **Screen Reader Support**:
  - Custom `AutomationProperties.Name` and `HelpText` bindings describe exact contexts, e.g., `"Duplicate image: photo.jpg, 90 percent similar. Size 2.4 MB, taken 2026-08-17. Action is selected."`
