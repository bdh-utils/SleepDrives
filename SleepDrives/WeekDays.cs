using System;

namespace SleepDrives
{
    /// <summary>
    /// The days of the week a <see cref="ScheduleWindow"/> applies to, as a
    /// bit flag so a single window can cover any combination of days.
    /// </summary>
    [Flags]
    public enum WeekDays
    {
        None = 0,
        Monday = 1 << 0,
        Tuesday = 1 << 1,
        Wednesday = 1 << 2,
        Thursday = 1 << 3,
        Friday = 1 << 4,
        Saturday = 1 << 5,
        Sunday = 1 << 6,

        Weekdays = Monday | Tuesday | Wednesday | Thursday | Friday,
        Weekend = Saturday | Sunday,
        All = Weekdays | Weekend
    }

    /// <summary>Helpers for mapping between <see cref="DayOfWeek"/> and <see cref="WeekDays"/>.</summary>
    public static class WeekDaysExtensions
    {
        /// <summary>The single <see cref="WeekDays"/> flag for a calendar day.</summary>
        public static WeekDays ToFlag(this DayOfWeek day) => day switch
        {
            DayOfWeek.Monday => WeekDays.Monday,
            DayOfWeek.Tuesday => WeekDays.Tuesday,
            DayOfWeek.Wednesday => WeekDays.Wednesday,
            DayOfWeek.Thursday => WeekDays.Thursday,
            DayOfWeek.Friday => WeekDays.Friday,
            DayOfWeek.Saturday => WeekDays.Saturday,
            DayOfWeek.Sunday => WeekDays.Sunday,
            _ => WeekDays.None
        };

        /// <summary>Whether the given calendar day is included in the set.</summary>
        public static bool Includes(this WeekDays days, DayOfWeek day) =>
            (days & day.ToFlag()) != 0;
    }
}
