using System.Collections.Generic;
using System.Linq;
using SleepDrives;

namespace SleepDrives.Tests
{
    /// <summary>
    /// In-memory <see cref="IDiskService"/> for testing the scheduling engine
    /// without real hardware. Records every <see cref="SetOffline"/> call and
    /// updates the disk's reported state so reconciliation can be observed.
    /// </summary>
    public sealed class FakeDiskService : IDiskService
    {
        private readonly List<DiskInfo> _disks;

        public FakeDiskService(params DiskInfo[] disks)
        {
            _disks = disks.ToList();
        }

        /// <summary>Every (diskId, offline) request issued, in order.</summary>
        public List<(string DiskId, bool Offline)> Calls { get; } = new();

        public IReadOnlyList<DiskInfo> ListDisks() => _disks.ToList();

        public bool SetOffline(string diskId, bool offline)
        {
            int idx = _disks.FindIndex(d => d.DiskId == diskId);
            if (idx < 0) return false;
            if (!_disks[idx].IsManageable) return false;

            Calls.Add((diskId, offline));
            _disks[idx] = TestDisk.With(_disks[idx], offline);
            return true;
        }
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
