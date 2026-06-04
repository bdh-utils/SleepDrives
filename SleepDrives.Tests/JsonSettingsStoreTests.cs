using System;
using System.IO;
using SleepDrives;
using Xunit;

namespace SleepDrives.Tests
{
    public class JsonSettingsStoreTests : IDisposable
    {
        private readonly string _path = Path.Combine(
            Path.GetTempPath(), $"sleepdrives-test-{Guid.NewGuid():N}.json");

        public void Dispose()
        {
            if (File.Exists(_path)) File.Delete(_path);
        }

        [Fact]
        public void Load_WhenMissing_ReturnsDefaults()
        {
            var store = new JsonSettingsStore(_path);

            var settings = store.Load();

            Assert.False(settings.Enabled);
            Assert.Empty(settings.ManagedDiskIds);
            Assert.Empty(settings.Windows);
            Assert.False(settings.StartMinimised);
        }

        [Fact]
        public void SaveThenLoad_RoundTrips()
        {
            var store = new JsonSettingsStore(_path);
            var original = new AppSettings
            {
                Enabled = true,
                StartMinimised = true,
                ManagedDiskIds = { "serial-123", "serial-456" },
                Windows =
                {
                    new ScheduleWindow { Days = WeekDays.Friday | WeekDays.Saturday, StartMinutes = 1320, EndMinutes = 420 }
                }
            };

            store.Save(original);
            var loaded = store.Load();

            Assert.True(loaded.Enabled);
            Assert.True(loaded.StartMinimised);
            Assert.Equal(new[] { "serial-123", "serial-456" }, loaded.ManagedDiskIds);
            var window = Assert.Single(loaded.Windows);
            Assert.Equal(WeekDays.Friday | WeekDays.Saturday, window.Days);
            Assert.Equal(1320, window.StartMinutes);
            Assert.Equal(420, window.EndMinutes);
        }

        [Fact]
        public void Load_WhenCorrupt_ReturnsDefaults()
        {
            File.WriteAllText(_path, "{ this is not valid json");
            var store = new JsonSettingsStore(_path);

            var settings = store.Load();

            Assert.False(settings.Enabled);
            Assert.Empty(settings.Windows);
        }

        [Fact]
        public void DaysSerialiseAsNames_NotNumbers()
        {
            var store = new JsonSettingsStore(_path);
            store.Save(new AppSettings
            {
                Windows = { new ScheduleWindow { Days = WeekDays.Monday, StartMinutes = 60, EndMinutes = 120 } }
            });

            var json = File.ReadAllText(_path);

            // The string enum converter should emit a readable flag name.
            Assert.Contains("Monday", json);
        }
    }
}
