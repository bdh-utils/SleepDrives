using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SleepDrives
{
    /// <summary>
    /// Stores settings as JSON under %AppData%\bdh-utils\SleepDrives. Reads and
    /// writes are best-effort: a missing, unreadable, or corrupt file falls
    /// back to defaults, and a failed save is swallowed rather than crashing
    /// the app over a preference.
    /// </summary>
    public sealed class JsonSettingsStore : ISettingsStore
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };

        private readonly string _path;

        /// <param name="path">
        /// File to read/write. Defaults to the per-user AppData location;
        /// tests can pass a temp path.
        /// </param>
        public JsonSettingsStore(string? path = null)
        {
            _path = path ?? DefaultPath();
        }

        /// <summary>The per-user settings file location.</summary>
        public static string DefaultPath()
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "bdh-utils", "SleepDrives");
            return Path.Combine(dir, "settings.json");
        }

        public AppSettings Load()
        {
            try
            {
                if (File.Exists(_path))
                {
                    var json = File.ReadAllText(_path);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json, Options);
                    if (settings != null)
                    {
                        // Tolerate a file written before these collections
                        // existed (null) by normalising to empty lists.
                        settings.ManagedDiskIds ??= new();
                        settings.Windows ??= new();
                        return settings;
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
            {
                // Corrupt or unreadable settings: fall through to defaults.
            }

            return new AppSettings();
        }

        public void Save(AppSettings settings)
        {
            try
            {
                var dir = Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var json = JsonSerializer.Serialize(settings, Options);
                File.WriteAllText(_path, json);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Best-effort persistence: ignore failures.
            }
        }
    }
}
