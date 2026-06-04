using System.Collections.Generic;

namespace SleepDrives
{
    /// <summary>
    /// Politely asks any programs holding files open on a set of volumes to
    /// close them, so a drive can be dismounted gracefully rather than having
    /// open handles torn out from under it. Behind an interface so the disk
    /// service can be exercised without the real Restart Manager.
    /// </summary>
    public interface IDismountNotifier
    {
        /// <summary>
        /// Ask programs using the given volumes to release them. Best-effort:
        /// returns the friendly names of the applications it asked to close
        /// (possibly empty), and never throws.
        /// </summary>
        IReadOnlyList<string> NotifyClosing(IReadOnlyList<string> volumePaths);
    }
}
