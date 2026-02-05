using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Interop;

static class IconLoader
{
    private const int MaxCacheCount = 32;
    private const int DisplaySize = 24;

    private static readonly LinkedList<string> _order = new();
    private static readonly Dictionary<string, (ImageSource Image, LinkedListNode<string> Node)> _cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object _lock = new();

    public static int GetCacheCount()
    {
        lock (_lock) return _cache.Count;
    }

    public static void TrimToWatermark(int maxCount)
    {
        lock (_lock)
        {
            while (_cache.Count > maxCount && _order.First != null)
            {
                var key = _order.First.Value;
                _order.RemoveFirst();
                _cache.Remove(key);
            }
        }
    }

    public static ImageSource? GetIcon(string path, bool isFolder)
    {
        if (string.IsNullOrEmpty(path)) return null;

        var key = (isFolder ? "D:" : "F:") + path.ToLowerInvariant();

        lock (_lock)
        {
            if (_cache.TryGetValue(key, out var entry))
            {
                _order.Remove(entry.Node);
                _order.AddLast(entry.Node);
                return entry.Image;
            }
        }

        IntPtr hIcon = IntPtr.Zero;
        try
        {
            hIcon = GetShellIcon(path, isFolder);
            if (hIcon == IntPtr.Zero) return null;

            var icon = Imaging.CreateBitmapSourceFromHIcon(
                hIcon,
                Int32Rect.Empty,
                BitmapSizeOptions.FromWidthAndHeight(DisplaySize, DisplaySize));
            DestroyIcon(hIcon);
            icon.Freeze();

            lock (_lock)
            {
                if (_cache.TryGetValue(key, out var existing))
                {
                    _order.Remove(existing.Node);
                    _order.AddLast(existing.Node);
                    _cache[key] = (icon, existing.Node);
                }
                else
                {
                    while (_cache.Count >= MaxCacheCount && _order.First != null)
                    {
                        var evict = _order.First.Value;
                        _order.RemoveFirst();
                        _cache.Remove(evict);
                    }
                    var node = _order.AddLast(key);
                    _cache[key] = (icon, node);
                }
            }
            return icon;
        }
        catch { }

        return null;
    }

    private static IntPtr GetShellIcon(string path, bool isFolder)
    {
        const uint SHGFI_ICON = 0x100;
        const uint SHGFI_LARGEICON = 0x0;
        const uint SHGFI_USEFILEATTRIBUTES = 0x10;

        uint attributes = isFolder ? 0x10u : 0x80u;
        uint flags = SHGFI_ICON | SHGFI_LARGEICON;

        string ext = Path.GetExtension(path);
        bool isExeOrLnk = ext.Equals(".exe", StringComparison.OrdinalIgnoreCase) ||
                          ext.Equals(".lnk", StringComparison.OrdinalIgnoreCase) ||
                          ext.Equals(".ico", StringComparison.OrdinalIgnoreCase);

        if (!isExeOrLnk && !isFolder)
            flags |= SHGFI_USEFILEATTRIBUTES;

        SHFILEINFO shfi = new();
        SHGetFileInfo(path, attributes, out shfi, (uint)Marshal.SizeOf<SHFILEINFO>(), flags);
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
