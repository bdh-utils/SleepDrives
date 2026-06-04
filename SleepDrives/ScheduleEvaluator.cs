using System;
using System.Collections.Generic;

namespace SleepDrives
{
    /// <summary>
    /// Pure decision logic: given a set of schedule windows and the current
    /// time, decide whether the managed drives should be disabled. Kept free of
    /// any UI or Win32 dependency so it can be exhaustively unit tested.
    /// </summary>
    public static class ScheduleEvaluator
    {
        /// <summary>
        /// True when at least one window is active at <paramref name="localNow"/>,
        /// meaning the managed drives should currently be offline.
        /// </summary>
        public static bool ShouldDisable(IEnumerable<ScheduleWindow> windows, DateTime localNow)
        {
            if (windows is null) return false;

            foreach (var window in windows)
            {
                if (window is not null && window.IsActiveAt(localNow))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
