using Microsoft.Data.Sqlite;
using System.IO;

namespace ImageBrowser.Services
{
    public class ThumbnailDatabase
    {
        private readonly string _dbPath;
        private readonly string _connectionString;

        public ThumbnailDatabase(string cacheFolder)
        {
            if (!Directory.Exists(cacheFolder))
                Directory.CreateDirectory(cacheFolder);

            _dbPath = Path.Combine(cacheFolder, "ImageCache.db");
            _connectionString = $"Data Source={_dbPath}";

            Initialize();
        }

        private void Initialize()
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText =
            @"
            CREATE TABLE IF NOT EXISTS Thumbnails (
                Hash TEXT PRIMARY KEY,
                Image BLOB,
                LastModified TEXT
            );
            ";

            command.ExecuteNonQuery();
        }
        public byte[]? GetThumbnail(string hash)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText =
                "SELECT Image FROM Thumbnails WHERE Hash = $hash";
            command.Parameters.AddWithValue("$hash", hash);

            var result = command.ExecuteScalar();

            if (result == null || result == DBNull.Value)
                return null;

            return (byte[])result;
        }
        public void SaveThumbnail(string hash, byte[] imageBytes)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText =
            @"
        INSERT OR REPLACE INTO Thumbnails (Hash, Image, LastModified)
        VALUES ($hash, $image, datetime('now'))
    ";

            command.Parameters.AddWithValue("$hash", hash);
            command.Parameters.AddWithValue("$image", imageBytes);

            command.ExecuteNonQuery();
        }
    }
}