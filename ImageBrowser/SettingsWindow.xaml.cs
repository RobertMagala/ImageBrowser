using ImageBrowser.Services;
using Microsoft.WindowsAPICodePack.Dialogs;
using System.Windows;

namespace ImageBrowser
{
    public partial class SettingsWindow : Window
    {
        private readonly AppSettings _working;
        private readonly AppSettings _original;

        public SettingsWindow(AppSettings current)
        {
            InitializeComponent();

            _original = current;

            // work on a copy so cancel works
            _working = new AppSettings
            {
                CacheDirectory = current.CacheDirectory,
                JpegQuality = current.JpegQuality,
                ThumbnailSize = current.ThumbnailSize,
                BackgroundColor = current.BackgroundColor,
                ChunkSize = current.ChunkSize,
                MaxConcurrency = current.MaxConcurrency
            };

            DataContext = _working;
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new CommonOpenFileDialog
            {
                IsFolderPicker = true,
                Title = "Select cache folder"
            };

            if (dlg.ShowDialog() == CommonFileDialogResult.Ok)
            {
                _working.CacheDirectory = dlg.FileName;
            }
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            // copy back to original and save
            _original.CacheDirectory = _working.CacheDirectory;
            _original.JpegQuality = _working.JpegQuality;
            _original.ThumbnailSize = _working.ThumbnailSize;
            _original.BackgroundColor = _working.BackgroundColor;
            _original.ChunkSize = _working.ChunkSize;
            _original.MaxConcurrency = _working.MaxConcurrency;
            _original.Save();
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
