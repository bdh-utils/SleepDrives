using System;
using SleepDrives;
using Xunit;

namespace SleepDrives.Tests
{
    public class ScheduleWindowTests
    {
        // Anchor dates with known weekdays (2024-01-01 was a Monday).
        private static DateTime Monday(int h, int m) => new(2024, 1, 1, h, m, 0);
        private static DateTime Tuesday(int h, int m) => new(2024, 1, 2, h, m, 0);
        private static DateTime Friday(int h, int m) => new(2024, 1, 5, h, m, 0);
        private static DateTime Saturday(int h, int m) => new(2024, 1, 6, h, m, 0);

        private static ScheduleWindow Window(WeekDays days, int startMin, int endMin) =>
            new() { Days = days, StartMinutes = startMin, EndMinutes = endMin };

        // ---- Same-day windows ---------------------------------------------

        [Fact]
        public void SameDay_InsideWindow_IsActive()
        {
            var w = Window(WeekDays.Monday, 9 * 60, 17 * 60);
            Assert.True(w.IsActiveAt(Monday(12, 0)));
        }

        [Fact]
        public void SameDay_StartIsInclusive()
        {
            var w = Window(WeekDays.Monday, 9 * 60, 17 * 60);
            Assert.True(w.IsActiveAt(Monday(9, 0)));
        }

        [Fact]
        public void SameDay_EndIsExclusive()
        {
            var w = Window(WeekDays.Monday, 9 * 60, 17 * 60);
            Assert.False(w.IsActiveAt(Monday(17, 0)));
        }

        [Fact]
        public void SameDay_BeforeWindow_IsInactive()
        {
            var w = Window(WeekDays.Monday, 9 * 60, 17 * 60);
            Assert.False(w.IsActiveAt(Monday(8, 59)));
        }

        [Fact]
        public void SameDay_WrongDay_IsInactive()
        {
            var w = Window(WeekDays.Monday, 9 * 60, 17 * 60);
            Assert.False(w.IsActiveAt(Tuesday(12, 0)));
        }

        // ---- Overnight windows --------------------------------------------

        [Fact]
        public void Overnight_LateOnStartDay_IsActive()
        {
            var w = Window(WeekDays.Friday, 22 * 60, 7 * 60);
            Assert.True(w.IsActiveAt(Friday(23, 30)));
        }

        [Fact]
        public void Overnight_EarlyNextMorning_IsActive()
        {
            // A Friday window runs into Saturday morning.
            var w = Window(WeekDays.Friday, 22 * 60, 7 * 60);
            Assert.True(w.IsActiveAt(Saturday(6, 0)));
        }

        [Fact]
        public void Overnight_EndIsExclusiveNextMorning()
        {
            var w = Window(WeekDays.Friday, 22 * 60, 7 * 60);
            Assert.False(w.IsActiveAt(Saturday(7, 0)));
        }

        [Fact]
        public void Overnight_MiddayIsInactive()
        {
            var w = Window(WeekDays.Friday, 22 * 60, 7 * 60);
            Assert.False(w.IsActiveAt(Friday(12, 0)));
        }

        [Fact]
        public void Overnight_SaturdayEvening_NotCoveredByFridayFlag()
        {
            // Saturday night belongs to Saturday's flag, which isn't set.
            var w = Window(WeekDays.Friday, 22 * 60, 7 * 60);
            Assert.False(w.IsActiveAt(Saturday(23, 0)));
        }

        // ---- Degenerate windows -------------------------------------------

        [Fact]
        public void NoDays_IsNeverActive()
        {
            var w = Window(WeekDays.None, 9 * 60, 17 * 60);
            Assert.False(w.IsActiveAt(Monday(12, 0)));
        }

        [Fact]
        public void ZeroLength_IsNeverActive()
        {
            var w = Window(WeekDays.All, 9 * 60, 9 * 60);
            Assert.False(w.IsActiveAt(Monday(9, 0)));
        }

        [Fact]
        public void Overnight_IsOvernightFlag_IsTrue()
        {
            Assert.True(Window(WeekDays.All, 22 * 60, 7 * 60).IsOvernight);
            Assert.False(Window(WeekDays.All, 9 * 60, 17 * 60).IsOvernight);
        }

        // ---- Time parsing / formatting ------------------------------------

        [Theory]
        [InlineData("00:00", 0)]
        [InlineData("07:30", 450)]
        [InlineData("23:59", 1439)]
        public void ParseTime_ValidValues(string text, int expected)
        {
            Assert.Equal(expected, ScheduleWindow.ParseTime(text));
        }

        [Theory]
        [InlineData("24:00")]
        [InlineData("12:60")]
        [InlineData("9")]
        [InlineData("ab:cd")]
        [InlineData("")]
        [InlineData(null)]
        public void ParseTime_InvalidValues_ReturnNull(string? text)
        {
            Assert.Null(ScheduleWindow.ParseTime(text));
        }

        [Theory]
        [InlineData(0, "00:00")]
        [InlineData(450, "07:30")]
        [InlineData(1439, "23:59")]
        public void FormatTime_FormatsAsHourMinute(int minutes, string expected)
        {
            Assert.Equal(expected, ScheduleWindow.FormatTime(minutes));
        }
    }
}
