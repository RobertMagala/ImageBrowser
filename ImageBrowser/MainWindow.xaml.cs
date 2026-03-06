using ImageBrowser.ViewModels;
using Microsoft.WindowsAPICodePack.Dialogs;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;


namespace ImageBrowser
{

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // Handle key presses for zooming in and out
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (DataContext is not MainViewModel vm)
                return;

            if (e.Key == Key.OemPlus || e.Key == Key.Add)
            {
                vm.ThumbnailSize += 50;
                e.Handled = true;
            }
            else if (e.Key == Key.OemMinus || e.Key == Key.Subtract)
            {
                vm.ThumbnailSize -= 50;
                e.Handled = true;
            }
        }

// Menu item click handlers to change background color
private void SetBlackBackground_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
                vm.BackgroundBrush = Brushes.Black;
        }

        private void SetGrayBackground_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
                vm.BackgroundBrush = Brushes.Gray;
        }

        private void SetWhiteBackground_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
                vm.BackgroundBrush = Brushes.White;
        }
        // Menu item click handler to open folder dialog
        private void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new CommonOpenFileDialog
            {
                IsFolderPicker = true,
                Title = "Select Image Folder"
            };

            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                var vm = DataContext as MainViewModel;
                if (vm is { } && !string.IsNullOrWhiteSpace(dialog.FileName))
                {
                    _ = vm.LoadFolder(dialog.FileName);
                }
            }
        }
        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
        private void OpenPreferences_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not MainViewModel vm)
                return;

            var current = ImageBrowser.Services.AppSettings.Load();
            var dlg = new SettingsWindow(current)
            {
                Owner = this
            };

            if (dlg.ShowDialog() == true)
            {
                // reload and apply
                vm.ReloadSettings();
            }
        }
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
        }
    }
}