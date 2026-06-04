using System;
using System.Collections.Generic;
using SleepDrives;
using Xunit;

namespace SleepDrives.Tests
{
    public class ScheduleEvaluatorTests
    {
        private static DateTime Monday(int h, int m) => new(2024, 1, 1, h, m, 0);

        [Fact]
        public void NullWindows_ReturnsFalse()
        {
            Assert.False(ScheduleEvaluator.ShouldDisable(null!, Monday(12, 0)));
        }

        [Fact]
        public void EmptyWindows_ReturnsFalse()
        {
            Assert.False(ScheduleEvaluator.ShouldDisable(new List<ScheduleWindow>(), Monday(12, 0)));
        }

        [Fact]
        public void AnyActiveWindow_ReturnsTrue()
        {
            var windows = new List<ScheduleWindow>
            {
                new() { Days = WeekDays.Monday, StartMinutes = 0, EndMinutes = 60 },     // inactive at noon
                new() { Days = WeekDays.Monday, StartMinutes = 11 * 60, EndMinutes = 13 * 60 } // active at noon
            };

            Assert.True(ScheduleEvaluator.ShouldDisable(windows, Monday(12, 0)));
        }

        [Fact]
        public void NoActiveWindow_ReturnsFalse()
        {
            var windows = new List<ScheduleWindow>
            {
                new() { Days = WeekDays.Monday, StartMinutes = 0, EndMinutes = 60 },
                new() { Days = WeekDays.Tuesday, StartMinutes = 11 * 60, EndMinutes = 13 * 60 }
            };

            Assert.False(ScheduleEvaluator.ShouldDisable(windows, Monday(12, 0)));
        }

        [Fact]
        public void ToleratesNullEntries()
        {
            var windows = new List<ScheduleWindow>
            {
                null!,
                new() { Days = WeekDays.Monday, StartMinutes = 11 * 60, EndMinutes = 13 * 60 }
            };

            Assert.True(ScheduleEvaluator.ShouldDisable(windows, Monday(12, 0)));
        }
    }
}
