using System;
using System.Collections.Generic;
using System.Management;

namespace SleepDrives
{
    /// <summary>
    /// The real <see cref="IDiskService"/>, backed by the Windows Storage
    /// Management WMI provider (MSFT_Disk under root\Microsoft\Windows\Storage).
    /// Disabling a drive maps to <c>Offline()</c> and enabling it to
    /// <c>Online()</c> — the same actions Disk Management performs. These calls
    /// require an elevated process, which the app manifest guarantees.
    /// </summary>
    public sealed class WmiDiskService : IDiskService
    {
        private const string StorageScope = @"\\.\root\Microsoft\Windows\Storage";
        private const string DiskClass = "MSFT_Disk";

        public IReadOnlyList<DiskInfo> ListDisks()
        {
            var disks = new List<DiskInfo>();

            try
            {
                using var searcher = new ManagementObjectSearcher(
                    StorageScope, $"SELECT * FROM {DiskClass}");
                using var results = searcher.Get();

                foreach (ManagementBaseObject mo in results)
                {
                    using (mo)
                    {
                        disks.Add(ToDiskInfo(mo));
                    }
                }
            }
            catch (ManagementException)
            {
                // Storage provider unavailable or query rejected: report nothing
                // rather than crash. The UI surfaces the empty list to the user.
            }

            disks.Sort((a, b) => a.Number.CompareTo(b.Number));
            return disks;
        }

        public bool SetOffline(string diskId, bool offline)
        {
            if (string.IsNullOrEmpty(diskId)) return false;

            try
            {
                using var searcher = new ManagementObjectSearcher(
                    StorageScope, $"SELECT * FROM {DiskClass}");
                using var results = searcher.Get();

                foreach (ManagementBaseObject mo in results)
                {
                    using (mo)
                    {
                        if (mo is not ManagementObject disk) continue;
                        if (!string.Equals(DiskIdOf(mo), diskId, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        // Never touch the boot or system disk, defence in depth
                        // against a stale selection pointing at one.
                        if (GetBool(mo, "IsBoot") || GetBool(mo, "IsSystem"))
                        {
                            return false;
                        }

                        return offline ? Offline(disk) : Online(disk);
                    }
                }
            }
            catch (ManagementException)
            {
                // Fall through to false: the caller retries on the next pass.
            }

            return false;
        }

        private static bool Offline(ManagementObject disk)
        {
            using var outParams = disk.InvokeMethod("Offline", null, null);
            return ReturnedSuccess(outParams);
        }

        private static bool Online(ManagementObject disk)
        {
            using var outParams = disk.InvokeMethod("Online", null, null);
            bool ok = ReturnedSuccess(outParams);

            // Disks that were offline often come back read-only; clear that so
            // the user actually regains write access when the window ends.
            if (ok && GetBool(disk, "IsReadOnly"))
            {
                TryClearReadOnly(disk);
            }

            return ok;
        }

        private static void TryClearReadOnly(ManagementObject disk)
        {
            try
            {
                using var inParams = disk.GetMethodParameters("SetAttributes");
                if (inParams is null) return;
                inParams["IsReadOnly"] = false;
                using var _ = disk.InvokeMethod("SetAttributes", inParams, null);
            }
            catch (ManagementException)
            {
                // Best-effort: leaving it read-only is preferable to throwing.
            }
        }

        private static bool ReturnedSuccess(ManagementBaseObject? outParams)
        {
            if (outParams is null) return false;
            try
            {
                return Convert.ToUInt32(outParams["ReturnValue"]) == 0;
            }
            catch (Exception ex) when (ex is FormatException or InvalidCastException or ManagementException)
            {
                return false;
            }
        }

        private static DiskInfo ToDiskInfo(ManagementBaseObject mo) => new()
        {
            DiskId = DiskIdOf(mo),
            Number = GetUInt32(mo, "Number"),
            FriendlyName = GetString(mo, "FriendlyName") ?? "Disk",
            SizeBytes = GetUInt64(mo, "Size"),
            IsOffline = GetBool(mo, "IsOffline"),
            IsBoot = GetBool(mo, "IsBoot"),
            IsSystem = GetBool(mo, "IsSystem"),
            IsReadOnly = GetBool(mo, "IsReadOnly"),
            BusType = BusTypeName(GetUInt16(mo, "BusType"))
        };

        /// <summary>
        /// A stable id for a disk: serial number where present, otherwise the
        /// provider's UniqueId, otherwise the (volatile) disk number. Used to
        /// remember the user's selection across reboots and reconnections.
        /// </summary>
        private static string DiskIdOf(ManagementBaseObject mo)
        {
            var serial = GetString(mo, "SerialNumber")?.Trim();
            if (!string.IsNullOrEmpty(serial)) return serial;

            var unique = GetString(mo, "UniqueId")?.Trim();
            if (!string.IsNullOrEmpty(unique)) return unique;

            return $"disk-{GetUInt32(mo, "Number")}";
        }

        private static string BusTypeName(ushort busType) => busType switch
        {
            1 => "SCSI",
            2 => "ATAPI",
            3 => "ATA",
            4 => "1394",
            5 => "SSA",
            6 => "Fibre Channel",
            7 => "USB",
            8 => "RAID",
            9 => "iSCSI",
            10 => "SAS",
            11 => "SATA",
            12 => "SD",
            13 => "MMC",
            14 => "Virtual",
            15 => "File-Backed Virtual",
            16 => "Storage Spaces",
            17 => "NVMe",
            _ => "Unknown"
        };

        // ---- Safe property readers ----------------------------------------

        private static string? GetString(ManagementBaseObject mo, string name) =>
            TryGet(mo, name) as string;

        private static bool GetBool(ManagementBaseObject mo, string name) =>
            TryGet(mo, name) is bool b && b;

        private static uint GetUInt32(ManagementBaseObject mo, string name) =>
            SafeConvert(TryGet(mo, name), Convert.ToUInt32, 0u);

        private static ulong GetUInt64(ManagementBaseObject mo, string name) =>
            SafeConvert(TryGet(mo, name), Convert.ToUInt64, 0ul);

        private static ushort GetUInt16(ManagementBaseObject mo, string name) =>
            SafeConvert(TryGet(mo, name), Convert.ToUInt16, (ushort)0);

        private static object? TryGet(ManagementBaseObject mo, string name)
        {
            try
            {
                return mo[name];
            }
            catch (ManagementException)
            {
                return null;
            }
        }

        private static T SafeConvert<T>(object? value, Func<object, T> convert, T fallback)
        {
            if (value is null) return fallback;
            try
            {
                return convert(value);
            }
            catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException)
            {
                return fallback;
            }
        }
    }
}
