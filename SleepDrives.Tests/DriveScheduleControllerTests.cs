using System;
using SleepDrives;
using Xunit;

namespace SleepDrives.Tests
{
    public class DriveScheduleControllerTests
    {
        // 2024-01-01 is a Monday; this window disables Monday 22:00–07:00.
        private static readonly DateTime InsideWindow = new(2024, 1, 1, 23, 0, 0);
        private static readonly DateTime OutsideWindow = new(2024, 1, 1, 12, 0, 0);

        private static ScheduleWindow OvernightMonday() =>
            new() { Days = WeekDays.Monday, StartMinutes = 22 * 60, EndMinutes = 7 * 60 };

        [Fact]
        public void Constructor_NullService_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new DriveScheduleController(null!));
        }

        [Fact]
        public void Apply_WhenDisabled_DoesNothing()
        {
            var fake = new FakeDiskService(TestDisk.Make("d1", offline: false));
            var controller = new DriveScheduleController(fake) { Enabled = false };
            controller.Windows.Add(OvernightMonday());
            controller.ManagedDiskIds.Add("d1");

            var result = controller.Apply(InsideWindow);

            Assert.False(result.Enforced);
            Assert.Empty(fake.Calls);
        }

        [Fact]
        public void Apply_InWindow_DisablesManagedDisk()
        {
            var fake = new FakeDiskService(TestDisk.Make("d1", offline: false));
            var controller = new DriveScheduleController(fake) { Enabled = true };
            controller.Windows.Add(OvernightMonday());
            controller.ManagedDiskIds.Add("d1");

            var result = controller.Apply(InsideWindow);

            Assert.True(result.Enforced);
            Assert.True(result.ShouldDisable);
            Assert.Equal(1, result.DisksChanged);
            Assert.Equal(("d1", true), fake.Calls[0]);
        }

        [Fact]
        public void Apply_OutsideWindow_EnablesManagedDisk()
        {
            var fake = new FakeDiskService(TestDisk.Make("d1", offline: true));
            var controller = new DriveScheduleController(fake) { Enabled = true };
            controller.Windows.Add(OvernightMonday());
            controller.ManagedDiskIds.Add("d1");

            var result = controller.Apply(OutsideWindow);

            Assert.True(result.Enforced);
            Assert.False(result.ShouldDisable);
            Assert.Equal(("d1", false), fake.Calls[0]);
        }

        [Fact]
        public void Apply_DiskAlreadyInDesiredState_IsNoOp()
        {
            // Already offline and inside a disable window: nothing to do.
            var fake = new FakeDiskService(TestDisk.Make("d1", offline: true));
            var controller = new DriveScheduleController(fake) { Enabled = true };
            controller.Windows.Add(OvernightMonday());
            controller.ManagedDiskIds.Add("d1");

            var result = controller.Apply(InsideWindow);

            Assert.Equal(0, result.DisksChanged);
            Assert.Empty(fake.Calls);
        }

        [Fact]
        public void Apply_UnmanagedDisk_IsUntouched()
        {
            var fake = new FakeDiskService(TestDisk.Make("d1", offline: false));
            var controller = new DriveScheduleController(fake) { Enabled = true };
            controller.Windows.Add(OvernightMonday());
            // d1 is NOT added to ManagedDiskIds.

            controller.Apply(InsideWindow);

            Assert.Empty(fake.Calls);
        }

        [Fact]
        public void Apply_ProtectedDisk_IsNeverDisabled()
        {
            // Even if a stale selection names the boot disk, it must be skipped.
            var fake = new FakeDiskService(TestDisk.Make("sys", isBoot: true, isSystem: true));
            var controller = new DriveScheduleController(fake) { Enabled = true };
            controller.Windows.Add(OvernightMonday());
            controller.ManagedDiskIds.Add("sys");

            var result = controller.Apply(InsideWindow);

            Assert.Equal(0, result.DisksChanged);
            Assert.Empty(fake.Calls);
        }

        [Fact]
        public void Apply_IsSelfCorrecting_AcrossPasses()
        {
            var disk = TestDisk.Make("d1", offline: false);
            var fake = new FakeDiskService(disk);
            var controller = new DriveScheduleController(fake) { Enabled = true };
            controller.Windows.Add(OvernightMonday());
            controller.ManagedDiskIds.Add("d1");

            // First pass inside the window disables it.
            controller.Apply(InsideWindow);
            // Second pass inside the window is a no-op (already offline).
            var second = controller.Apply(InsideWindow);
            // A later pass outside the window re-enables it.
            var third = controller.Apply(OutsideWindow);

            Assert.Equal(0, second.DisksChanged);
            Assert.Equal(1, third.DisksChanged);
            Assert.Equal(("d1", true), fake.Calls[0]);
            Assert.Equal(("d1", false), fake.Calls[1]);
        }

        [Fact]
        public void Apply_OnlyManagedDisksAffected_AmongMany()
        {
            var fake = new FakeDiskService(
                TestDisk.Make("d1", number: 1, offline: false),
                TestDisk.Make("d2", number: 2, offline: false),
                TestDisk.Make("d3", number: 3, offline: false));
            var controller = new DriveScheduleController(fake) { Enabled = true };
            controller.Windows.Add(OvernightMonday());
            controller.ManagedDiskIds.Add("d1");
            controller.ManagedDiskIds.Add("d3");

            var result = controller.Apply(InsideWindow);

            Assert.Equal(2, result.DisksChanged);
            Assert.Contains(("d1", true), fake.Calls);
            Assert.Contains(("d3", true), fake.Calls);
            Assert.DoesNotContain(("d2", true), fake.Calls);
        }
    }
}
