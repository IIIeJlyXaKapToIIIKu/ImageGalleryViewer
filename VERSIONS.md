# ImageGalleryViewer Versions

## Production

- `ImageGalleryViewer.exe`
  - Current production build: `v0.3.2`.
  - Promoted from `ImageGalleryViewer_beta_v0.3.2.exe`.
  - Loads all selected-folder images before opening the viewer and fixes empty-space picker clicks.
  - Adds a square `?` help button to the folder picker header.
  - Uses RAM-only mode for folders with 700 images or fewer.
  - Uses hidden temporary lossless BMP cache plus virtualized RAM unloading for folders with more than 700 images.
  - Forces WPF image cleanup and full garbage collection when a gallery closes.
  - Should not be overwritten by future beta work.

## Beta

Beta executables are stored in the `ImageGalleryViewer` project folder.

- `ImageGalleryViewer_beta_v0.3.2.exe`
  - Fixes RAM-only mode cleanup after closing a gallery.
  - Detaches WPF `Image.Source`, clears viewer visual/content references and item lists, and forces full garbage collection on close.

- `ImageGalleryViewer_beta_v0.3.1.exe`
  - Uses the old RAM-only loading mode for folders with 700 images or fewer.
  - Enables the hidden temporary lossless BMP cache and virtualized RAM unloading only when a folder has more than 700 images.

- `ImageGalleryViewer_beta_v0.3.0.exe`
  - Adds a hidden temporary lossless BMP cache next to the selected folder.
  - Replaces full in-RAM image retention with a virtualized viewer that only keeps the visible range and nearby buffer decoded.
  - Keeps `R`, `N`, `B`, and `Space` navigation based on image/block metadata rather than loaded WPF controls.
  - Deletes the temporary cache when the gallery closes.

- `ImageGalleryViewer_beta_v0.2.1.exe`
  - Adds a square `?` help button to the folder picker header.
  - The help button opens a small instruction window explaining that images must be inside an `img` folder within the selected directory.

- `ImageGalleryViewer_beta_v0.2.0.exe`
  - Loads all selected-folder images in the background before opening the viewer.
  - Progress text now tracks loaded image count and percent.
  - Prevents reopening the selected folder when clicking empty space under the picker list.
  - Decodes images to the current screen width to avoid keeping full-resolution decoded bitmaps in RAM.

- `ImageGalleryViewer_beta_v0.1.0.exe`
  - Baseline beta build created from the initial WPF implementation.
  - Future changes should increment `AppVersion.Version`, `AppVersion.ExeName`, and this changelog.
