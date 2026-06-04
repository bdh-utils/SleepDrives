using System.Collections.Generic;
using System.Linq;
using SleepDrives;

namespace SleepDrives.Tests
{
    /// <summary>
    /// In-memory <see cref="IDiskService"/> for testing the scheduling engine
    /// without real hardware. Records every disable/enable request and updates
    /// the disk's reported state so reconciliation can be observed. Disks named
    /// in <see cref="BusyDiskIds"/> refuse to disable (simulating an in-use
    /// volume) without changing state.
    /// </summary>
    public sealed class FakeDiskService : IDiskService
    {
        private readonly List<DiskInfo> _disks;

        public FakeDiskService(params DiskInfo[] disks)
        {
            _disks = disks.ToList();
        }

        /// <summary>Disks that report <see cref="DriveOperationResult.Busy"/> on a clean disable.</summary>
        public HashSet<string> BusyDiskIds { get; } = new();

        /// <summary>Every (diskId, operation) request issued, in order.</summary>
        public List<(string DiskId, DriveOperation Op)> Calls { get; } = new();

        /// <summary>Disk ids that were ultimately disabled via a forced dismount.</summary>
        public HashSet<string> ForcedDisables { get; } = new();

        public IReadOnlyList<DiskInfo> ListDisks() => _disks.ToList();

        public DriveOperationResult Disable(string diskId, bool force)
        {
            int idx = _disks.FindIndex(d => d.DiskId == diskId);
            if (idx < 0) return DriveOperationResult.NotFound;
            if (!_disks[idx].IsManageable) return DriveOperationResult.Protected;
            if (_disks[idx].IsOffline) return DriveOperationResult.NoChangeNeeded;

            Calls.Add((diskId, DriveOperation.Disable));

            // A busy disk only disables when forced (mirrors a service holding
            // a handle that the forced dismount tears down).
            if (BusyDiskIds.Contains(diskId) && !force)
            {
                return DriveOperationResult.Busy; // left online, no state change
            }

            if (force && BusyDiskIds.Contains(diskId))
            {
                ForcedDisables.Add(diskId);
            }

            _disks[idx] = TestDisk.With(_disks[idx], offline: true);
            return DriveOperationResult.Succeeded;
        }

        public DriveOperationResult Enable(string diskId)
        {
            int idx = _disks.FindIndex(d => d.DiskId == diskId);
            if (idx < 0) return DriveOperationResult.NotFound;
            if (!_disks[idx].IsOffline) return DriveOperationResult.NoChangeNeeded;

            Calls.Add((diskId, DriveOperation.Enable));
            _disks[idx] = TestDisk.With(_disks[idx], offline: false);
            return DriveOperationResult.Succeeded;
        }
    }

    /// <summary>Which way a recorded <see cref="FakeDiskService"/> call went.</summary>
    public enum DriveOperation
    {
        Disable,
        Enable
    }

    /// <summary>Factory helpers for building <see cref="DiskInfo"/> fixtures.</summary>
    public static class TestDisk
    {
        public static DiskInfo Make(
            string id,
            uint number = 1,
            bool offline = false,
            bool isBoot = false,
            bool isSystem = false) => new()
            {
                DiskId = id,
                Number = number,
                FriendlyName = $"Test Disk {number}",
                SizeBytes = 1_000_000_000_000,
                IsOffline = offline,
                IsBoot = isBoot,
                IsSystem = isSystem,
                BusType = "SATA"
            };

        public static DiskInfo With(DiskInfo d, bool offline) => new()
        {
            DiskId = d.DiskId,
            Number = d.Number,
            FriendlyName = d.FriendlyName,
            SizeBytes = d.SizeBytes,
            IsOffline = offline,
            IsBoot = d.IsBoot,
            IsSystem = d.IsSystem,
            IsReadOnly = d.IsReadOnly,
            BusType = d.BusType
        };
    }
}
