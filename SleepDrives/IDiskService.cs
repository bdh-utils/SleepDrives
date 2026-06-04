using System.Collections.Generic;

namespace SleepDrives
{
    /// <summary>
    /// Abstraction over the platform storage layer. Behind an interface so the
    /// scheduling engine in <see cref="DriveScheduleController"/> can be unit
    /// tested without touching real hardware.
    /// </summary>
    public interface IDiskService
    {
        /// <summary>Enumerate the physical disks currently attached.</summary>
        IReadOnlyList<DiskInfo> ListDisks();

        /// <summary>
        /// Take a disk offline (<paramref name="offline"/> true) or bring it
        /// back online (false). Identified by stable <paramref name="diskId"/>
        /// so the operation survives disk-number reshuffles between sessions.
        /// Returns true if the disk was found and the call was issued.
        /// </summary>
        bool SetOffline(string diskId, bool offline);
    }
}
