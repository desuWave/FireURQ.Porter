using System;
using System.IO;
using System.Runtime.InteropServices;

namespace FireURQ.Runtime;

internal sealed class GifFrames : IDisposable
{
    private const int PixelFormat32bppArgb = 0x0026200A;
    private const int FrameDelayTag = 0x5100;
    private const int PropertyNotFound = 19;
    private static readonly Guid TimeDimension = new("6AEDBD6D-3FB5-418A-83A6-7F45229DC872");
    private static readonly Guid GifFormat = new("B96B3CB0-0728-11D3-9D7B-0000F81EF32E");

    private IntPtr token;
    private IntPtr stream;
    private IntPtr image;
    private int[] delays = Array.Empty<int>();

    public GifFrames(Stream source)
    {
        using var data = new MemoryStream();
        source.CopyTo(data);
        byte[] bytes = data.ToArray();

        var input = new StartupInput { Version = 1 };
        Check(GdiplusStartup(out token, in input, IntPtr.Zero));
        try
        {
            stream = SHCreateMemStream(bytes, checked((uint)bytes.Length));
            if (stream == IntPtr.Zero)
                throw new InvalidOperationException("Could not create the GIF decoder stream.");

            Check(GdipLoadImageFromStream(stream, out image));
            Check(GdipGetImageRawFormat(image, out Guid format));
            if (format != GifFormat)
                throw new InvalidDataException("The resource is not a GIF image.");

            Check(GdipGetImageWidth(image, out uint width));
            Check(GdipGetImageHeight(image, out uint height));
            Width = checked((int)width);
            Height = checked((int)height);
            Check(GdipImageGetFrameCount(image, in TimeDimension, out uint count));
            delays = ReadDelays(checked((int)count));
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public int Width { get; }
    public int Height { get; }
    public int Count => delays.Length;

    public int DelayMilliseconds(int index) => delays[index];

    public unsafe int[] DecodeArgb(int index)
    {
        ObjectDisposedException.ThrowIf(image == IntPtr.Zero, this);
        if ((uint)index >= (uint)Count)
            throw new ArgumentOutOfRangeException(nameof(index));

        Check(GdipImageSelectActiveFrame(image, in TimeDimension, (uint)index));
        var pixels = new int[checked(Width * Height)];
        fixed (int* pixelsAddress = pixels)
        {
            Check(GdipCreateBitmapFromScan0(Width, Height, checked(Width * sizeof(int)),
                PixelFormat32bppArgb, (IntPtr)pixelsAddress, out IntPtr bitmap));
            try
            {
                Check(GdipGetImageGraphicsContext(bitmap, out IntPtr graphics));
                try
                {
                    Check(GdipDrawImageRectI(graphics, image, 0, 0, Width, Height));
                }
                finally { GdipDeleteGraphics(graphics); }
            }
            finally { GdipDisposeImage(bitmap); }
        }

        return pixels;
    }

    private int[] ReadDelays(int count)
    {
        var result = new int[count];
        int status = GdipGetPropertyItemSize(image, FrameDelayTag, out uint size);
        if (status == PropertyNotFound)
            return result;

        Check(status);
        if (size < Marshal.SizeOf<PropertyItem>())
            throw new InvalidDataException("Invalid GIF frame-delay property.");

        IntPtr memory = Marshal.AllocHGlobal(checked((int)size));
        try
        {
            Check(GdipGetPropertyItem(image, FrameDelayTag, size, memory));
            var item = Marshal.PtrToStructure<PropertyItem>(memory);
            if (item.Type != 4 || item.Value == IntPtr.Zero ||
                item.Length < checked(count * sizeof(int)))
                throw new InvalidDataException("Incomplete GIF frame-delay property.");

            for (int i = 0; i < count; i++)
                result[i] = checked(Marshal.ReadInt32(item.Value, i * sizeof(int)) * 10);
        }
        finally { Marshal.FreeHGlobal(memory); }

        return result;
    }

    public void Dispose()
    {
        if (image != IntPtr.Zero)
        {
            GdipDisposeImage(image);
            image = IntPtr.Zero;
        }

        if (stream != IntPtr.Zero)
        {
            Marshal.Release(stream);
            stream = IntPtr.Zero;
        }

        if (token != IntPtr.Zero)
        {
            GdiplusShutdown(token);
            token = IntPtr.Zero;
        }
    }

    private static void Check(int status)
    {
        if (status != 0)
            throw new InvalidDataException($"GDI+ GIF decoder status {status}.");
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct StartupInput
    {
        public uint Version;
        public IntPtr DebugCallback;
        public int SuppressBackgroundThread;
        public int SuppressExternalCodecs;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PropertyItem
    {
        public uint Id, Length;
        public ushort Type;
        public IntPtr Value;
    }

    [DllImport("shlwapi.dll", ExactSpelling = true)]
    private static extern IntPtr SHCreateMemStream(byte[] data, uint size);

    [DllImport("gdiplus.dll", ExactSpelling = true)]
    private static extern int GdiplusStartup(out IntPtr token, in StartupInput input, IntPtr output);
    [DllImport("gdiplus.dll", ExactSpelling = true)]
    private static extern void GdiplusShutdown(IntPtr token);
    [DllImport("gdiplus.dll", ExactSpelling = true)]
    private static extern int GdipLoadImageFromStream(IntPtr stream, out IntPtr image);
    [DllImport("gdiplus.dll", ExactSpelling = true)]
    private static extern int GdipDisposeImage(IntPtr image);
    [DllImport("gdiplus.dll", ExactSpelling = true)]
    private static extern int GdipGetImageRawFormat(IntPtr image, out Guid format);
    [DllImport("gdiplus.dll", ExactSpelling = true)]
    private static extern int GdipGetImageWidth(IntPtr image, out uint width);
    [DllImport("gdiplus.dll", ExactSpelling = true)]
    private static extern int GdipGetImageHeight(IntPtr image, out uint height);
    [DllImport("gdiplus.dll", ExactSpelling = true)]
    private static extern int GdipImageGetFrameCount(IntPtr image, in Guid dimension, out uint count);
    [DllImport("gdiplus.dll", ExactSpelling = true)]
    private static extern int GdipImageSelectActiveFrame(IntPtr image, in Guid dimension, uint index);
    [DllImport("gdiplus.dll", ExactSpelling = true)]
    private static extern int GdipGetPropertyItemSize(IntPtr image, uint property, out uint size);
    [DllImport("gdiplus.dll", ExactSpelling = true)]
    private static extern int GdipGetPropertyItem(IntPtr image, uint property, uint size, IntPtr item);
    [DllImport("gdiplus.dll", ExactSpelling = true)]
    private static extern int GdipCreateBitmapFromScan0(int width, int height, int stride,
        int pixelFormat, IntPtr data, out IntPtr bitmap);
    [DllImport("gdiplus.dll", ExactSpelling = true)]
    private static extern int GdipGetImageGraphicsContext(IntPtr image, out IntPtr graphics);
    [DllImport("gdiplus.dll", ExactSpelling = true)]
    private static extern int GdipDrawImageRectI(IntPtr graphics, IntPtr image, int x, int y, int width, int height);
    [DllImport("gdiplus.dll", ExactSpelling = true)]
    private static extern int GdipDeleteGraphics(IntPtr graphics);
}
