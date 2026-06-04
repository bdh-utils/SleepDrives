using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace SleepDrives
{
    /// <summary>
    /// Performs the graceful "safely remove" sequence on the volumes of a
    /// physical disk before it is taken offline: flush the volume's buffers,
    /// lock it (which fails if any file is open), then dismount it. Locking is
    /// the safety gate — if Windows won't grant the lock the volume is in use,
    /// so the caller must leave the disk online rather than force it.
    /// </summary>
    public sealed class VolumeDismounter
    {
        /// <summary>Result of attempting to quiesce one volume.</summary>
        public enum VolumeResult
        {
            /// <summary>Flushed, locked and dismounted cleanly.</summary>
            Dismounted,

            /// <summary>A file is open on the volume; it was left mounted.</summary>
            Busy,

            /// <summary>The volume could not be opened or the call failed.</summary>
            Failed
        }

        /// <summary>
        /// The volume GUID paths (e.g. <c>\\?\Volume{guid}\</c>) hosted on the
        /// given physical disk number, including volumes without a drive letter.
        /// </summary>
        public IReadOnlyList<string> GetVolumesOnDisk(uint diskNumber)
        {
            var volumes = new List<string>();
            var sb = new StringBuilder(260);

            IntPtr find = FindFirstVolumeW(sb, sb.Capacity);
            if (find == InvalidHandle) return volumes;

            try
            {
                do
                {
                    string volume = sb.ToString();
                    if (VolumeLivesOnDisk(volume, diskNumber))
                    {
                        volumes.Add(volume);
                    }
                    sb.Clear();
                    sb.EnsureCapacity(260);
                }
                while (FindNextVolumeW(find, sb, sb.Capacity));
            }
            finally
            {
                FindVolumeClose(find);
            }

            return volumes;
        }

        /// <summary>
        /// Flush and dismount a single volume.
        ///
        /// The volume is always flushed first so cached writes reach the disk.
        /// We then try to lock it — the clean path, which only succeeds when
        /// nothing has the volume open. When <paramref name="force"/> is false a
        /// failed lock returns <see cref="VolumeResult.Busy"/> and the volume is
        /// left mounted. When <paramref name="force"/> is true the volume is
        /// dismounted regardless (invalidating any open handles), so a drive
        /// that is merely held by a background service is still disabled.
        /// </summary>
        public VolumeResult TryDismountVolume(string volumeGuidPath, bool force)
        {
            // CreateFile needs the device path without the trailing backslash.
            string device = volumeGuidPath.TrimEnd('\\');

            using SafeFileHandle handle = CreateFileW(
                device,
                GENERIC_READ | GENERIC_WRITE,
                FILE_SHARE_READ | FILE_SHARE_WRITE,
                IntPtr.Zero,
                OPEN_EXISTING,
                0,
                IntPtr.Zero);

            if (handle.IsInvalid)
            {
                return VolumeResult.Failed;
            }

            // Push any cached writes to the platters before we touch the mount.
            FlushFileBuffers(handle);

            // Try the clean lock first. It fails if anything has the volume open.
            bool locked = DeviceIoControl(handle, FSCTL_LOCK_VOLUME, IntPtr.Zero, 0,
                IntPtr.Zero, 0, out _, IntPtr.Zero);

            if (!locked && !force)
            {
                // In use and not forcing yet: leave it mounted for a later retry.
                return VolumeResult.Busy;
            }

            try
            {
                // FSCTL_DISMOUNT_VOLUME dismounts whether or not we hold the lock;
                // unlocked it forces the dismount, invalidating open handles.
                if (!DeviceIoControl(handle, FSCTL_DISMOUNT_VOLUME, IntPtr.Zero, 0,
                        IntPtr.Zero, 0, out _, IntPtr.Zero))
                {
                    return VolumeResult.Failed;
                }
            }
            finally
            {
                if (locked)
                {
                    // Closing the handle also releases the lock; the volume stays
                    // dismounted until something remounts it.
                    DeviceIoControl(handle, FSCTL_UNLOCK_VOLUME, IntPtr.Zero, 0,
                        IntPtr.Zero, 0, out _, IntPtr.Zero);
                }
            }

            return VolumeResult.Dismounted;
        }

        private bool VolumeLivesOnDisk(string volumeGuidPath, uint diskNumber)
        {
            string device = volumeGuidPath.TrimEnd('\\');

            using SafeFileHandle handle = CreateFileW(
                device,
                0, // query only: no access rights needed for the extents IOCTL
                FILE_SHARE_READ | FILE_SHARE_WRITE,
                IntPtr.Zero,
                OPEN_EXISTING,
                0,
                IntPtr.Zero);

            if (handle.IsInvalid) return false;

            const int bufferSize = 16 * 1024; // room for many extents
            IntPtr buffer = Marshal.AllocHGlobal(bufferSize);
            try
            {
                if (!DeviceIoControl(handle, IOCTL_VOLUME_GET_VOLUME_DISK_EXTENTS,
                        IntPtr.Zero, 0, buffer, bufferSize, out _, IntPtr.Zero))
                {
                    return false;
                }

                int count = Marshal.ReadInt32(buffer, 0);
                for (int i = 0; i < count; i++)
                {
                    // VOLUME_DISK_EXTENTS: DWORD count, then 4 bytes padding for
                    // 8-byte alignment, then DISK_EXTENT[24] entries whose first
                    // field (DWORD DiskNumber) is what we compare.
                    int extentDisk = Marshal.ReadInt32(buffer, 8 + i * 24);
                    if ((uint)extentDisk == diskNumber)
                    {
                        return true;
                    }
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }

            return false;
        }

        // ---- Win32 interop ------------------------------------------------

        private static readonly IntPtr InvalidHandle = new(-1);

        private const uint GENERIC_READ = 0x80000000;
        private const uint GENERIC_WRITE = 0x40000000;
        private const uint FILE_SHARE_READ = 0x00000001;
        private const uint FILE_SHARE_WRITE = 0x00000002;
        private const uint OPEN_EXISTING = 3;

        private const uint FSCTL_LOCK_VOLUME = 0x00090018;
        private const uint FSCTL_UNLOCK_VOLUME = 0x0009001C;
        private const uint FSCTL_DISMOUNT_VOLUME = 0x00090020;
        private const uint IOCTL_VOLUME_GET_VOLUME_DISK_EXTENTS = 0x00560000;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFileHandle CreateFileW(
            string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes,
            uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeviceIoControl(
            SafeFileHandle hDevice, uint dwIoControlCode, IntPtr lpInBuffer, uint nInBufferSize,
            IntPtr lpOutBuffer, uint nOutBufferSize, out uint lpBytesReturned, IntPtr lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool FlushFileBuffers(SafeFileHandle hFile);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr FindFirstVolumeW(StringBuilder lpszVolumeName, int cchBufferLength);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool FindNextVolumeW(IntPtr hFindVolume, StringBuilder lpszVolumeName, int cchBufferLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool FindVolumeClose(IntPtr hFindVolume);
    }
}
