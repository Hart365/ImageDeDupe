# ImageDeDupe

A modern, fast, and fully accessible WPF desktop application for finding and removing duplicate or near-duplicate images. It utilizes visual similarity hashing, metadata comparison, and a highly responsive slide-resizable preview pane.

![Application Checkmark](ImageDeDupeApp/Resources/check_icon.png)

---

## Key Features

- **Multi-Criteria Hashing & Comparison**:
  - **Visual Similarity**: Uses a 128-bit dual-axis difference hash (dHash) combined with a 48-byte average color signature to identify similar images regardless of size, format, or compression.
  - **Exact Hash**: MD5 binary contents matching.
  - **Date Taken**: Exif metadata оригинал timestamp analysis.
  - **GPS Location**: Coordinates proximity check.
  - **Filename / File Size**: Smart string distance and size ratios.
- **AND / OR Search Logic**: Allows combining multiple criteria using boolean logic to find exact duplicates or near-matches.
- **Symmetric & Aligned Gallery**: Lists duplicates side-by-side with original "KEEP" files, displaying status and similarity ratings cleanly.
- **Resizable Image Preview Pane**: 
  - Slide open a large, high-resolution preview of any clicked image.
  - Interactive divider (`GridSplitter`) to scale the image and details dynamically.
  - Quick action buttons to **Copy Path** or **Show in Folder** (pre-selects the file in File Explorer).
  - Fully non-locking decoder to keep files editable while being viewed.
- **WCAG 2.2 AA / AAA Accessibility**:
  - Exceeds WCAG AAA text contrast minimums (9:1 contrast ratio on primary buttons).
  - Dedicated custom focus visual rings (prominent amber outline) for keyboard-only users.
  - Tabbable cards and keyboard shortcuts (**Enter** / **Space**) to toggle previews.
  - Full screen-reader support via `AutomationProperties`.

---

## How to Build & Run

### Prerequisites
- **.NET 10.0 SDK** (or later)
- **Windows OS** (required for WPF desktop components)

### Command Line Instructions

1. **Clone the Repository**:
   ```bash
   git clone https://github.com/Hart365/ImageDeDupe.git
   cd ImageDeDupe
   ```

2. **Restore Dependencies**:
   ```bash
   dotnet restore
   ```

3. **Build the Project**:
   ```bash
   dotnet build -c Release
   ```

4. **Run the Application**:
   ```bash
   dotnet run --project ImageDeDupeApp -c Release
   ```

5. **Run Unit Tests**:
   ```bash
   dotnet test
   ```

---

## Project Structure

- **`ImageDeDupeApp/`**: The main WPF desktop application.
  - `Models/`: Data structures representing files and duplicates.
  - `Helpers/`: Core hashing algorithms, visual similarity, and distance formulas.
  - `Services/`: Scanning service managing multi-threaded folders and comparison runs.
  - `Styles/`: Global resources, accessibility colors, and customized layouts.
- **`ImageDeDupeApp.Tests/`**: MSTest unit testing suite verifying GPS, hashing, and scanner behaviors.
- **`publish/`**: Standalone publishing output containing single-file executable builds.

---

## License

(c) 2026 Mike Hartley / Hart of the Midlands. All rights reserved.
This project is licensed under the terms of the MIT license
