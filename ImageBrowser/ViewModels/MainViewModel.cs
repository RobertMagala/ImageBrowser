using ImageBrowser.Infrastructure;
using ImageBrowser.Models;
using ImageBrowser.Services;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;


namespace ImageBrowser.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private AppSettings Settings { get; set; }
        private string _currentCacheFolder = string.Empty;

        //OpenImageCommand - a command that is bound to the UI (e.g., when a thumbnail is clicked) to open the full-size image in a new window.
        //It uses the RelayCommand class, which is a common implementation of ICommand that allows you to define the action to be executed when the command is invoked.
        public ICommand OpenImageCommand { get; }

        //Constructor - initializes the OpenImageCommand and sets up the ThumbnailDatabase.
        //The LoadImages method is commented out, allowing you to call LoadFolder with a specific path when needed.
        public MainViewModel()
        {
            OpenImageCommand = new RelayCommand(OpenFullImage);

            // load persisted settings
            Settings = AppSettings.Load();

            ThumbnailSize = Settings.ThumbnailSize;
            JpegQuality = Settings.JpegQuality;

            // apply background color from settings
            BackgroundBrush = Settings.BackgroundColor?.ToLowerInvariant() switch
            {
                "black" => Brushes.Black,
                "gray" => Brushes.Gray,
                _ => Brushes.White,
            };

            _currentCacheFolder = Settings.CacheDirectory;
            _thumbnailDb = new ThumbnailDatabase(_currentCacheFolder);
            // initialize semaphore from settings (guard against invalid values)
            var concurrency = Settings.MaxConcurrency;
            if (concurrency <= 0) concurrency = 1;
            _semaphore = new SemaphoreSlim(concurrency);
        }

        public void ReloadSettings()
        {
            var s = AppSettings.Load();
            ThumbnailSize = s.ThumbnailSize;
            JpegQuality = s.JpegQuality;

            if (!string.Equals(s.CacheDirectory, _currentCacheFolder, StringComparison.OrdinalIgnoreCase))
            {
                _thumbnailDb = new ThumbnailDatabase(s.CacheDirectory);
                _currentCacheFolder = s.CacheDirectory;
            }

            // update in-memory settings reference
            var oldMax = Settings.MaxConcurrency;

            Settings.CacheDirectory = s.CacheDirectory;
            Settings.JpegQuality = s.JpegQuality;
            Settings.ThumbnailSize = s.ThumbnailSize;
            Settings.BackgroundColor = s.BackgroundColor;
            Settings.ChunkSize = s.ChunkSize;
            Settings.MaxConcurrency = s.MaxConcurrency;

            // if concurrency changed, recreate semaphore
            if (s.MaxConcurrency != oldMax)
            {
                try { _semaphore?.Dispose(); } catch { }
                _semaphore = new SemaphoreSlim(s.MaxConcurrency);
            }

            BackgroundBrush = s.BackgroundColor?.ToLowerInvariant() switch
            {
                "black" => Brushes.Black,
                "gray" => Brushes.Gray,
                _ => Brushes.White,
            };
        }

        //ObservableCollection<ImageItem> - a collection that holds ImageItem objects, which represent individual images with their file paths and thumbnails.
        //The ObservableCollection is used to automatically notify the UI when items are added or removed, allowing for dynamic updates to the image list displayed in the application.
        public ObservableCollection<ImageItem> Images { get; } = new();

        //SemaphoreSlim - used to limit the number of concurrent thumbnail generation tasks.
        //This is important when dealing with network drives or large images, as generating thumbnails can be resource-intensive.
        //By using a semaphore, we can ensure that only a certain number of thumbnail generation tasks run at the same time, preventing excessive CPU and memory usage.
        private SemaphoreSlim _semaphore; // created from settings

        // Keep track of in-progress generation tasks per image hash to avoid duplicating work
        private readonly ConcurrentDictionary<string, Task> _generationTasks = new();

        //BackgroundBrush - a property that holds the background brush for the UI, allowing for dynamic changes to the background color or pattern.
        private Brush _backgroundBrush = Brushes.White;

        public Brush BackgroundBrush
        {
            get => _backgroundBrush;
            set
            {
                _backgroundBrush = value;
                OnPropertyChanged();
            }
        }

        //ThumbnailSize - a property that defines the size of the thumbnails to be generated.
        private int _thumbnailSize = 250;
        public int ThumbnailSize
        {
            get => _thumbnailSize;
            set
            {
                if (_thumbnailSize != value)
                {
                    _thumbnailSize = value;
                    OnPropertyChanged();
                }
            }
        }

        // JpegQuality - persistence for saved thumbnails
        private int _jpegQuality = 70;
        public int JpegQuality
        {
            get => _jpegQuality;
            set
            {
                if (_jpegQuality != value)
                {
                    _jpegQuality = value;
                    OnPropertyChanged();
                }
            }
        }

        //ThumbnailDatabase - a simple class that manages the SQLite database for storing and retrieving thumbnails.
        //It provides methods to save a thumbnail (SaveThumbnail) and retrieve a thumbnail (GetThumbnail) based on a unique hash key derived from the image file path.
        private ThumbnailDatabase _thumbnailDb;

        //LoadThumbnailAsync - an asynchronous method that loads a thumbnail for a given ImageItem.
        //It first checks if a cached thumbnail exists in the database using the computed hash of the file path. If it exists, it loads the thumbnail from the database.
        //If not, it generates a new thumbnail, saves it to the database, and then loads it. The method also updates the progress counters and current file being processed.
        //The use of async/await ensures that the UI remains responsive while thumbnails are being loaded and generated in the background.
        //The method also uses a semaphore to limit the number of concurrent thumbnail generation tasks, preventing excessive resource usage when processing many images or large files.
        private async Task LoadThumbnailAsync(ImageItem item)
        {
            var fileName = Path.GetFileName(item.FilePath);

            App.Current.Dispatcher.Invoke(() =>
            {
                CurrentFile = fileName;
            });
            try
            {
                var hash = ComputeHash(item.FilePath);

                var cachedBytes = _thumbnailDb.GetThumbnail(hash);

                if (cachedBytes != null)
                {
                    var bitmap = await Task.Run(() =>
                    {
                        using var ms = new MemoryStream(cachedBytes);

                        var img = new BitmapImage();
                        img.BeginInit();
                        img.CacheOption = BitmapCacheOption.OnLoad;
                        img.StreamSource = ms;
                        img.EndInit();
                        img.Freeze();

                        return img;
                    });

                    item.Thumbnail = bitmap;
                    return;
                }

                // If no cached thumbnail, use or start a generation task so we don't duplicate work
                var generationTask = _generationTasks.GetOrAdd(hash, _ => Task.Run(async () =>
                {
                    try
                    {
                        await GenerateAndSaveThumbnailAsync(item.FilePath, ThumbnailSize);
                    }
                    finally
                    {
                        _generationTasks.TryRemove(hash, out var _task);
                    }
                }));

                // Wait for generation to complete (may be created by a different caller)
                await generationTask.ConfigureAwait(false);

                // After generation, load from DB
                var resultBytes = _thumbnailDb.GetThumbnail(hash);
                if (resultBytes != null)
                {
                    var bitmap = await Task.Run(() =>
                    {
                        using var ms = new MemoryStream(resultBytes);

                        var img = new BitmapImage();
                        img.BeginInit();
                        img.CacheOption = BitmapCacheOption.OnLoad;
                        img.StreamSource = ms;
                        img.EndInit();
                        img.Freeze();

                        return img;
                    });

                    item.Thumbnail = bitmap;
                }
            }
            catch
            {
                // Ignore corrupted files
            }
            finally
            {
                // Increment atomically and publish the latest value to the UI.
                // Use CompareExchange to read the current value on the UI thread so
                // we don't accidentally overwrite a newer value with a stale one
                // when multiple background tasks complete concurrently.
                Interlocked.Increment(ref _processedCount);

                App.Current.Dispatcher.Invoke(() =>
                {
                    var current = Interlocked.CompareExchange(ref _processedCount, 0, 0);
                    ProcessedCount = current;
                });
            }
        }

        // Generate thumbnail and save into DB. Uses semaphore to limit concurrency.
        private async Task GenerateAndSaveThumbnailAsync(string filePath, int decodeWidth)
        {
            var hash = ComputeHash(filePath);

            await _semaphore.WaitAsync();
            try
            {
                // Double-check DB in case another task saved it already
                var cached = _thumbnailDb.GetThumbnail(hash);
                if (cached != null)
                    return;

                // Generate thumbnail
                BitmapImage? thumbnail = null;

                try
                {
                    thumbnail = await Task.Run(() =>
                    {
                        var image = new BitmapImage();

                        using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 8192, FileOptions.SequentialScan))
                        {
                            image.BeginInit();
                            image.DecodePixelWidth = decodeWidth;
                            image.CacheOption = BitmapCacheOption.OnLoad;
                            image.StreamSource = stream;
                            image.EndInit();
                            image.Freeze();
                        }

                        return image;
                    });
                }
                catch
                {
                    // ignore generation errors for individual files
                    return;
                }

                // Save thumbnail into SQLite database
                await Task.Run(() =>
                {
                    byte[] imageBytes;

                    using (var ms = new MemoryStream())
                    {
                        var encoder = new JpegBitmapEncoder
                        {
                            QualityLevel = JpegQuality
                        };

                        encoder.Frames.Add(BitmapFrame.Create(thumbnail));
                        encoder.Save(ms);

                        imageBytes = ms.ToArray();
                    }

                    _thumbnailDb.SaveThumbnail(hash, imageBytes);
                });
            }
            finally
            {
                _semaphore.Release();
            }
        }

        //LoadFolder - loads image file paths from the specified folder and creates ImageItem instances for each file.
        //It processes files in batches of 50 to keep the UI responsive, adding them to the ObservableCollection and starting thumbnail loading asynchronously.
        public async Task LoadFolder(string path)
        {
            TotalCount = 0;
            ProcessedCount = 0;
            CurrentFile = null;
            Images.Clear();

            // capture chunk size so background worker uses a consistent batch size
            var chunk = Settings?.ChunkSize ?? 50;

            await Task.Run(async () =>
            {
                var allowedExt = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    ".png",
                    ".jpg",
                    ".jpeg",
                    ".bmp",
                    ".gif",
                    ".tif",
                    ".tiff",
                    ".webp",
                    ".heic"
                };

                var files = Directory.EnumerateFiles(path, "*.*")
                    .Where(f => allowedExt.Contains(Path.GetExtension(f)))
                    .ToList();
                var count = files.Count;

                await App.Current.Dispatcher.InvokeAsync(() =>
                {
                    TotalCount = count;
                });

                var batch = new List<ImageItem>(chunk);

                foreach (var file in files)
                {
                    batch.Add(new ImageItem(file));

                    if (batch.Count >= chunk)
                    {
                        var itemsToAdd = batch.ToList();
                        batch.Clear();

                        await App.Current.Dispatcher.InvokeAsync(() =>
                        {
                            foreach (var item in itemsToAdd)
                            {
                                Images.Add(item);
                                // Start thumbnail loading on a thread-pool thread so DB/IO work
                                // doesn't run on the UI thread and block the UI.
                                Task.Run(() => LoadThumbnailAsync(item));
                            }
                        });
                    }
                }

                // Add remaining
                if (batch.Count > 0)
                {
                    var remaining = batch.ToList();

                    await App.Current.Dispatcher.InvokeAsync(() =>
                    {
                        foreach (var item in remaining)
                        {
                            Images.Add(item);
                            // Run thumbnail loading off the UI thread to avoid UI hangs
                            Task.Run(() => LoadThumbnailAsync(item));
                        }
                    });
                }

                // Start background preloading of thumbnails into the database for all files.
                // Fire-and-forget each generation task so LoadFolder returns quickly instead
                // of awaiting every preload sequentially.
                foreach (var file in files)
                {
                    var hash = ComputeHash(file);
                    _ = _generationTasks.GetOrAdd(hash, _ => Task.Run(async () =>
                    {
                        try
                        {
                            await GenerateAndSaveThumbnailAsync(file, ThumbnailSize);
                        }
                        finally
                        {
                            _generationTasks.TryRemove(hash, out var _task);
                        }
                    }));
                }

                // Start a short-lived monitor to correct the processed count in case
                // some thumbnail completions raced with the UI updates and left the
                // displayed counters off by a few items. This polls the visual
                // collection for assigned thumbnails and publishes the accurate
                // processed count to the UI. It's fire-and-forget and stops once
                // everything is accounted for or after a short timeout.
                _ = Task.Run(async () =>
                {
                    const int maxChecks = 15;
                    for (int i = 0; i < maxChecks; i++)
                    {
                        await Task.Delay(200).ConfigureAwait(false);

                        int done = 0;
                        App.Current.Dispatcher.Invoke(() =>
                        {
                            done = Images.Count(img => img.Thumbnail != null);
                        });

                        // Publish the observed value to the UI
                        App.Current.Dispatcher.Invoke(() =>
                        {
                            ProcessedCount = Math.Min(done, TotalCount);
                        });

                        if (done >= count)
                            break;
                    }
                });
            });
        }

        //OpenFullImage - opens a new window to display the full-size image when a thumbnail is clicked
        private void OpenFullImage(object? parameter)
        {
            if (parameter is not string path)
                return;

            var index = Images
                .Select((img, i) => new { img.FilePath, Index = i })
                .FirstOrDefault(x => x.FilePath == path)?.Index ?? -1;

            if (index < 0)
                return;

            var viewer = new FullImageWindow(Images.ToList(), index);
            viewer.Show();
        }

        //ComputeHash - simple hash function to generate a unique key for each image file based on its path
        private static string ComputeHash(string filePath)
        {
            var hash = System.Security.Cryptography.SHA1.HashData(
                System.Text.Encoding.UTF8.GetBytes(filePath));

            return BitConverter.ToString(hash).Replace("-", "");
        }

        //_TotalCount, _ProcessedCount, RemainingCount, and CurrentFile - properties to track the progress of loading and processing images.
        //TotalCount represents the total number of images to be processed, ProcessedCount tracks how many have been processed so far,
        //RemainingCount calculates how many are left, and CurrentFile holds the path of the currently processed file.
        //These properties can be used to update the UI with progress information.
        private int _totalCount;
        public int TotalCount
        {
            get => _totalCount;
            set
            {
                _totalCount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(RemainingCount));
            }
        }

        private int _processedCount;
        public int ProcessedCount
        {
            get => _processedCount;
            set
            {
                _processedCount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(RemainingCount));
            }
        }

        public int RemainingCount => TotalCount - ProcessedCount;

        private string? _currentFile;
        public string? CurrentFile
        {
            get => _currentFile;
            set
            {
                _currentFile = value;
                OnPropertyChanged();
            }
        }
    }
}