using System.Collections.Generic;

namespace SleepDrives
{
    /// <summary>The outcome of a single disable/enable request on a disk.</summary>
    public enum DriveOperationResult
    {
        /// <summary>The disk was disabled or enabled as asked.</summary>
        Succeeded,

        /// <summary>The disk was already in the requested state; nothing to do.</summary>
        NoChangeNeeded,

        /// <summary>
        /// One of the disk's volumes is still in use, so it was deliberately
        /// <em>not</em> taken offline. The drive is left online and the caller
        /// should retry later — this is the safe outcome, never a forced cut.
        /// </summary>
        Busy,

        /// <summary>No disk with the given id is currently attached.</summary>
        NotFound,

        /// <summary>The disk is the boot/system disk and may never be disabled.</summary>
        Protected,

        /// <summary>The operation was attempted but the storage layer rejected it.</summary>
        Failed
    }

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
        /// Take a disk offline: flush and dismount each of its volumes first,
        /// then offline it. When <paramref name="force"/> is false this is the
        /// clean path — if a volume can't be locked because it's in use, the
        /// disk is left online and <see cref="DriveOperationResult.Busy"/> is
        /// returned. When <paramref name="force"/> is true the volumes are
        /// dismounted regardless, so the disk is disabled even while a service
        /// holds it open. Identified by stable <paramref name="diskId"/>.
        /// </summary>
        DriveOperationResult Disable(string diskId, bool force);

        /// <summary>Bring a previously disabled disk back online.</summary>
        DriveOperationResult Enable(string diskId);
    }
}
