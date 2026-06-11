# ImageGalleryViewer Versions

## Production

- `ImageGalleryViewer.exe`
  - Current production build: `v0.4.5`.
  - Stats window now opens at 1185×882; the post-viewer select window stays at 1100×820.
  - `v0.4.4`: fixes the picker turning white with invisible folder names while a folder loads: the list is no longer disabled (re-entry was already guarded by the loading flag), so the dark background and white text remain visible.
  - `v0.4.3`: adds a top-right "Выбрать несколько" checkbox (unchecked by default) to the post-viewer select window: when checked, clicking tiles marks them without closing, so several blocks can be marked in one session.
  - `v0.4.2`: stats view now sorts tiles by count descending (ties by natural name order).
  - `v0.4.1`: fixes counters not persisting after the first save: the hidden stats file could not be truncated, so the Hidden attribute is now cleared before writing and re-applied after.
  - Clicking a tile in the post-viewer mini-gallery now marks the block (+1) and closes the window immediately.
  - Main picker window height increased to 1000.
  - `v0.4.0`: adds a tiled mini-gallery shown after a viewer closes: one tile per block (first image + caption); clicking a tile adds +1 to that block's counter. The block you were on when closing the viewer is moved to the front.
  - Each picker row now has a rounded stats chip (📊) on the far right that opens a read-only stats view showing only marked blocks with the image name and count.
  - Counters are persisted per source folder in a hidden `.ImageGalleryViewerStats.tsv` file next to the `img` folder's parent.
  - Picker rows now stretch full width so the stats chip sits flush at the right edge.
  - `v0.3.3`: block grouping keys on the full name minus the trailing `_<number>` (`image1_1_1` / `image1_2_1` are separate blocks); `N`/`B` step between sub-blocks and also work from single images.
  - Previous production build: `v0.3.2`.
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
