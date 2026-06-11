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

- `0.4.5`

Current production metadata in `AppVersion`:

- `Channel = "production"`
- `ExeName = "ImageGalleryViewer.exe"`
- `WindowTitle = "Image Gallery Viewer v0.4.5"`

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
- Supports `R` shuffle while preserving blocks as intact sequences.
- Supports `N` / `B` block navigation (also works while sitting on a single image).
- Supports `F11` fullscreen correctly on multi-monitor setups.
- Shows a `?` help button in the folder picker.

## Block Grouping (v0.3.3+)

- A block is keyed on the full file name minus the trailing `_<number>`
  (regex `^(.+)_([0-9]+)$` in `ImageOrganizer.BlockPattern`).
- So `image1_1_1` and `image1_2_1` are separate blocks (`image1_1`, `image1_2`),
  and `N` / `B` step between these sub-blocks rather than skipping the whole
  `image1` group. `image1` with no `_<number>` is a single-image block.
- Within a block, items sort by the trailing number (`CompareBlockItems`).

## Mini-Gallery + Stats (v0.4.x)

Reusable `TileWindow` (modes `TileMode.Select` / `TileMode.Stats`) shows a
`WrapPanel` of tiles, one per block: representative = first image of the block,
caption = its file name without extension, with a `× N` counter label.

- **Select mode** opens automatically after a viewer closes
  (`PickerWindow.ShowSelectGallery`). The block you were on when closing the
  viewer (`ViewerWindow.LastViewedRepFileName`) is moved to the front.
  Clicking a tile adds +1 to its counter. A top-right "Выбрать несколько"
  checkbox (unchecked by default) keeps the window open for multi-marking;
  unchecked, a click marks and closes immediately. Window size 1100×820.
- **Stats mode** opens from a rounded 📊 chip on the right of each picker row
  (`PickerWindow.BuildStatsChip` / `OpenStats`). It shows only blocks with
  count > 0, sorted by count descending, with the image name and number.
  Window size 1185×882.

Counters persist per source folder via `StatsStore` in a hidden
`.ImageGalleryViewerStats.tsv` (TSV: `count\tRepFileName`) next to the `img`
folder's parent.

- IMPORTANT: a hidden file cannot be truncated by `File.WriteAllLines`
  (FileMode.Create throws Access denied), so `StatsStore.Save` clears the
  Hidden attribute before writing and re-applies it afterwards. Removing this
  reintroduces the "only the first mark is saved" bug.
- Picker rows stretch full width (`ListBoxItem` `HorizontalContentAlignment =
  Stretch`) so the stats chip sits flush at the right edge.
- The picker list is NOT disabled during load (re-entry is guarded by the
  `isLoading` flag); disabling it whitened the list background and made the
  white folder names invisible.

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

`compile_comand.txt` is stored in the project directory. It now uses absolute
paths and a pre-step that closes any running `ImageGalleryViewer` process
(the exe is locked while running, blocking the rebuild):

- source: `E:\Meatings\ImageGalleryViewer\Program.cs`
- output: `E:\Meatings\ImageGalleryViewer.exe`
- compiler: `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe` (C# 5)

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
