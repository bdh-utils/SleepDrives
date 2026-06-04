using System.Collections.Generic;

namespace SleepDrives
{
    /// <summary>
    /// User preferences persisted between runs. Defaults here are the
    /// out-of-the-box behaviour when no settings file exists yet — notably the
    /// schedule starts disabled so SleepDrives never touches a disk until the
    /// user has explicitly opted in.
    /// </summary>
    public sealed class AppSettings
    {
        /// <summary>Whether the schedule is actively enforced.</summary>
        public bool Enabled { get; set; } = false;

        /// <summary>Stable ids of the disks the user has chosen to manage.</summary>
        public List<string> ManagedDiskIds { get; set; } = new();

        /// <summary>The periods during which the managed disks are disabled.</summary>
        public List<ScheduleWindow> Windows { get; set; } = new();

        /// <summary>Start hidden in the system tray rather than showing the window.</summary>
        public bool StartMinimised { get; set; } = false;
    }
}
