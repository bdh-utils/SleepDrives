namespace SleepDrives
{
    /// <summary>
    /// Loads and saves <see cref="AppSettings"/>. Behind an interface so the
    /// app can be tested (and run) without touching the real filesystem.
    /// </summary>
    public interface ISettingsStore
    {
        /// <summary>
        /// Return the stored settings, or freshly-defaulted settings if none
        /// are saved or the saved data cannot be read.
        /// </summary>
        AppSettings Load();

        /// <summary>Persist the given settings. Best-effort; never throws.</summary>
        void Save(AppSettings settings);
    }
}
