using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace SleepDrives
{
    /// <summary>
    /// Real <see cref="IDismountNotifier"/> backed by the Windows Restart Manager
    /// (Rstrtmgr.dll) — the same machinery installers use to ask apps to release
    /// files before an update. SleepDrives registers the mount paths of the disk's
    /// volumes, discovers which applications have them open, and asks those apps to
    /// close gracefully so the volume can be dismounted cleanly instead of forced.
    ///
    /// Everything is best-effort: any failure leaves the caller to fall back on a
    /// forced dismount, so this can only ever make a dismount gentler, never block it.
    /// </summary>
    public sealed class RestartManagerNotifier : IDismountNotifier
    {
        public IReadOnlyList<string> NotifyClosing(IReadOnlyList<string> volumePaths)
        {
            var mountPaths = ResolveMountPaths(volumePaths);
            if (mountPaths.Count == 0)
            {
                return Array.Empty<string>();
            }

            var key = new StringBuilder(CCH_RM_SESSION_KEY + 1);
            if (RmStartSession(out uint session, 0, key) != ERROR_SUCCESS)
            {
                return Array.Empty<string>();
            }

            try
            {
                if (RmRegisterResources(session, (uint)mountPaths.Count, mountPaths.ToArray(),
                        0, null, 0, null) != ERROR_SUCCESS)
                {
                    return Array.Empty<string>();
                }

                var apps = GetAffectedApps(session);

                // Ask the apps to close. Default flags let them shut down
                // gracefully (save and exit) rather than being killed outright.
                RmShutdown(session, 0, IntPtr.Zero);

                return apps;
            }
            catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
            {
                // Restart Manager unavailable on this SKU: nothing to do.
                return Array.Empty<string>();
            }
            finally
            {
                RmEndSession(session);
            }
        }

        private static List<string> GetAffectedApps(uint session)
        {
            var names = new List<string>();

            uint needed = 0;
            uint count = 0;
            int rc = RmGetList(session, out needed, ref count, null, out _);

            if ((rc == ERROR_SUCCESS && needed == 0) || needed == 0)
            {
                return names;
            }

            if (rc != ERROR_SUCCESS && rc != ERROR_MORE_DATA)
            {
                return names;
            }

            var infos = new RM_PROCESS_INFO[needed];
            count = needed;
            if (RmGetList(session, out needed, ref count, infos, out _) != ERROR_SUCCESS)
            {
                return names;
            }

            for (int i = 0; i < count && i < infos.Length; i++)
            {
                var name = infos[i].strAppName;
                if (!string.IsNullOrWhiteSpace(name))
                {
                    names.Add(name);
                }
            }

            return names;
        }

        /// <summary>
        /// Turn volume GUID paths into the mount paths (drive letters such as
        /// "E:\") Restart Manager understands, falling back to the GUID path.
        /// </summary>
        private static List<string> ResolveMountPaths(IReadOnlyList<string> volumePaths)
        {
            var paths = new List<string>();

            foreach (var volume in volumePaths)
            {
                if (string.IsNullOrWhiteSpace(volume)) continue;

                bool found = false;
                var buffer = new char[1024];

                if (GetVolumePathNamesForVolumeNameW(volume, buffer, (uint)buffer.Length, out _))
                {
                    foreach (var mount in SplitDoubleNullList(buffer))
                    {
                        paths.Add(mount);
                        found = true;
                    }
                }

                if (!found)
                {
                    paths.Add(volume);
                }
            }

            return paths;
        }

        private static IEnumerable<string> SplitDoubleNullList(char[] buffer)
        {
            int start = 0;
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i] != '\0') continue;
                if (i == start) yield break; // empty string => end of the list
                yield return new string(buffer, start, i - start);
                start = i + 1;
            }
        }

        // ---- Restart Manager interop --------------------------------------

        private const int ERROR_SUCCESS = 0;
        private const int ERROR_MORE_DATA = 234;
        private const int CCH_RM_SESSION_KEY = 32;
        private const int CCH_RM_MAX_APP_NAME = 255;
        private const int CCH_RM_MAX_SVC_NAME = 63;

        [StructLayout(LayoutKind.Sequential)]
        private struct RM_UNIQUE_PROCESS
        {
            public int dwProcessId;
            public System.Runtime.InteropServices.ComTypes.FILETIME ProcessStartTime;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct RM_PROCESS_INFO
        {
            public RM_UNIQUE_PROCESS Process;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCH_RM_MAX_APP_NAME + 1)]
            public string strAppName;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCH_RM_MAX_SVC_NAME + 1)]
            public string strServiceShortName;

            public uint ApplicationType;
            public uint AppStatus;
            public uint TSSessionId;

            [MarshalAs(UnmanagedType.Bool)]
            public bool bRestartable;
        }

        [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
        private static extern int RmStartSession(out uint pSessionHandle, int dwSessionFlags, StringBuilder strSessionKey);

        [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
        private static extern int RmRegisterResources(uint dwSessionHandle,
            uint nFiles, string[]? rgsFilenames,
            uint nApplications, RM_UNIQUE_PROCESS[]? rgApplications,
            uint nServices, string[]? rgsServiceNames);

        [DllImport("rstrtmgr.dll")]
        private static extern int RmGetList(uint dwSessionHandle,
            out uint pnProcInfoNeeded, ref uint pnProcInfo,
            [In, Out] RM_PROCESS_INFO[]? rgAffectedApps, out uint lpdwRebootReasons);

        [DllImport("rstrtmgr.dll")]
        private static extern int RmShutdown(uint dwSessionHandle, uint lActionFlags, IntPtr fnStatus);

        [DllImport("rstrtmgr.dll")]
        private static extern int RmEndSession(uint dwSessionHandle);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetVolumePathNamesForVolumeNameW(
            string lpszVolumeName, char[] lpszVolumePathNames, uint cchBufferLength, out uint lpcchReturnLength);
    }
}
