using System;
using System.Runtime.Caching;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;
using System.Runtime.InteropServices;

static class IconLoader
{
    private static readonly MemoryCache _cache = new MemoryCache("IconCache");
    private static readonly CacheItemPolicy _policy = new CacheItemPolicy
    {
        SlidingExpiration = TimeSpan.FromMinutes(5),
    };

    public static ImageSource? GetIcon(string path, bool isFolder)
    {
        var key = (isFolder ? "D:" : "F:") + path.ToLowerInvariant();
        if (_cache.Get(key) is ImageSource imgCached)
            return imgCached;

        const uint SHGFI_ICON = 0x100;
        const uint SHGFI_SMALLICON = 0x1;
        const uint SHGFI_USEFILEATTRIBUTES = 0x10;
        const uint FILE_ATTRIBUTE_NORMAL = 0x80;
        const uint FILE_ATTRIBUTE_DIRECTORY = 0x10;

        var flags = SHGFI_ICON | SHGFI_SMALLICON | SHGFI_USEFILEATTRIBUTES;
        var attributes = isFolder
                           ? FILE_ATTRIBUTE_DIRECTORY
                           : FILE_ATTRIBUTE_NORMAL;

        SHFILEINFO shfi = new();
        IntPtr result = SHGetFileInfo(
            path,
            attributes,
            out shfi,
            (uint)Marshal.SizeOf<SHFILEINFO>(),
            flags);

        if (shfi.hIcon == IntPtr.Zero)
            return null;

        var icon = Imaging.CreateBitmapSourceFromHIcon(
            shfi.hIcon,
            Int32Rect.Empty,
            BitmapSizeOptions.FromEmptyOptions());
        DestroyIcon(shfi.hIcon);

        _cache.Add(key, icon, _policy);
        return icon;
    }

    #region P/Invoke
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(
        string pszPath, uint dwFileAttributes,
        out SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

    private static IntPtr SHGetFileIcon(string path, bool isFolder)
    {
        const uint SHGFI_ICON = 0x000000100;
        const uint SHGFI_SMALLICON = 0x000000001;
        const uint FILE_ATTRIBUTE_DIRECTORY = 0x10;

        SHFILEINFO shfi;
        SHGetFileInfo(path,
            isFolder ? FILE_ATTRIBUTE_DIRECTORY : 0,
            out shfi, (uint)Marshal.SizeOf<SHFILEINFO>(),
            SHGFI_ICON | SHGFI_SMALLICON);
        return shfi.hIcon;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }
    #endregion
}
