# Handoff

## Goal

Build and iterate on a native Windows image viewer for a folder tree of image galleries.

The project now lives in `E:\Meatings\ImageGalleryViewer`.

Production executable:

- `E:\Meatings\ImageGalleryViewer.exe`

Future beta builds:

- must be named `ImageGalleryViewer_beta_vX.Y.Z.exe`
- must be saved inside `E:\Meatings\ImageGalleryViewer`
- must not overwrite the production exe unless explicitly promoted

## Current Code State

Source code:

- `ImageGalleryViewer/Program.cs`

Current production version:

- `0.3.2`

Current production metadata in `AppVersion`:

- `Channel = "production"`
- `ExeName = "ImageGalleryViewer.exe"`
- `WindowTitle = "Image Gallery Viewer v0.3.2"`

The app is a WPF implementation using:

- `Application`
- `Window`
- `ScrollViewer`
- `Canvas`
- `Image`
- hidden vertical scrollbar
- `BitmapScalingMode.HighQuality`

## Core Behavior

- Native Windows UI only. No browser, HTML, or webview.
- Scans from the executable directory.
- Finds image galleries by searching for child folders named `img`.
- Excludes `Downloads`.
- Opens a folder picker first.
- Opens a gallery only after preparation finishes.
- Displays images vertically at full window width.
- Supports keyboard scrolling with `Up` / `Down`.
- Supports `Space` to jump to the next image.
- Supports `R` shuffle while preserving `name_N` and `name_N_N` sequences as intact blocks.
- Supports `N` / `B` block navigation.
- Supports `F11` fullscreen correctly on multi-monitor setups.
- Shows a `?` help button in the folder picker.

## Memory Behavior

Current production `v0.3.2` uses two loading modes:

- `<= 700` images: RAM-only loading.
- `> 700` images: hidden temporary lossless BMP cache plus virtualized loading.

In cache mode:

- a hidden `.ImageGalleryViewerCache` folder is created next to the selected gallery;
- only visible and nearby images are kept decoded in RAM;
- far images are removed from WPF/RAM and reloaded from the BMP cache when needed;
- the temporary cache is deleted when the gallery closes.

In RAM-only mode, closing the gallery detaches WPF `Image.Source`, clears viewer content/list references, and forces full garbage collection.

## Actively Edited Files

- `Program.cs`
- `README.md`
- `VERSIONS.md`
- `handoff.md`
- `compile_comand.txt`

## Build Notes

`compile_comand.txt` is stored in the project directory and should use project-relative paths:

- source: `Program.cs`
- output: `..\ImageGalleryViewer.exe`

When creating a beta, switch `AppVersion` back to beta and compile to:

```text
ImageGalleryViewer_beta_vX.Y.Z.exe
```

inside the project folder.

## Failed Attempts / Lessons

1. WinForms GDI+ canvas worked functionally but had visible scroll lag.
2. WinForms animated easing made scroll input feel worse.
3. WinForms scaled-image cache reduced some pauses but added complexity and rendering edge cases.
4. Full preload of all images made the progress bar meaningful but caused huge RAM usage.
5. Browser rendering was smooth, but the user requires a native Windows app.
6. WPF is the current direction and works best so far.
7. Navigation should be based on image/block metadata and calculated offsets, not on assuming every WPF image control exists.

## Next Checks

- Verify that `ImageGalleryViewer.exe` runs from the collection root.
- Verify that scanning still finds `img` folders from the executable directory.
- Verify that RAM drops after closing RAM-mode galleries.
- Verify cache mode for folders with more than 700 images.
