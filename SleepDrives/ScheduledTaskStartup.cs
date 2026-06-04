using System;
using System.Diagnostics;

namespace SleepDrives
{
    /// <summary>
    /// Auto-start backed by a Windows logon task created with schtasks.exe.
    ///
    /// A scheduled task (not the HKCU\...\Run key) is used because SleepDrives
    /// runs elevated: a Run-key entry for an elevated app would either be blocked
    /// or fire a UAC prompt at every sign-in, whereas a task registered with the
    /// highest run level starts the app elevated and silently at logon.
    /// </summary>
    public sealed class ScheduledTaskStartup : IStartupRegistration
    {
        /// <summary>The Task Scheduler task name SleepDrives registers under.</summary>
        public const string TaskName = "SleepDrives-Startup";

        private readonly string _executablePath;

        public ScheduledTaskStartup(string executablePath)
        {
            _executablePath = executablePath ?? throw new ArgumentNullException(nameof(executablePath));
        }

        public bool IsEnabled()
        {
            // /Query returns exit code 0 when the named task exists.
            return RunSchtasks($"/Query /TN \"{TaskName}\"") == 0;
        }

        public void SetEnabled(bool enabled)
        {
            if (enabled)
            {
                if (string.IsNullOrEmpty(_executablePath)) return;

                // /RL HIGHEST runs the task elevated; /F overwrites any stale
                // entry so a moved executable is corrected when re-enabled.
                RunSchtasks(
                    $"/Create /TN \"{TaskName}\" /TR \"\\\"{_executablePath}\\\"\" " +
                    "/SC ONLOGON /RL HIGHEST /F");
            }
            else
            {
                RunSchtasks($"/Delete /TN \"{TaskName}\" /F");
            }
        }

        private static int RunSchtasks(string arguments)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var process = Process.Start(psi);
                if (process is null) return -1;

                process.WaitForExit();
                return process.ExitCode;
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
            {
                return -1;
            }
        }
    }
}
