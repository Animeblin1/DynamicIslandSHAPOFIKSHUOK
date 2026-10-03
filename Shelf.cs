using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace DynamicIsland;

/// <summary>
/// Files put down on the island to be carried somewhere else. Only where they are is kept, not the files themselves,
/// and that is remembered between runs; each comes with a picture: what is in it when the system can show that
/// (photos, videos, documents), its icon otherwise.
/// </summary>
sealed class Shelf
{
    const int Thumb = 112, Glyph = 96; // px asked of the shell: twice the tile, so it stays sharp on a scaled screen

    public sealed class Item(string path)
    {
        public string Path { get; } = path;
        public string Name { get; } = System.IO.Path.GetFileName(path.TrimEnd('\\')) is { Length: > 0 } name ? name : path;
        public ImageSource? Picture { get; internal set; }
        /// <summary>The picture shows what is in the file and fills its tile; an icon sits in the middle of it.</summary>
        public bool Photo { get; internal set; }
    }

    readonly Dispatcher _ui;
    readonly List<Item> _items = new();

    public Shelf(Dispatcher ui)
    {
        _ui = ui;
        // whatever has gone from where it lay since the last run is not on the shelf any more
        Put(Settings.Shelf.Where(Exists));
    }

    public IReadOnlyList<Item> Items => _items;

    /// <summary>Something was put down or taken away.</summary>
    public event Action? Changed;

    /// <summary>The picture of an item has come; it is drawn off the UI thread, after the item is already shown.</summary>
    public event Action<Item>? Pictured;

    /// <returns>Whether any of them is new to the shelf.</returns>
    public bool Add(IEnumerable<string> paths)
    {
        if (Put(paths.Where(Exists)).Count == 0) return false;
        Save();
        return true;
    }

    public void Remove(Item item)
    {
        if (!_items.Remove(item)) return;
        Save();
    }

    public void Clear()
    {
        if (_items.Count == 0) return;
        _items.Clear();
        Save();
    }

    static bool Exists(string path) => File.Exists(path) || Directory.Exists(path);

    List<Item> Put(IEnumerable<string> paths)
    {
        var added = new List<Item>();
        foreach (string path in paths)
        {
            // the same file twice is still one thing to carry
            if (_items.Any(i => string.Equals(i.Path, path, StringComparison.OrdinalIgnoreCase))) continue;
            var item = new Item(path);
            _items.Add(item);
            added.Add(item);
        }
        if (added.Count > 0) Draw(added);
        return added;
    }

    void Save()
    {
        Settings.Shelf = _items.Select(i => i.Path).ToArray();
        Changed?.Invoke();
    }

    // thumbnails can take a while (a video has to be opened): a thread of their own, the kind shell extensions expect
    void Draw(List<Item> items)
    {
        var thread = new Thread(() =>
        {
            foreach (Item item in items)
            {
                BitmapSource? picture = Picture(item.Path, Thumb, ThumbnailOnly);
                // a folder's "thumbnail" is its icon all the same: only a picture that reaches its corners fills the tile
                bool photo = picture != null && Opaque(picture);
                if (!photo) picture = Picture(item.Path, Glyph, IconOnly);
                if (picture == null) continue;
                _ui.InvokeAsync(() =>
                {
                    item.Picture = picture;
                    item.Photo = photo;
                    Pictured?.Invoke(item);
                });
            }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
    }

    static bool Opaque(BitmapSource picture)
    {
        int w = picture.PixelWidth, h = picture.PixelHeight;
        var pixel = new byte[4];
        foreach (var (x, y) in new[] { (0, 0), (w - 1, 0), (0, h - 1), (w - 1, h - 1) })
        {
            picture.CopyPixels(new Int32Rect(x, y, 1, 1), pixel, 4, 0);
            if (pixel[3] < 0xFF) return false;
        }
        return true;
    }

    // ───────────────────────── shell ─────────────────────────

    const int ThumbnailOnly = 0x8, IconOnly = 0x4; // SIIGBF_*

    [StructLayout(LayoutKind.Sequential)]
    struct SIZE { public int cx, cy; }

    [StructLayout(LayoutKind.Sequential)]
    struct BITMAP
    {
        public int bmType, bmWidth, bmHeight, bmWidthBytes;
        public ushort bmPlanes, bmBitsPixel;
        public IntPtr bmBits;
    }

    [ComImport, Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IShellItemImageFactory
    {
        [PreserveSig] int GetImage(SIZE size, int flags, out IntPtr bitmap);
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    static extern int SHCreateItemFromParsingName(string path, IntPtr context, [MarshalAs(UnmanagedType.LPStruct)] Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory factory);

    [DllImport("gdi32.dll")] static extern int GetObject(IntPtr handle, int size, out BITMAP bitmap);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr handle);

    /// <summary>What the shell would show for the file at about <paramref name="size"/> px; null when it has nothing of the kind.</summary>
    static BitmapSource? Picture(string path, int size, int flags)
    {
        IntPtr bitmap = IntPtr.Zero;
        try
        {
            if (SHCreateItemFromParsingName(path, IntPtr.Zero, typeof(IShellItemImageFactory).GUID, out var factory) != 0) return null;
            int result = factory.GetImage(new SIZE { cx = size, cy = size }, flags, out bitmap);
            Marshal.ReleaseComObject(factory);
            if (result != 0 || bitmap == IntPtr.Zero) return null;

            // the shell hands over a 32-bit section with its alpha premultiplied, bottom row first
            if (GetObject(bitmap, Marshal.SizeOf<BITMAP>(), out BITMAP info) == 0 || info.bmBits == IntPtr.Zero || info.bmBitsPixel != 32)
                return null;
            int stride = info.bmWidthBytes, height = Math.Abs(info.bmHeight);
            var source = BitmapSource.Create(info.bmWidth, height, 96, 96, PixelFormats.Pbgra32, null, info.bmBits, stride * height, stride);
            BitmapSource upright = info.bmHeight > 0 ? new TransformedBitmap(source, new ScaleTransform(1, -1)) : source;
            // copied out of the section before it is let go, and made shareable with the UI thread
            var copy = new WriteableBitmap(upright);
            copy.Freeze();
            return copy;
        }
        catch (Exception ex) when (ex is COMException or ArgumentException or InvalidCastException)
        {
            return null;
        }
        finally
        {
            if (bitmap != IntPtr.Zero) DeleteObject(bitmap);
        }
    }
}
