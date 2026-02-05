using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using Microsoft.Toolkit.Uwp.Notifications;
using DataFormats = System.Windows.DataFormats;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace WannaTool
{
    public static class ScreenCaptureManager
    {
        static class NativeClipboard
        {
            public const uint CF_BITMAP = 2;

            [DllImport("user32.dll", SetLastError = true)]
            public static extern bool OpenClipboard(IntPtr hWndNewOwner);

            [DllImport("user32.dll", SetLastError = true)]
            public static extern bool EmptyClipboard();

            [DllImport("user32.dll", SetLastError = true)]
            public static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

            [DllImport("user32.dll", SetLastError = true)]
            public static extern bool CloseClipboard();

            public static void Open() => OpenClipboard(IntPtr.Zero);
            public static void Empty() => EmptyClipboard();
            public static void Close() => CloseClipboard();

            public static void SetData(uint format, IntPtr hMem) =>
                SetClipboardData(format, hMem);
        }

        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        public static void CapturePrimary()
        {
            Capture(false);
        }

        public static void CaptureAll()
        {
            Capture(true);
        }

        private static void Capture(bool allScreens)
        {
            try
            {
                Rectangle bounds;
                
                if (allScreens)
                {
                    int minX = Forms.Screen.AllScreens.Min(s => s.Bounds.X);
                    int minY = Forms.Screen.AllScreens.Min(s => s.Bounds.Y);
                    int maxX = Forms.Screen.AllScreens.Max(s => s.Bounds.Right);
                    int maxY = Forms.Screen.AllScreens.Max(s => s.Bounds.Bottom);
                    bounds = new Rectangle(minX, minY, maxX - minX, maxY - minY);
                }
                else
                {
                    GetCursorPos(out var pt);
                    var screen = Forms.Screen.FromPoint(new Drawing.Point(pt.X, pt.Y));
                    bounds = screen.Bounds;
                }

                using var bmp = new Bitmap(bounds.Width, bounds.Height);
                using var g = Graphics.FromImage(bmp);
                
                g.CopyFromScreen(bounds.Location, Drawing.Point.Empty, bounds.Size);

                System.Windows.Clipboard.SetImage(bmp.ToBitmapSource());

                var filePath = SaveScreenshot(bmp);
                new ToastContentBuilder()
                    .AddText("Screenshot saved")
                    .AddText(allScreens ? "All screens captured." : "Active monitor captured.")
                    .AddArgument("action", "openScreenshot")
                    .AddArgument("path", filePath)
                    .AddButton(new ToastButton()
                        .SetContent("Open")
                        .AddArgument("action", "openScreenshot")
                        .AddArgument("path", filePath)
                        .SetBackgroundActivation())
                    .Show();
            }
            catch (Exception ex)
            {
                new ToastContentBuilder()
                    .AddText("Error capturing screenshot")
                    .AddText(ex.Message)
                    .Show();
            }
        }

        private static string SaveScreenshot(Bitmap bmp)
        {
            string picturesPath = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            string directory = Path.Combine(picturesPath, "WannaTool_Captures");
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            string fileName = $"Capture-WT_{DateTime.Now:yyyyMMdd_HHmmss}.png";
            string filePath = Path.Combine(directory, fileName);

            bmp.Save(filePath, ImageFormat.Png);
            return filePath;
        }

        private static System.Windows.Media.Imaging.BitmapSource ToBitmapSource(this Bitmap source)
        {
            var hBitmap = source.GetHbitmap();
            try
            {
                var bitmapSource = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap,
                    IntPtr.Zero,
                    Int32Rect.Empty,
                    System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
                bitmapSource.Freeze();
                return bitmapSource;
            }
            finally
            {
                DeleteObject(hBitmap);
            }
        }
    }
}
