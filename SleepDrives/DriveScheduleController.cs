using System;
using System.Collections.Generic;

namespace SleepDrives
{
    /// <summary>The outcome of a single <see cref="DriveScheduleController.Apply"/> pass.</summary>
    public readonly record struct ApplyResult(
        bool Enforced,
        bool ShouldDisable,
        int DisksChanged,
        IReadOnlyList<string> BusyDiskIds)
    {
        /// <summary>Whether any managed disk was left online because it was in use.</summary>
        public bool HasBusyDisks => BusyDiskIds.Count > 0;
    }

    /// <summary>
    /// The WPF-free engine that drives SleepDrives. Given a schedule and the set
    /// of disks the user has chosen to manage, each <see cref="Apply"/> pass
    /// reconciles every managed disk's online/offline state with what the
    /// schedule says it should be right now.
    ///
    /// Reconciliation reads the disks' live state each pass rather than tracking
    /// it internally, so the controller is self-correcting: a disk the user
    /// brought back online manually is re-disabled on the next pass while the
    /// window is still active, and a disk that was too busy to disable is simply
    /// retried next time.
    /// </summary>
    public sealed class DriveScheduleController
    {
        private static readonly IReadOnlyList<string> NoBusyDisks = Array.Empty<string>();

        private readonly IDiskService _diskService;

        public DriveScheduleController(IDiskService diskService)
        {
            _diskService = diskService ?? throw new ArgumentNullException(nameof(diskService));
        }

        /// <summary>
        /// Whether the schedule is actively enforced. When false, <see cref="Apply"/>
        /// leaves every disk exactly as it is — SleepDrives takes no action.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>The disable periods. Drives are offline while any window is active.</summary>
        public IList<ScheduleWindow> Windows { get; } = new List<ScheduleWindow>();

        /// <summary>Stable ids of the disks the user has chosen to manage.</summary>
        public ISet<string> ManagedDiskIds { get; } =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Whether the managed drives should be offline at the given time.</summary>
        public bool ShouldDisableNow(DateTime localNow) =>
            ScheduleEvaluator.ShouldDisable(Windows, localNow);

        /// <summary>
        /// Reconcile every managed disk with the schedule. No-op (and reports
        /// <see cref="ApplyResult.Enforced"/> = false) when <see cref="Enabled"/>
        /// is off. Boot and system disks are skipped even if somehow selected.
        /// Disks that are in use when they should be disabled are reported in
        /// <see cref="ApplyResult.BusyDiskIds"/> and left online for retry.
        /// </summary>
        public ApplyResult Apply(DateTime localNow)
        {
            if (!Enabled)
            {
                return new ApplyResult(Enforced: false, ShouldDisable: false, DisksChanged: 0, NoBusyDisks);
            }

            bool shouldDisable = ShouldDisableNow(localNow);
            int changed = 0;
            List<string>? busy = null;

            foreach (var disk in _diskService.ListDisks())
            {
                if (!disk.IsManageable) continue;
                if (!ManagedDiskIds.Contains(disk.DiskId)) continue;
                if (disk.IsOffline == shouldDisable) continue;

                var result = shouldDisable
                    ? _diskService.Disable(disk.DiskId)
                    : _diskService.Enable(disk.DiskId);

                if (result == DriveOperationResult.Succeeded)
                {
                    changed++;
                }
                else if (result == DriveOperationResult.Busy)
                {
                    (busy ??= new List<string>()).Add(disk.DiskId);
                }
            }

            return new ApplyResult(
                Enforced: true,
                ShouldDisable: shouldDisable,
                DisksChanged: changed,
                busy is null ? NoBusyDisks : busy);
        }
    }
}
