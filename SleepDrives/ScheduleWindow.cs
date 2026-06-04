using System;

namespace SleepDrives
{
    /// <summary>
    /// A recurring period during which the managed drives should be disabled
    /// (taken offline). Times are minutes since midnight in local time, and a
    /// window may run overnight (when <see cref="EndMinutes"/> is at or before
    /// <see cref="StartMinutes"/> the period wraps into the following day).
    ///
    /// <see cref="Days"/> selects which days the window <em>starts</em> on, so
    /// an overnight window flagged for Friday covers Friday night into Saturday
    /// morning.
    /// </summary>
    public sealed class ScheduleWindow
    {
        /// <summary>Lowest legal minutes-since-midnight value.</summary>
        public const int MinMinutes = 0;

        /// <summary>Highest legal minutes-since-midnight value (23:59).</summary>
        public const int MaxMinutes = 24 * 60 - 1;

        public WeekDays Days { get; set; } = WeekDays.All;

        /// <summary>Start of the disable period, minutes since midnight.</summary>
        public int StartMinutes { get; set; }

        /// <summary>End of the disable period, minutes since midnight.</summary>
        public int EndMinutes { get; set; }

        /// <summary>True when the period crosses midnight into the next day.</summary>
        public bool IsOvernight => EndMinutes <= StartMinutes;

        /// <summary>
        /// Whether this window is active at the given local time. A window with
        /// no days, or a zero-length window (start == end), is never active.
        /// </summary>
        public bool IsActiveAt(DateTime localNow)
        {
            if (Days == WeekDays.None || StartMinutes == EndMinutes)
            {
                return false;
            }

            int nowMinutes = localNow.Hour * 60 + localNow.Minute;

            if (!IsOvernight)
            {
                // Simple same-day window, e.g. 09:00–17:00.
                return Days.Includes(localNow.DayOfWeek)
                    && nowMinutes >= StartMinutes
                    && nowMinutes < EndMinutes;
            }

            // Overnight window, e.g. 22:00–07:00. It is active either late on a
            // start day (after Start) or early on the morning after a start day
            // (before End).
            if (nowMinutes >= StartMinutes && Days.Includes(localNow.DayOfWeek))
            {
                return true;
            }

            if (nowMinutes < EndMinutes && Days.Includes(localNow.AddDays(-1).DayOfWeek))
            {
                return true;
            }

            return false;
        }

        /// <summary>Format minutes-since-midnight as HH:mm.</summary>
        public static string FormatTime(int minutes)
        {
            minutes = Math.Clamp(minutes, MinMinutes, MaxMinutes);
            return $"{minutes / 60:D2}:{minutes % 60:D2}";
        }

        /// <summary>
        /// Parse an HH:mm string to minutes since midnight, returning null when
        /// it is not a valid 24-hour time.
        /// </summary>
        public static int? ParseTime(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;

            var parts = text.Split(':');
            if (parts.Length != 2) return null;

            if (!int.TryParse(parts[0], out int h) || !int.TryParse(parts[1], out int m))
            {
                return null;
            }

            if (h < 0 || h > 23 || m < 0 || m > 59) return null;

            return h * 60 + m;
        }
    }
}
