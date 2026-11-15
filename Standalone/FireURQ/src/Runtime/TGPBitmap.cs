using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace FireURQ.Runtime;

[StructLayout(LayoutKind.Sequential)]
public struct TGpRect
{
    public int X, Y, Width, Height;
}

[StructLayout(LayoutKind.Sequential)]
public struct TBitmapData
{
    public uint Width, Height;
    public int Stride, PixelFormat;
    public IntPtr Scan0;
    private UIntPtr reserved;
}

public sealed class TGPBitmap : TObject
{
    private IntPtr token;
    internal IntPtr Handle { get; private set; }

    private TGPBitmap()
    {
        var input = new GdiPlusFontBitmaps.StartupInput { Version = 1 };
        GdiPlusFontBitmaps.Check(GdiPlusFontBitmaps.GdiplusStartup(out token, in input, IntPtr.Zero));
    }

    public static TGPBitmap Create(int width, int height, int pixelFormat)
    {
        var bitmap = new TGPBitmap();
        int status = GdiPlusFontBitmaps.GdipCreateBitmapFromScan0(
            width, height, 0, pixelFormat, IntPtr.Zero, out IntPtr handle);
        return bitmap.Attach(status, handle);
    }

    private TGPBitmap Attach(int status, IntPtr handle)
    {
        Handle = handle;
        if (status != 0)
        {
            Free();
            GdiPlusFontBitmaps.Check(status);
        }
        return this;
    }

    public int GetWidth()
    {
        GdiPlusFontBitmaps.Check(GdiPlusFontBitmaps.GdipGetImageWidth(Handle, out uint width));
        return checked((int)width);
    }

    public int GetHeight()
    {
        GdiPlusFontBitmaps.Check(GdiPlusFontBitmaps.GdipGetImageHeight(Handle, out uint height));
        return checked((int)height);
    }

    public int LockBits(in TGpRect rect, uint flags, int format, out TBitmapData data)
    {
        data = default;
        return GdiPlusFontBitmaps.GdipBitmapLockBits(Handle, in rect, flags, format, ref data);
    }

    public int UnlockBits(ref TBitmapData data) =>
        GdiPlusFontBitmaps.GdipBitmapUnlockBits(Handle, ref data);

    public TGPBitmap Clone(int x, int y, int width, int height, int format)
    {
        var bitmap = new TGPBitmap();
        int status = GdiPlusFontBitmaps.GdipCloneBitmapAreaI(
            x, y, width, height, format, Handle, out IntPtr clone);
        return bitmap.Attach(status, clone);
    }

    public int Save(string filename, in Guid encoder) =>
        GdiPlusFontBitmaps.GdipSaveImageToFile(Handle, filename, in encoder, IntPtr.Zero);

    public int Save(IStream stream, in Guid encoder) =>
        GdiPlusFontBitmaps.GdipSaveImageToStream(Handle, stream, in encoder, IntPtr.Zero);

    public override void Destroy()
    {
        if (Handle != IntPtr.Zero)
        {
            GdiPlusFontBitmaps.GdipDisposeImage(Handle);
            Handle = IntPtr.Zero;
        }

        if (token != IntPtr.Zero)
        {
            GdiPlusFontBitmaps.GdiplusShutdown(token);
            token = IntPtr.Zero;
        }

        base.Destroy();
    }
}

public sealed class TGPGraphics : TObject
{
    private IntPtr handle;
    private readonly TGPBitmap target;

    private TGPGraphics(TGPBitmap target)
    {
        this.target = target;
        GdiPlusFontBitmaps.Check(
            GdiPlusFontBitmaps.GdipGetImageGraphicsContext(target.Handle, out handle));
    }

    public static TGPGraphics Create(TGPBitmap target) => new(target);

    public int DrawImage(TGPBitmap source, int x, int y)
    {
        int status = GdiPlusFontBitmaps.GdipDrawImageI(handle, source.Handle, x, y);
        GC.KeepAlive(target);
        return status;
    }

    public override void Destroy()
    {
        if (handle != IntPtr.Zero)
        {
            GdiPlusFontBitmaps.GdipDeleteGraphics(handle);
            handle = IntPtr.Zero;
        }

        base.Destroy();
    }
}

internal static class GdiPlusFontBitmaps
{
    public const int Ok = 0;
    public const int PixelFormat32bppARGB = 0x0026200A;
    public const uint ImageLockModeRead = 1;
    public const uint ImageLockModeWrite = 2;

    public static TGpRect MakeRect(int x, int y, int width, int height) =>
        new() { X = x, Y = y, Width = width, Height = height };

    internal static void Check(int status)
    {
        if (status != Ok)
            throw new InvalidDataException($"GDI+ bitmap status {status}.");
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct StartupInput
    {
        public uint Version;
        public IntPtr DebugCallback;
        public int SuppressBackgroundThread;
        public int SuppressExternalCodecs;
    }

    [DllImport("gdiplus.dll", ExactSpelling = true)]
    internal static extern int GdiplusStartup(out IntPtr token, in StartupInput input, IntPtr output);
    [DllImport("gdiplus.dll", ExactSpelling = true)]
    internal static extern void GdiplusShutdown(IntPtr token);
    [DllImport("gdiplus.dll", ExactSpelling = true)]
    internal static extern int GdipCreateBitmapFromScan0(int width, int height, int stride,
        int format, IntPtr scan0, out IntPtr bitmap);
    [DllImport("gdiplus.dll", ExactSpelling = true)]
    internal static extern int GdipDisposeImage(IntPtr image);
    [DllImport("gdiplus.dll", ExactSpelling = true)]
    internal static extern int GdipGetImageWidth(IntPtr image, out uint width);
    [DllImport("gdiplus.dll", ExactSpelling = true)]
    internal static extern int GdipGetImageHeight(IntPtr image, out uint height);
    [DllImport("gdiplus.dll", ExactSpelling = true)]
    internal static extern int GdipBitmapLockBits(IntPtr bitmap, in TGpRect rect, uint flags,
        int format, ref TBitmapData data);
    [DllImport("gdiplus.dll", ExactSpelling = true)]
    internal static extern int GdipBitmapUnlockBits(IntPtr bitmap, ref TBitmapData data);
    [DllImport("gdiplus.dll", ExactSpelling = true)]
    internal static extern int GdipCloneBitmapAreaI(int x, int y, int width, int height,
        int format, IntPtr bitmap, out IntPtr clone);
    [DllImport("gdiplus.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
    internal static extern int GdipSaveImageToFile(IntPtr image, string filename, in Guid encoder,
        IntPtr parameters);
    [DllImport("gdiplus.dll", ExactSpelling = true)]
    internal static extern int GdipSaveImageToStream(IntPtr image,
        [MarshalAs(UnmanagedType.Interface)] IStream stream, in Guid encoder, IntPtr parameters);
    [DllImport("gdiplus.dll", ExactSpelling = true)]
    internal static extern int GdipGetImageGraphicsContext(IntPtr image, out IntPtr graphics);
    [DllImport("gdiplus.dll", ExactSpelling = true)]
    internal static extern int GdipDrawImageI(IntPtr graphics, IntPtr image, int x, int y);
    [DllImport("gdiplus.dll", ExactSpelling = true)]
    internal static extern int GdipDeleteGraphics(IntPtr graphics);
}
