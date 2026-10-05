using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Livy.Helpers;

/// <summary>
/// Extracts high-quality video thumbnails using Windows Shell IShellItemImageFactory.
/// </summary>
internal static class ThumbnailHelper
{
    private static readonly Guid IShellItemImageFactoryGuid = new("bcc18b79-ba16-442f-80c4-8a59c30c463b");

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int SHCreateItemFromParsingName(
        [MarshalAs(UnmanagedType.LPWStr)] string pszPath,
        IntPtr pbc,
        [In] ref Guid riid,
        [Out, MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory ppv);

    [ComImport]
    [Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory
    {
        [PreserveSig]
        int GetImage(
            [In, MarshalAs(UnmanagedType.Struct)] SIZE size,
            [In] int flags,
            [Out] out IntPtr phbm);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SIZE
    {
        public int cx;
        public int cy;
    }

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr hObject);

    private const int SIIGBF_RESIZETOFIT = 0x00000000;

    private static readonly Dictionary<string, ImageSource> _cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets a frozen BitmapSource thumbnail for a media file, or null if generation fails.
    /// Uses 640x360 resolution to maintain crystal-sharp display without jagged edges or pixelation.
    /// </summary>
    public static ImageSource? GetThumbnail(string filePath, int width = 640, int height = 360)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return null;

        lock (_cache)
        {
            if (_cache.TryGetValue(filePath, out var cached))
                return cached;
        }

        IntPtr hBitmap = IntPtr.Zero;
        IShellItemImageFactory? factory = null;
        try
        {
            var guid = IShellItemImageFactoryGuid;
            int hr = SHCreateItemFromParsingName(filePath, IntPtr.Zero, ref guid, out factory);
            if (hr != 0 || factory == null)
            {
                return null;
            }

            var size = new SIZE { cx = width, cy = height };
            hr = factory.GetImage(size, SIIGBF_RESIZETOFIT, out hBitmap);
            if (hr != 0 || hBitmap == IntPtr.Zero)
            {
                return null;
            }

            var bitmapSource = Imaging.CreateBitmapSourceFromHBitmap(
                hBitmap,
                IntPtr.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());

            bitmapSource.Freeze(); // Freeze for cross-thread access

            lock (_cache)
            {
                _cache[filePath] = bitmapSource;
            }

            return bitmapSource;
        }
        catch
        {
            return null;
        }
        finally
        {
            if (hBitmap != IntPtr.Zero)
            {
                DeleteObject(hBitmap);
            }
            if (factory != null && Marshal.IsComObject(factory))
            {
                try { Marshal.ReleaseComObject(factory); } catch { }
            }
        }
    }

    /// <summary>
    /// Removes a cached thumbnail when a wallpaper is deleted to free memory.
    /// </summary>
    public static void InvalidateCache(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return;
        lock (_cache)
        {
            _cache.Remove(filePath);
        }
    }

    /// <summary>
    /// Clears all cached thumbnails.
    /// </summary>
    public static void ClearCache()
    {
        lock (_cache)
        {
            _cache.Clear();
        }
    }
}
