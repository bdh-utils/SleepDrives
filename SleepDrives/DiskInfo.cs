namespace SleepDrives
{
    /// <summary>
    /// A read-only snapshot of one physical disk as reported by the storage
    /// layer. <see cref="DiskId"/> is a stable identifier (serial number where
    /// available) used to remember the user's selection across reboots, since
    /// the volatile disk <see cref="Number"/> can change between sessions.
    /// </summary>
    public sealed class DiskInfo
    {
        public required string DiskId { get; init; }

        /// <summary>Windows disk number (e.g. 0, 1). May change between boots.</summary>
        public required uint Number { get; init; }

        public string FriendlyName { get; init; } = "Disk";

        public ulong SizeBytes { get; init; }

        /// <summary>The drive currently has no OS access (it is "disabled").</summary>
        public bool IsOffline { get; init; }

        /// <summary>Hosts the active boot files. Never offer such a disk for management.</summary>
        public bool IsBoot { get; init; }

        /// <summary>Hosts the running Windows installation. Never offer such a disk for management.</summary>
        public bool IsSystem { get; init; }

        public bool IsReadOnly { get; init; }

        /// <summary>Human-readable bus, e.g. "USB", "SATA", "NVMe".</summary>
        public string BusType { get; init; } = "Unknown";

        /// <summary>
        /// Whether SleepDrives is allowed to take this disk offline. The boot
        /// and system disks are protected so a schedule can never lock the user
        /// out of their own machine.
        /// </summary>
        public bool IsManageable => !IsBoot && !IsSystem;

        /// <summary>A compact size string such as "931.5 GB".</summary>
        public string SizeText
        {
            get
            {
                const double gb = 1024d * 1024 * 1024;
                const double tb = gb * 1024;
                if (SizeBytes >= tb) return $"{SizeBytes / tb:0.##} TB";
                if (SizeBytes >= gb) return $"{SizeBytes / gb:0.#} GB";
                return $"{SizeBytes / (1024d * 1024):0} MB";
            }
        }
    }
}
