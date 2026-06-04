using System;
using System.Collections.Generic;

namespace SleepDrives
{
    /// <summary>The outcome of a single <see cref="DriveScheduleController.Apply"/> pass.</summary>
    public readonly record struct ApplyResult(bool Enforced, bool ShouldDisable, int DisksChanged);

    /// <summary>
    /// The WPF-free engine that drives SleepDrives. Given a schedule and the set
    /// of disks the user has chosen to manage, each <see cref="Apply"/> pass
    /// reconciles every managed disk's online/offline state with what the
    /// schedule says it should be right now.
    ///
    /// Reconciliation reads the disks' live state each pass rather than tracking
    /// it internally, so the controller is self-correcting: a disk the user
    /// brought back online manually is re-disabled on the next pass while the
    /// window is still active, and stays untouched once it isn't.
    /// </summary>
    public sealed class DriveScheduleController
    {
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
        /// </summary>
        public ApplyResult Apply(DateTime localNow)
        {
            if (!Enabled)
            {
                return new ApplyResult(Enforced: false, ShouldDisable: false, DisksChanged: 0);
            }

            bool shouldDisable = ShouldDisableNow(localNow);
            int changed = 0;

            foreach (var disk in _diskService.ListDisks())
            {
                if (!disk.IsManageable) continue;
                if (!ManagedDiskIds.Contains(disk.DiskId)) continue;
                if (disk.IsOffline == shouldDisable) continue;

                if (_diskService.SetOffline(disk.DiskId, shouldDisable))
                {
                    changed++;
                }
            }

            return new ApplyResult(Enforced: true, ShouldDisable: shouldDisable, DisksChanged: changed);
        }
    }
}
