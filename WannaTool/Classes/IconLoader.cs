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
        SlidingExpiration = TimeSpan.FromMinutes(10),
    };

    public static ImageSource? GetIcon(string path, bool isFolder)
    {
        if (string.IsNullOrEmpty(path)) return null;

        var key = (isFolder ? "D:" : "F:") + path.ToLowerInvariant();
        if (_cache.Get(key) is ImageSource imgCached)
            return imgCached;

        IntPtr hIcon = IntPtr.Zero;
        try
        {
            if (isFolder)
            {
                hIcon = GetShellIcon(path, true);
            }
            else
            {
                hIcon = GetShellIcon(path, false);
            }

            if (hIcon != IntPtr.Zero)
            {
                var icon = Imaging.CreateBitmapSourceFromHIcon(
                    hIcon,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
                
                icon.Freeze();
                
                DestroyIcon(hIcon);

                _cache.Set(key, icon, _policy);
                return icon;
            }
        }
        catch { }

        return null;
    }

    private static IntPtr GetShellIcon(string path, bool isFolder)
    {
        const uint SHGFI_ICON = 0x100;
        const uint SHGFI_LARGEICON = 0x0; // 32x32
        const uint SHGFI_USEFILEATTRIBUTES = 0x10;
        
        uint attributes = isFolder ? 0x10u : 0x80u;
        uint flags = SHGFI_ICON | SHGFI_LARGEICON;
        
        string ext = Path.GetExtension(path);
        bool isExeOrLnk = ext.Equals(".exe", StringComparison.OrdinalIgnoreCase) || 
                          ext.Equals(".lnk", StringComparison.OrdinalIgnoreCase) ||
                          ext.Equals(".ico", StringComparison.OrdinalIgnoreCase);

        if (!isExeOrLnk && !isFolder)
        {
            flags |= SHGFI_USEFILEATTRIBUTES;
        }

        SHFILEINFO shfi = new();
        IntPtr result = SHGetFileInfo(
            path,
            attributes,
            out shfi,
            (uint)Marshal.SizeOf<SHFILEINFO>(),
            flags);

        return shfi.hIcon;
    }

    #region P/Invoke
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(
        string pszPath, uint dwFileAttributes,
        out SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

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
