using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;

namespace ImageBrowser.Models
{
    public class ImageItem : INotifyPropertyChanged
    {
        public string FilePath { get; }

        private BitmapImage? _thumbnail;
        public BitmapImage? Thumbnail
        {
            get => _thumbnail;
            set
            {
                _thumbnail = value;
                OnPropertyChanged();
            }
        }

        public ImageItem(string path)
        {
            FilePath = path;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}