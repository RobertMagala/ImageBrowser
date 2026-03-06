using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace ImageBrowser.Services
{
    public class AppSettings : INotifyPropertyChanged
    {
        private const string FileName = "settings.ini";

        private string _cacheDirectory =
            Path.Combine(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), "Cache");

        private int _jpegQuality = 70;
        private int _thumbnailSize = 250;
        private string _backgroundColor = "White";
        private int _chunkSize = 50;
        private int _maxConcurrency = 8;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public int ChunkSize
        {
            get => _chunkSize;
            set
            {
                if (_chunkSize != value)
                {
                    _chunkSize = value;
                    OnPropertyChanged();
                }
            }
        }

        public int MaxConcurrency
        {
            get => _maxConcurrency;
            set
            {
                if (_maxConcurrency != value)
                {
                    _maxConcurrency = value;
                    OnPropertyChanged();
                }
            }
        }

        public string CacheDirectory
        {
            get => _cacheDirectory;
            set
            {
                if (_cacheDirectory != value)
                {
                    _cacheDirectory = value;
                    OnPropertyChanged();
                }
            }
        }

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

        public string BackgroundColor
        {
            get => _backgroundColor;
            set
            {
                if (_backgroundColor != value)
                {
                    _backgroundColor = value;
                    OnPropertyChanged();
                }
            }
        }

        private static string GetFilePath()
        {
            // store ini in the same directory as the running program
            var dir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            // ensure directory exists (should normally)
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            return Path.Combine(dir, FileName);
        }

        public static AppSettings Load()
        {
            var path = GetFilePath();
            var s = new AppSettings();

            if (!File.Exists(path))
                return s;

            try
            {
                foreach (var line in File.ReadAllLines(path))
                {
                    var trimmed = line.Trim();
                    if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#"))
                        continue;

                    var idx = trimmed.IndexOf('=');
                    if (idx <= 0)
                        continue;

                    var key = trimmed.Substring(0, idx).Trim();
                    var val = trimmed.Substring(idx + 1).Trim();

                    switch (key)
                    {
                        case "CacheDirectory":
                            s.CacheDirectory = val;
                            break;
                        case "JpegQuality":
                            if (int.TryParse(val, out var q)) s.JpegQuality = q;
                            break;
                        case "ThumbnailSize":
                            if (int.TryParse(val, out var t)) s.ThumbnailSize = t;
                            break;
                        case "BackgroundColor":
                            s.BackgroundColor = val;
                            break;
                        case "ChunkSize":
                            if (int.TryParse(val, out var c)) s.ChunkSize = c;
                            break;
                        case "MaxConcurrency":
                            if (int.TryParse(val, out var m)) s.MaxConcurrency = m;
                            break;
                    }
                }
            }
            catch
            {
                // ignore parse errors and return defaults
            }

            return s;
        }

        public void Save()
        {
            var path = GetFilePath();
            var lines = new[]
            {
                $"CacheDirectory={CacheDirectory}",
                $"JpegQuality={JpegQuality}",
                $"ThumbnailSize={ThumbnailSize}",
                $"BackgroundColor={BackgroundColor}",
                $"ChunkSize={ChunkSize}",
                $"MaxConcurrency={MaxConcurrency}"
            };

            File.WriteAllLines(path, lines);
        }
    }
}
