using ImageBrowser.Models;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace ImageBrowser
{
    public partial class FullImageWindow : Window, INotifyPropertyChanged
    {
        private readonly List<ImageItem> _images;
        private int _currentIndex;

        private string? _filePath;
        private BitmapImage? _imageSource;

        public string? FilePath
        {
            get => _filePath;
            private set
            {
                _filePath = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FileName));
            }
        }

        public string? FileName =>
            FilePath != null ? Path.GetFileName(FilePath) : null;

        public BitmapImage? ImageSource
        {
            get => _imageSource;
            private set
            {
                _imageSource = value;
                OnPropertyChanged();
            }
        }

        // ✅ New Constructor With Navigation Support
        public FullImageWindow(List<ImageItem> images, int startIndex)
        {
            InitializeComponent();

            _images = images;
            _currentIndex = startIndex;

            DataContext = this;

            // load image asynchronously to avoid blocking the UI thread
            _ = LoadCurrentImageAsync();
        }

        // ✅ Loads Current Image Based On Index
        private async Task LoadCurrentImageAsync()
        {
            if (_currentIndex < 0 || _currentIndex >= _images.Count)
                return;

            var path = _images[_currentIndex].FilePath;

            if (!File.Exists(path))
                return;

            FilePath = path;

            // load on background thread and freeze the bitmap so it can be set from UI thread
            var bmp = await Task.Run(() => LoadBitmap(path));

            ImageSource = bmp;
        }

        private BitmapImage LoadBitmap(string filePath)
        {
            var bitmap = new BitmapImage();

            using var stream = File.OpenRead(filePath);

            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();

            return bitmap;
        }

        // ✅ Keyboard Navigation
        private async void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
            else if (e.Key == Key.Right)
            {
                if (_currentIndex < _images.Count - 1)
                {
                    _currentIndex++;
                    await LoadCurrentImageAsync();
                }
            }
            else if (e.Key == Key.Left)
            {
                if (_currentIndex > 0)
                {
                    _currentIndex--;
                    await LoadCurrentImageAsync();
                }
            }
        }

        // ✅ Property Changed Implementation
        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this,
                new PropertyChangedEventArgs(propertyName));
        }
    }
}