using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace ImageGalleryViewer
{
    internal static class AppVersion
    {
        // Future experiments should switch back to beta and compile to ImageGalleryViewer_beta_v{Version}.exe.
        public const string Channel = "production";
        public const string Version = "0.3.2";
        public const string ExeName = "ImageGalleryViewer.exe";
        public const string WindowTitle = "Image Gallery Viewer v0.3.2";
    }

    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            string root = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
            List<ImageSource> sources = SourceScanner.FindSources(root);

            Application app = new Application();
            app.Run(new PickerWindow(root, sources));
        }
    }

    internal sealed class ImageSource
    {
        public string DisplayName;
        public string ImgPath;
    }

    internal sealed class ImageItem
    {
        public string Path;
        public string FileName;
        public ImageUnit Unit;
        public FrameworkElement Element;
        public BitmapSource Bitmap;
        public string LoadError;
        public string CachePath;
        public int PixelWidth;
        public int PixelHeight;
        public double DisplayTop;
        public double DisplayHeight;
        public bool IsBitmapLoading;
    }

    internal sealed class ImageUnit
    {
        public string Key;
        public bool IsBlock;
        public List<ImageItem> Items = new List<ImageItem>();
    }

    internal static class SourceScanner
    {
        private static readonly HashSet<string> ImageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff", ".webp"
        };

        public static bool IsSupportedImage(string path)
        {
            return ImageExtensions.Contains(Path.GetExtension(path));
        }

        public static List<ImageSource> FindSources(string root)
        {
            List<ImageSource> result = new List<ImageSource>();
            string downloads = Path.Combine(root, "Downloads");

            foreach (string top in SafeGetDirectories(root))
            {
                if (String.Equals(Path.GetFileName(top), "Downloads", StringComparison.OrdinalIgnoreCase))
                    continue;

                foreach (string img in SafeFindImgDirectories(top))
                {
                    if (IsInside(img, downloads))
                        continue;

                    bool hasImages = SafeGetFiles(img).Any(IsSupportedImage);
                    if (!hasImages)
                        continue;

                    result.Add(new ImageSource
                    {
                        DisplayName = BuildDisplayName(root, img),
                        ImgPath = img
                    });
                }
            }

            result.Sort(delegate(ImageSource a, ImageSource b)
            {
                return NaturalStringComparer.Instance.Compare(a.DisplayName, b.DisplayName);
            });
            return result;
        }

        private static IEnumerable<string> SafeFindImgDirectories(string start)
        {
            Queue<string> queue = new Queue<string>();
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                string current = queue.Dequeue();
                foreach (string child in SafeGetDirectories(current))
                {
                    if (String.Equals(Path.GetFileName(child), "img", StringComparison.OrdinalIgnoreCase))
                        yield return child;
                    queue.Enqueue(child);
                }
            }
        }

        private static string[] SafeGetDirectories(string path)
        {
            try { return Directory.GetDirectories(path); }
            catch { return new string[0]; }
        }

        private static string[] SafeGetFiles(string path)
        {
            try { return Directory.GetFiles(path); }
            catch { return new string[0]; }
        }

        private static bool IsInside(string path, string parent)
        {
            string fullPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string fullParent = Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return fullPath.StartsWith(fullParent, StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildDisplayName(string root, string imgPath)
        {
            DirectoryInfo owner = Directory.GetParent(imgPath);
            if (owner != null && String.Equals(owner.Name, "static", StringComparison.OrdinalIgnoreCase) && owner.Parent != null)
                owner = owner.Parent;

            string rel = owner == null ? imgPath : MakeRelative(root, owner.FullName);
            return rel.Replace(Path.DirectorySeparatorChar.ToString(), " / ");
        }

        private static string MakeRelative(string root, string path)
        {
            Uri rootUri = new Uri(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar);
            Uri pathUri = new Uri(Path.GetFullPath(path));
            return Uri.UnescapeDataString(rootUri.MakeRelativeUri(pathUri).ToString()).Replace('/', Path.DirectorySeparatorChar);
        }
    }

    internal sealed class PickerWindow : Window
    {
        private readonly string root;
        private readonly List<ImageSource> sources;
        private readonly ListBox list;
        private readonly ProgressBar progress;
        private readonly TextBlock progressText;
        private readonly Button cancelButton;
        private readonly DockPanel loadingPanel;
        private bool isLoading;
        private volatile bool cancelLoading;

        public PickerWindow(string root, List<ImageSource> sources)
        {
            this.root = root;
            this.sources = sources;

            Title = AppVersion.WindowTitle;
            Width = 620;
            Height = 720;
            MinWidth = 420;
            MinHeight = 320;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = new SolidColorBrush(Color.FromRgb(28, 30, 34));
            Foreground = Brushes.White;

            DockPanel rootPanel = new DockPanel();

            const double titleFontSize = 22;

            DockPanel header = new DockPanel();
            header.LastChildFill = true;
            DockPanel.SetDock(header, Dock.Top);

            Button helpButton = new Button();
            helpButton.Content = "?";
            helpButton.Width = titleFontSize;
            helpButton.Height = titleFontSize;
            helpButton.Margin = new Thickness(0, 12, 18, 12);
            helpButton.FontFamily = new FontFamily("Segoe UI");
            helpButton.FontSize = 14;
            helpButton.FontWeight = FontWeights.Bold;
            helpButton.VerticalAlignment = VerticalAlignment.Center;
            helpButton.Click += delegate { ShowHelpWindow(); };
            DockPanel.SetDock(helpButton, Dock.Right);

            TextBlock title = new TextBlock();
            title.Text = "Выберите директорию";
            title.FontFamily = new FontFamily("Segoe UI");
            title.FontSize = titleFontSize;
            title.Padding = new Thickness(18, 12, 0, 12);

            header.Children.Add(helpButton);
            header.Children.Add(title);

            list = new ListBox();
            list.BorderThickness = new Thickness(0);
            list.Background = new SolidColorBrush(Color.FromRgb(38, 41, 46));
            list.Foreground = Brushes.White;
            list.FontFamily = new FontFamily("Segoe UI");
            list.FontSize = 16;
            list.MouseLeftButtonUp += OnListMouseLeftButtonUp;
            list.KeyDown += OnListKeyDown;

            foreach (ImageSource source in sources)
                list.Items.Add(source.DisplayName);

            if (sources.Count == 0)
                list.Items.Add("Папки img с поддерживаемыми изображениями не найдены");
            else
                list.SelectedIndex = 0;

            loadingPanel = new DockPanel();
            loadingPanel.LastChildFill = true;
            loadingPanel.Margin = new Thickness(12, 8, 12, 12);
            loadingPanel.Visibility = Visibility.Collapsed;
            DockPanel.SetDock(loadingPanel, Dock.Bottom);

            cancelButton = new Button();
            cancelButton.Content = "Отмена";
            cancelButton.Width = 96;
            cancelButton.Height = 28;
            cancelButton.Margin = new Thickness(10, 0, 0, 0);
            cancelButton.Click += delegate { cancelLoading = true; };
            DockPanel.SetDock(cancelButton, Dock.Right);

            progressText = new TextBlock();
            progressText.Text = "";
            progressText.Foreground = Brushes.White;
            progressText.VerticalAlignment = VerticalAlignment.Center;
            progressText.Margin = new Thickness(0, 0, 10, 0);
            DockPanel.SetDock(progressText, Dock.Left);

            progress = new ProgressBar();
            progress.Minimum = 0;
            progress.Maximum = 100;
            progress.Height = 18;
            progress.VerticalAlignment = VerticalAlignment.Center;

            loadingPanel.Children.Add(cancelButton);
            loadingPanel.Children.Add(progressText);
            loadingPanel.Children.Add(progress);

            rootPanel.Children.Add(header);
            rootPanel.Children.Add(loadingPanel);
            rootPanel.Children.Add(list);
            Content = rootPanel;
        }

        private void ShowHelpWindow()
        {
            MessageBox.Show(
                this,
                "Внутри выбранной папки должна быть папка img.\nИзображения должны находиться внутри этой папки img.",
                "Инструкция",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void OnListKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                OpenSelected();
                e.Handled = true;
            }
        }

        private void OnListMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (FindParentListBoxItem(e.OriginalSource as DependencyObject) == null)
                return;

            OpenSelected();
            e.Handled = true;
        }

        private static ListBoxItem FindParentListBoxItem(DependencyObject source)
        {
            DependencyObject current = source;
            while (current != null)
            {
                ListBoxItem item = current as ListBoxItem;
                if (item != null)
                    return item;

                try
                {
                    current = VisualTreeHelper.GetParent(current);
                }
                catch
                {
                    return null;
                }
            }
            return null;
        }

        private void OpenSelected()
        {
            if (isLoading)
                return;
            if (list.SelectedIndex < 0 || list.SelectedIndex >= sources.Count)
                return;

            isLoading = true;
            cancelLoading = false;
            list.IsEnabled = false;
            loadingPanel.Visibility = Visibility.Visible;
            progress.Value = 0;
            progressText.Text = "0%";

            System.Drawing.Rectangle screenBounds = GetCurrentScreenBounds();
            ViewerWindow viewer = new ViewerWindow(sources[list.SelectedIndex], screenBounds.Width, screenBounds.Height);
            progressText.Text = viewer.ImageCount <= 0 ? "0%" : "0/" + viewer.ImageCount.ToString() + " (0%)";
            viewer.BuildImageElementsIncremental(
                delegate(int done, int total)
                {
                    int percent = total <= 0 ? 100 : (int)Math.Round(done * 100.0 / total);
                    progress.Value = percent;
                    progressText.Text = total <= 0 ? percent.ToString() + "%" : done.ToString() + "/" + total.ToString() + " (" + percent.ToString() + "%)";
                },
                delegate { return cancelLoading; },
                delegate(bool cancelled)
                {
                    isLoading = false;
                    list.IsEnabled = true;
                    loadingPanel.Visibility = Visibility.Collapsed;

                    if (cancelled)
                    {
                        viewer.DisposeCache();
                        viewer.Close();
                        return;
                    }

                    viewer.Closed += delegate { Show(); };
                    Hide();
                    viewer.Show();
                });
        }

        private System.Drawing.Rectangle GetCurrentScreenBounds()
        {
            try
            {
                Point center = PointToScreen(new Point(Math.Max(0, ActualWidth / 2), Math.Max(0, ActualHeight / 2)));
                Forms.Screen screen = Forms.Screen.FromPoint(new System.Drawing.Point((int)Math.Round(center.X), (int)Math.Round(center.Y)));
                return screen.Bounds;
            }
            catch
            {
                return Forms.Screen.PrimaryScreen.Bounds;
            }
        }
    }

    internal sealed class ViewerWindow : Window
    {
        private const int DiskCacheImageLimit = 700;
        private const double BufferScreens = 3.0;

        private readonly ScrollViewer scroll;
        private readonly Canvas panel;
        private readonly TextBlock status;
        private readonly List<ImageUnit> units;
        private readonly List<ImageItem> items = new List<ImageItem>();
        private readonly ImageSource source;
        private readonly int targetDecodePixelWidth;
        private readonly int targetViewportPixelHeight;
        private readonly bool useDiskCache;
        private readonly Random random = new Random();
        private WindowStyle previousStyle;
        private WindowState previousState;
        private ResizeMode previousResizeMode;
        private Rect previousBounds;
        private string cacheRootPath;
        private string cacheSessionPath;
        private bool isFullscreen;
        private bool isClosed;
        private int unloadSinceCollect;

        public int ImageCount
        {
            get { return items.Count; }
        }

        public ViewerWindow(ImageSource source, int targetDecodePixelWidth, int targetViewportPixelHeight)
        {
            this.source = source;
            Title = source.DisplayName + " - " + AppVersion.WindowTitle;
            Background = Brushes.Black;
            WindowState = WindowState.Maximized;
            this.targetDecodePixelWidth = Math.Max(1, targetDecodePixelWidth);
            this.targetViewportPixelHeight = Math.Max(1, targetViewportPixelHeight);

            units = ImageOrganizer.LoadUnits(source.ImgPath);
            foreach (ImageUnit unit in units)
                items.AddRange(unit.Items);
            useDiskCache = items.Count > DiskCacheImageLimit;

            Grid grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            status = new TextBlock();
            status.Text = source.DisplayName + "    " + AppVersion.WindowTitle + "    R: перемешать    N/B: следующий/предыдущий блок    F11: полный экран";
            status.Background = new SolidColorBrush(Color.FromArgb(210, 16, 18, 22));
            status.Foreground = Brushes.White;
            status.FontFamily = new FontFamily("Segoe UI");
            status.FontSize = 13;
            status.Padding = new Thickness(10, 6, 0, 6);
            Grid.SetRow(status, 0);

            panel = new Canvas();
            panel.Background = Brushes.Black;

            scroll = new ScrollViewer();
            scroll.Content = panel;
            scroll.Background = Brushes.Black;
            scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
            scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            scroll.CanContentScroll = false;
            scroll.Focusable = false;
            scroll.SizeChanged += delegate { UpdateImageWidths(); };
            scroll.ScrollChanged += delegate { UpdateVisibleImages(false); };
            Grid.SetRow(scroll, 1);

            grid.Children.Add(status);
            grid.Children.Add(scroll);
            Content = grid;

            Loaded += delegate
            {
                RebuildLayout();
                UpdateVisibleImages(true);
            };
            KeyDown += OnViewerKeyDown;
        }

        public void BuildImageElementsIncremental(Action<int, int> progress, Func<bool> isCancelled, Action<bool> completed)
        {
            panel.Children.Clear();
            int total = items.Count;
            if (total == 0)
            {
                if (progress != null)
                    progress(0, 0);
                completed(false);
                return;
            }

            foreach (ImageItem item in items)
            {
                item.Element = null;
                item.Bitmap = null;
                item.LoadError = null;
                item.CachePath = null;
                item.PixelWidth = targetDecodePixelWidth;
                item.PixelHeight = 1;
                item.DisplayTop = 0;
                item.DisplayHeight = 1;
                item.IsBitmapLoading = false;
            }

            ThreadPool.QueueUserWorkItem(delegate
            {
                bool cancelled = false;

                try
                {
                    if (useDiskCache)
                        PrepareCacheDirectory();

                    for (int index = 0; index < total; index++)
                    {
                        if (isCancelled != null && isCancelled())
                        {
                            cancelled = true;
                            break;
                        }

                        ImageItem item = items[index];
                        try
                        {
                            if (useDiskCache)
                                PrepareCachedBitmap(item, index);
                            else
                                PrepareRamBitmap(item);
                        }
                        catch (Exception ex)
                        {
                            item.Bitmap = null;
                            item.CachePath = null;
                            item.PixelWidth = targetDecodePixelWidth;
                            item.PixelHeight = Math.Max(80, targetDecodePixelWidth / 2);
                            item.LoadError = ex.Message;
                        }

                        PostProgress(progress, index + 1, total);
                        if ((index + 1) % 25 == 0)
                            GC.Collect(0);
                    }
                }
                catch (Exception ex)
                {
                    foreach (ImageItem item in items)
                    {
                        item.LoadError = ex.Message;
                        item.PixelWidth = targetDecodePixelWidth;
                        item.PixelHeight = Math.Max(80, targetDecodePixelWidth / 2);
                    }
                }

                Dispatcher.BeginInvoke((Action)delegate
                {
                    if (cancelled || (isCancelled != null && isCancelled()))
                    {
                        DisposeCache();
                        completed(true);
                        return;
                    }

                    RebuildLayout();
                    scroll.ScrollToVerticalOffset(0);
                    UpdateVisibleImages(true);
                    if (progress != null)
                        progress(total, total);
                    completed(false);
                }, DispatcherPriority.Normal);
            });
        }

        public void DisposeCache()
        {
            string session = cacheSessionPath;
            cacheSessionPath = null;
            if (!String.IsNullOrEmpty(session))
                SafeDeleteDirectory(session);

            try
            {
                if (!String.IsNullOrEmpty(cacheRootPath) && Directory.Exists(cacheRootPath) && Directory.GetFileSystemEntries(cacheRootPath).Length == 0)
                    Directory.Delete(cacheRootPath);
            }
            catch
            {
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            isClosed = true;
            ReleaseViewerResources();
            DisposeCache();
            base.OnClosed(e);
            ForceFullGarbageCollection();
        }

        private void PrepareCacheDirectory()
        {
            DirectoryInfo imgParent = Directory.GetParent(source.ImgPath);
            string parentPath = imgParent == null ? source.ImgPath : imgParent.FullName;
            cacheRootPath = Path.Combine(parentPath, ".ImageGalleryViewerCache");

            Directory.CreateDirectory(cacheRootPath);
            SetHidden(cacheRootPath);

            foreach (string directory in Directory.GetDirectories(cacheRootPath))
                SafeDeleteDirectory(directory);

            foreach (string file in Directory.GetFiles(cacheRootPath))
            {
                try { File.Delete(file); }
                catch { }
            }

            cacheSessionPath = Path.Combine(cacheRootPath, "session_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + "_" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(cacheSessionPath);
            SetHidden(cacheSessionPath);
        }

        private static void SetHidden(string path)
        {
            try
            {
                File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.Hidden);
            }
            catch
            {
            }
        }

        private static void SafeDeleteDirectory(string path)
        {
            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    if (Directory.Exists(path))
                        Directory.Delete(path, true);
                    return;
                }
                catch
                {
                    Thread.Sleep(60);
                }
            }
        }

        private void PrepareCachedBitmap(ImageItem item, int index)
        {
            int originalWidth;
            int originalHeight;
            GetImageDimensions(item.Path, out originalWidth, out originalHeight);

            string cachePath = Path.Combine(cacheSessionPath, index.ToString("D6") + ".bmp");
            BitmapImage bitmap = new BitmapImage();
            using (FileStream stream = File.OpenRead(item.Path))
            {
                bitmap.BeginInit();
                bitmap.StreamSource = stream;
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                if (originalWidth > targetDecodePixelWidth)
                    bitmap.DecodePixelWidth = targetDecodePixelWidth;
                bitmap.EndInit();
                int forceLoadWidth = bitmap.PixelWidth;
                int forceLoadHeight = bitmap.PixelHeight;
            }

            using (FileStream output = File.Create(cachePath))
            {
                BmpBitmapEncoder encoder = new BmpBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                encoder.Save(output);
            }

            item.CachePath = cachePath;
            item.PixelWidth = Math.Max(1, bitmap.PixelWidth);
            item.PixelHeight = Math.Max(1, bitmap.PixelHeight);
            item.Bitmap = null;
        }

        private void PrepareRamBitmap(ImageItem item)
        {
            int originalWidth;
            int originalHeight;
            GetImageDimensions(item.Path, out originalWidth, out originalHeight);

            BitmapImage bitmap = new BitmapImage();
            using (FileStream stream = File.OpenRead(item.Path))
            {
                bitmap.BeginInit();
                bitmap.StreamSource = stream;
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                if (originalWidth > targetDecodePixelWidth)
                    bitmap.DecodePixelWidth = targetDecodePixelWidth;
                bitmap.EndInit();
                int forceLoadWidth = bitmap.PixelWidth;
                int forceLoadHeight = bitmap.PixelHeight;
            }

            if (bitmap.CanFreeze)
                bitmap.Freeze();

            item.CachePath = null;
            item.PixelWidth = Math.Max(1, bitmap.PixelWidth);
            item.PixelHeight = Math.Max(1, bitmap.PixelHeight);
            item.Bitmap = bitmap;
        }

        private static void GetImageDimensions(string path, out int width, out int height)
        {
            using (FileStream stream = File.OpenRead(path))
            {
                BitmapDecoder decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
                BitmapFrame frame = decoder.Frames[0];
                width = Math.Max(1, frame.PixelWidth);
                height = Math.Max(1, frame.PixelHeight);
            }
        }

        private void PostProgress(Action<int, int> progress, int done, int total)
        {
            if (progress == null)
                return;

            Dispatcher.BeginInvoke((Action)delegate
            {
                progress(done, total);
            }, DispatcherPriority.Normal);
        }

        private void RebuildLayout()
        {
            double width = GetViewportWidth();
            double top = 0;

            for (int i = 0; i < items.Count; i++)
            {
                ImageItem item = items[i];
                item.DisplayTop = top;
                item.DisplayHeight = CalculateDisplayHeight(item, width);
                top += item.DisplayHeight;

                FrameworkElement element = item.Element;
                if (element != null)
                {
                    element.Width = width;
                    element.Height = item.DisplayHeight;
                    Canvas.SetLeft(element, 0);
                    Canvas.SetTop(element, item.DisplayTop);
                }
            }

            panel.Width = width;
            panel.Height = Math.Max(1, top);
        }

        private double CalculateDisplayHeight(ImageItem item, double width)
        {
            if (item.PixelWidth <= 0 || item.PixelHeight <= 0)
                return Math.Max(1, width);
            return Math.Max(1, Math.Round(width * item.PixelHeight / item.PixelWidth));
        }

        private double GetViewportWidth()
        {
            return scroll.ActualWidth > 1 ? scroll.ActualWidth : targetDecodePixelWidth;
        }

        private double GetViewportHeight()
        {
            return scroll.ViewportHeight > 1 ? scroll.ViewportHeight : targetViewportPixelHeight;
        }

        private void UpdateImageWidths()
        {
            RebuildLayout();
            UpdateVisibleImages(false);
        }

        private void UpdateVisibleImages(bool synchronous)
        {
            if (isClosed || items.Count == 0)
                return;

            if (!useDiskCache)
            {
                for (int i = 0; i < items.Count; i++)
                    EnsureImageElement(items[i]);
                return;
            }

            double viewportHeight = GetViewportHeight();
            double buffer = Math.Max(300, viewportHeight * BufferScreens);
            double start = Math.Max(0, scroll.VerticalOffset - buffer);
            double end = scroll.VerticalOffset + viewportHeight + buffer;

            for (int i = 0; i < items.Count; i++)
            {
                ImageItem item = items[i];
                bool shouldKeep = item.DisplayTop <= end && item.DisplayTop + item.DisplayHeight >= start;
                if (shouldKeep)
                {
                    EnsureImageElement(item);
                    EnsureBitmapLoaded(item, synchronous);
                }
                else
                {
                    UnloadImage(item);
                }
            }
        }

        private bool IsItemInActiveRange(ImageItem item)
        {
            if (!useDiskCache)
                return true;

            double viewportHeight = GetViewportHeight();
            double buffer = Math.Max(300, viewportHeight * BufferScreens);
            double start = Math.Max(0, scroll.VerticalOffset - buffer);
            double end = scroll.VerticalOffset + viewportHeight + buffer;
            return item.DisplayTop <= end && item.DisplayTop + item.DisplayHeight >= start;
        }

        private void EnsureImageElement(ImageItem item)
        {
            if (item.Element != null)
                return;

            System.Windows.Controls.Image image = new System.Windows.Controls.Image();
            image.Stretch = Stretch.Uniform;
            image.HorizontalAlignment = HorizontalAlignment.Left;
            image.VerticalAlignment = VerticalAlignment.Top;
            image.SnapsToDevicePixels = true;
            image.Width = GetViewportWidth();
            image.Height = item.DisplayHeight;
            if (item.Bitmap != null)
                image.Source = item.Bitmap;
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
            Canvas.SetLeft(image, 0);
            Canvas.SetTop(image, item.DisplayTop);
            item.Element = image;
            panel.Children.Add(image);
        }

        private void EnsureBitmapLoaded(ImageItem item, bool synchronous)
        {
            if (!useDiskCache)
                return;

            if (item.Bitmap != null || item.IsBitmapLoading || String.IsNullOrEmpty(item.CachePath))
                return;

            if (synchronous)
            {
                try
                {
                    item.Bitmap = LoadCachedBitmap(item.CachePath);
                    SetElementSource(item);
                }
                catch (Exception ex)
                {
                    item.LoadError = ex.Message;
                }
                return;
            }

            item.IsBitmapLoading = true;
            string cachePath = item.CachePath;
            ThreadPool.QueueUserWorkItem(delegate
            {
                BitmapSource bitmap = null;
                string error = null;
                try
                {
                    bitmap = LoadCachedBitmap(cachePath);
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                }

                Dispatcher.BeginInvoke((Action)delegate
                {
                    item.IsBitmapLoading = false;
                    if (isClosed)
                        return;

                    if (error != null)
                    {
                        item.LoadError = error;
                        return;
                    }

                    if (bitmap != null && IsItemInActiveRange(item))
                    {
                        item.Bitmap = bitmap;
                        EnsureImageElement(item);
                        SetElementSource(item);
                    }
                }, DispatcherPriority.Background);
            });
        }

        private static BitmapImage LoadCachedBitmap(string path)
        {
            BitmapImage bitmap = new BitmapImage();
            using (FileStream stream = File.OpenRead(path))
            {
                bitmap.BeginInit();
                bitmap.StreamSource = stream;
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                bitmap.EndInit();
                int forceLoadWidth = bitmap.PixelWidth;
                int forceLoadHeight = bitmap.PixelHeight;
            }
            if (bitmap.CanFreeze)
                bitmap.Freeze();
            return bitmap;
        }

        private void SetElementSource(ImageItem item)
        {
            System.Windows.Controls.Image image = item.Element as System.Windows.Controls.Image;
            if (image != null)
                image.Source = item.Bitmap;
        }

        private void UnloadImage(ImageItem item)
        {
            if (item.Element != null)
            {
                System.Windows.Controls.Image image = item.Element as System.Windows.Controls.Image;
                if (image != null)
                    image.Source = null;
                panel.Children.Remove(item.Element);
                item.Element = null;
            }

            if (item.Bitmap != null)
            {
                item.Bitmap = null;
                unloadSinceCollect++;
                if (unloadSinceCollect >= 80)
                {
                    unloadSinceCollect = 0;
                    GC.Collect(0);
                }
            }
        }

        private void UnloadAllImages()
        {
            ClearImageElements(true);
        }

        private void ReleaseViewerResources()
        {
            ClearImageElements(true);
            items.Clear();
            units.Clear();
            scroll.Content = null;
            Content = null;
            ForceFullGarbageCollection();
        }

        private void ClearImageElements(bool releaseBitmaps)
        {
            foreach (ImageItem item in items)
            {
                System.Windows.Controls.Image image = item.Element as System.Windows.Controls.Image;
                if (image != null)
                    image.Source = null;

                if (releaseBitmaps)
                    item.Bitmap = null;
                item.Element = null;
                item.IsBitmapLoading = false;
            }
            panel.Children.Clear();
            if (releaseBitmaps)
                ForceFullGarbageCollection();
        }

        private static void ForceFullGarbageCollection()
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
            GC.WaitForPendingFinalizers();
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
        }

        private void OnViewerKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Down)
            {
                scroll.ScrollToVerticalOffset(scroll.VerticalOffset + 55);
                e.Handled = true;
            }
            else if (e.Key == Key.Up)
            {
                scroll.ScrollToVerticalOffset(scroll.VerticalOffset - 55);
                e.Handled = true;
            }
            else if (e.Key == Key.R)
            {
                ShuffleUnits();
                e.Handled = true;
            }
            else if (e.Key == Key.N)
            {
                GoToAdjacentBlock(true);
                e.Handled = true;
            }
            else if (e.Key == Key.B)
            {
                GoToAdjacentBlock(false);
                e.Handled = true;
            }
            else if (e.Key == Key.Space)
            {
                GoToNextImage();
                e.Handled = true;
            }
            else if (e.Key == Key.F11)
            {
                ToggleFullscreen();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape && isFullscreen)
            {
                ToggleFullscreen();
                e.Handled = true;
            }
        }

        private void ShuffleUnits()
        {
            for (int i = units.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                ImageUnit temp = units[i];
                units[i] = units[j];
                units[j] = temp;
            }

            if (useDiskCache)
                UnloadAllImages();
            else
                ClearImageElements(false);
            items.Clear();
            foreach (ImageUnit unit in units)
                items.AddRange(unit.Items);
            RebuildLayout();
            scroll.ScrollToVerticalOffset(0);
            UpdateVisibleImages(true);
        }

        private void GoToAdjacentBlock(bool forward)
        {
            int itemIndex = GetCurrentItemIndex();
            if (itemIndex < 0 || itemIndex >= items.Count)
                return;

            ImageUnit current = items[itemIndex].Unit;
            if (current == null || !current.IsBlock)
                return;

            int unitIndex = units.IndexOf(current);
            if (unitIndex < 0)
                return;

            if (forward)
            {
                for (int i = unitIndex + 1; i < units.Count; i++)
                {
                    if (units[i].IsBlock)
                    {
                        ScrollToItem(units[i].Items[0]);
                        return;
                    }
                }
                ScrollToItemAfterUnit(current);
            }
            else
            {
                for (int i = unitIndex - 1; i >= 0; i--)
                {
                    if (units[i].IsBlock)
                    {
                        ScrollToItem(units[i].Items[0]);
                        return;
                    }
                }
                ScrollToItemBeforeUnit(current);
            }
        }

        private void GoToNextImage()
        {
            int itemIndex = GetCurrentItemIndex();
            if (itemIndex >= 0 && itemIndex + 1 < items.Count)
                ScrollToItem(items[itemIndex + 1]);
        }

        private int GetCurrentItemIndex()
        {
            if (items.Count == 0)
                return -1;

            double probe = scroll.VerticalOffset + Math.Max(1, GetViewportHeight() / 3);
            return FindItemIndexByOffset(probe);
        }

        private int FindItemIndexByOffset(double offset)
        {
            if (items.Count == 0)
                return -1;

            int low = 0;
            int high = items.Count - 1;
            while (low <= high)
            {
                int mid = low + (high - low) / 2;
                ImageItem item = items[mid];
                if (offset < item.DisplayTop)
                    high = mid - 1;
                else if (offset >= item.DisplayTop + item.DisplayHeight)
                    low = mid + 1;
                else
                    return mid;
            }

            if (low < 0)
                return 0;
            if (low >= items.Count)
                return items.Count - 1;
            return low;
        }

        private void ScrollToItemAfterUnit(ImageUnit unit)
        {
            int last = items.IndexOf(unit.Items[unit.Items.Count - 1]);
            if (last >= 0 && last + 1 < items.Count)
                ScrollToItem(items[last + 1]);
        }

        private void ScrollToItemBeforeUnit(ImageUnit unit)
        {
            int first = items.IndexOf(unit.Items[0]);
            if (first > 0)
                ScrollToItem(items[first - 1]);
        }

        private void ScrollToItem(ImageItem item)
        {
            scroll.ScrollToVerticalOffset(item.DisplayTop);
            UpdateVisibleImages(false);
        }

        private void ToggleFullscreen()
        {
            if (!isFullscreen)
            {
                previousStyle = WindowStyle;
                previousState = WindowState;
                previousResizeMode = ResizeMode;
                previousBounds = new Rect(Left, Top, Width, Height);
                status.Visibility = Visibility.Collapsed;
                WindowStyle = WindowStyle.None;
                ResizeMode = ResizeMode.NoResize;
                Topmost = true;
                WindowState = WindowState.Normal;

                Forms.Screen screen = Forms.Screen.FromPoint(new System.Drawing.Point((int)Left, (int)Top));
                Left = screen.Bounds.Left;
                Top = screen.Bounds.Top;
                Width = screen.Bounds.Width;
                Height = screen.Bounds.Height;
                isFullscreen = true;
            }
            else
            {
                Topmost = false;
                WindowStyle = previousStyle;
                ResizeMode = previousResizeMode;
                WindowState = previousState;
                if (previousState == WindowState.Normal)
                {
                    Left = previousBounds.Left;
                    Top = previousBounds.Top;
                    Width = previousBounds.Width;
                    Height = previousBounds.Height;
                }
                status.Visibility = Visibility.Visible;
                isFullscreen = false;
            }
        }
    }

    internal static class ImageOrganizer
    {
        private static readonly Regex BlockPattern = new Regex(@"^(.+?)_([0-9]+)(?:_([0-9]+))?$", RegexOptions.Compiled);

        public static List<ImageUnit> LoadUnits(string imgPath)
        {
            string[] files = Directory.GetFiles(imgPath)
                .Where(SourceScanner.IsSupportedImage)
                .OrderBy(Path.GetFileName, NaturalStringComparer.Instance)
                .ToArray();

            Dictionary<string, ImageUnit> blocks = new Dictionary<string, ImageUnit>(StringComparer.OrdinalIgnoreCase);
            List<ImageUnit> singles = new List<ImageUnit>();

            foreach (string file in files)
            {
                string name = Path.GetFileNameWithoutExtension(file);
                Match match = BlockPattern.Match(name);
                ImageItem item = new ImageItem { Path = file, FileName = Path.GetFileName(file) };

                if (match.Success)
                {
                    string key = match.Groups[1].Value;
                    ImageUnit unit;
                    if (!blocks.TryGetValue(key, out unit))
                    {
                        unit = new ImageUnit { Key = key, IsBlock = true };
                        blocks.Add(key, unit);
                    }
                    item.Unit = unit;
                    unit.Items.Add(item);
                }
                else
                {
                    ImageUnit unit = new ImageUnit { Key = name, IsBlock = false };
                    item.Unit = unit;
                    unit.Items.Add(item);
                    singles.Add(unit);
                }
            }

            foreach (ImageUnit block in blocks.Values)
            {
                block.Items.Sort(delegate(ImageItem a, ImageItem b)
                {
                    return CompareBlockItems(a.FileName, b.FileName);
                });
            }

            List<ImageUnit> units = new List<ImageUnit>();
            units.AddRange(blocks.Values);
            units.AddRange(singles);
            units.Sort(delegate(ImageUnit a, ImageUnit b)
            {
                return NaturalStringComparer.Instance.Compare(a.Items[0].FileName, b.Items[0].FileName);
            });
            return units;
        }

        private static int CompareBlockItems(string left, string right)
        {
            string l = Path.GetFileNameWithoutExtension(left);
            string r = Path.GetFileNameWithoutExtension(right);
            Match lm = BlockPattern.Match(l);
            Match rm = BlockPattern.Match(r);
            if (lm.Success && rm.Success && String.Equals(lm.Groups[1].Value, rm.Groups[1].Value, StringComparison.OrdinalIgnoreCase))
            {
                int l1 = Int32.Parse(lm.Groups[2].Value);
                int r1 = Int32.Parse(rm.Groups[2].Value);
                if (l1 != r1) return l1.CompareTo(r1);

                int l2 = lm.Groups[3].Success ? Int32.Parse(lm.Groups[3].Value) : -1;
                int r2 = rm.Groups[3].Success ? Int32.Parse(rm.Groups[3].Value) : -1;
                if (l2 != r2) return l2.CompareTo(r2);
            }
            return NaturalStringComparer.Instance.Compare(left, right);
        }
    }

    internal sealed class NaturalStringComparer : IComparer<string>
    {
        public static readonly NaturalStringComparer Instance = new NaturalStringComparer();

        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
        private static extern int StrCmpLogicalW(string x, string y);

        public int Compare(string x, string y)
        {
            return StrCmpLogicalW(x ?? String.Empty, y ?? String.Empty);
        }
    }
}
