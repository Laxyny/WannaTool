using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using Microsoft.Toolkit.Uwp.Notifications;
using DataFormats = System.Windows.DataFormats;

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
                var screens = allScreens ? Screen.AllScreens.OrderBy(s => s.Bounds.X).ToArray() : new[] { Screen.PrimaryScreen };
                int totalWidth = screens.Sum(s => s.Bounds.Width);
                int maxHeight = screens.Max(s => s.Bounds.Height);

                using var bmp = new Bitmap(totalWidth, maxHeight);
                using var g = Graphics.FromImage(bmp);

                int offsetX = 0;
                foreach (var screen in screens)
                {
                    g.CopyFromScreen(screen.Bounds.X, screen.Bounds.Y, offsetX, 0, screen.Bounds.Size);
                    offsetX += screen.Bounds.Width;
                }

                // Copier dans le presse-papier (WPF Clipboard, évite la fuite mémoire)
                System.Windows.Clipboard.SetImage(bmp.ToBitmapSource());

                // Enregistrer l'image
                var filePath = SaveScreenshot(bmp);

                // Notification
                new ToastContentBuilder()
                    .AddText("Capture enregistrée")
                    .AddText(allScreens ? "Tous les écrans ont été capturés." : "Écran principal capturé.")
                    .AddArgument("action", "openScreenshot")
                    .AddArgument("path", filePath)
                    .AddButton(new ToastButton()
                        .SetContent("Ouvrir")
                        .AddArgument("action", "openScreenshot")
                        .AddArgument("path", filePath)
                        .SetBackgroundActivation())
                    .Show();
            }
            catch (Exception ex)
            {
                new ToastContentBuilder()
                    .AddText("Erreur lors de la capture d’écran")
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
