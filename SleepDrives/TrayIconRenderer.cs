using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace SleepDrives
{
    /// <summary>
    /// Draws the system-tray icon in the bdh-utils palette as a stacked-disk
    /// glyph. It is filled with the brand accent (#F15025) while the schedule is
    /// being enforced and the muted grey (#5A5E5A) while it is idle, so the tray
    /// reflects state at a glance.
    /// </summary>
    public static class TrayIconRenderer
    {
        // bdh-utils palette.
        private static readonly Color Accent = Color.FromArgb(0xF1, 0x50, 0x25);
        private static readonly Color Muted = Color.FromArgb(0x5A, 0x5E, 0x5A);
        private static readonly Color Bg = Color.FromArgb(0x19, 0x19, 0x19);

        /// <summary>Canvas size; Windows scales this down for the tray as needed.</summary>
        public const int Size = 32;

        /// <summary>Create a tray icon for the given enforcing state.</summary>
        public static Icon Create(bool active)
        {
            using var bmp = RenderBitmap(active);
            return ToIcon(bmp);
        }

        /// <summary>Render the glyph to a bitmap (shared by the app-icon generator).</summary>
        public static Bitmap RenderBitmap(bool active)
        {
            var bmp = new Bitmap(Size, Size);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            DrawDrive(g, active ? Accent : Muted);
            return bmp;
        }

        private static void DrawDrive(Graphics g, Color colour)
        {
            using var fill = new SolidBrush(colour);
            // Separators are carved in the brand background so the body reads as
            // a stack of platters.
            using var groove = new Pen(Bg, 1.8f) { LineJoin = LineJoin.Round };

            const float left = 6f;
            const float top = 5f;
            const float width = 20f;
            const float ellipseH = 7f;
            const float bodyBottom = 24f;

            // Cylinder: top cap, body, bottom cap.
            g.FillEllipse(fill, left, top, width, ellipseH);
            g.FillRectangle(fill, left, top + ellipseH / 2f, width, bodyBottom - (top + ellipseH));
            g.FillEllipse(fill, left, bodyBottom - ellipseH, width, ellipseH);

            // Platter separators.
            g.DrawArc(groove, left, top + 7f, width, ellipseH, 0, 180);
            g.DrawArc(groove, left, top + 12f, width, ellipseH, 0, 180);

            // Top rim, to keep the cap crisp.
            g.DrawEllipse(groove, left, top, width, ellipseH);
        }

        private static Icon ToIcon(Bitmap bmp)
        {
            // GetHicon allocates an unmanaged HICON that Icon.FromHandle does not
            // own; clone into a self-contained managed icon, then free the HICON.
            IntPtr hicon = bmp.GetHicon();
            try
            {
                using var temp = Icon.FromHandle(hicon);
                return (Icon)temp.Clone();
            }
            finally
            {
                DestroyIcon(hicon);
            }
        }

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyIcon(IntPtr handle);
    }
}
