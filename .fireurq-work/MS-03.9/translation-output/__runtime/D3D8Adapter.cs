using System;
using System.Runtime.InteropServices;

namespace FireURQ.Runtime;

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct D3DADAPTER_IDENTIFIER8
{
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 512, ArraySubType = UnmanagedType.U1)]
    public byte[] Driver;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 512, ArraySubType = UnmanagedType.U1)]
    public byte[] Description;

    public long DriverVersion;
    public uint VendorId, DeviceId, SubSysId, Revision;
    public Guid DeviceIdentifier;
    public uint WHQLLevel;

    public int DriverVersionFirstWord => (int)((DriverVersion >> 16) & 0xffff);
    public int DriverVersionSecondWord => (int)(DriverVersion & 0xffff);

    public string DriverText => DecodeAnsi(Driver);
    public string DescriptionText => DecodeAnsi(Description);

    private static string DecodeAnsi(byte[]? buffer)
    {
        if (buffer is null || buffer.Length == 0)
            return string.Empty;

        int length = Array.IndexOf(buffer, (byte)0);
        if (length < 0)
            length = buffer.Length;

        if (length == 0)
            return string.Empty;

        GCHandle pin = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try
        {
            return Marshal.PtrToStringAnsi(pin.AddrOfPinnedObject(), length)
                ?? string.Empty;
        }
        finally
        {
            pin.Free();
        }
    }
}

[StructLayout(LayoutKind.Sequential)]
public struct D3DDISPLAYMODE
{
    public uint Width, Height, RefreshRate;
    public int Format;
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct D3DPRESENT_PARAMETERS
{
    public uint BackBufferWidth;
    public uint BackBufferHeight;
    public int BackBufferFormat;
    public uint BackBufferCount;
    public int MultiSampleType;
    public int SwapEffect;
    public IntPtr hDeviceWindow;

    [MarshalAs(UnmanagedType.Bool)]
    public bool Windowed;

    [MarshalAs(UnmanagedType.Bool)]
    public bool EnableAutoDepthStencil;

    public int AutoDepthStencilFormat;
    public uint Flags;
    public uint FullScreen_RefreshRateInHz;
    public uint FullScreen_PresentationInterval;
}

[ComImport]
[Guid("1DD9E8DA-1C77-4D40-B0CF-98FEFDFF9512")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IDirect3D8
{
    [PreserveSig] int RegisterSoftwareDevice(IntPtr initializeFunction);
    [PreserveSig] uint GetAdapterCount();
    [PreserveSig] int GetAdapterIdentifier(
        uint adapter, uint flags, out D3DADAPTER_IDENTIFIER8 identifier);
    [PreserveSig] uint GetAdapterModeCount(uint adapter);
    [PreserveSig] int EnumAdapterModes(uint adapter, uint mode, out D3DDISPLAYMODE displayMode);
    [PreserveSig] int GetAdapterDisplayMode(uint adapter, out D3DDISPLAYMODE displayMode);

    [PreserveSig] int CheckDeviceType(uint adapter, int deviceType, int displayFormat,
        int backBufferFormat, [MarshalAs(UnmanagedType.Bool)] bool windowed);
    [PreserveSig] int CheckDeviceFormat(uint adapter, int deviceType, int adapterFormat,
        uint usage, int resourceType, int checkFormat);
    [PreserveSig] int CheckDeviceMultiSampleType(uint adapter, int deviceType, int surfaceFormat,
        [MarshalAs(UnmanagedType.Bool)] bool windowed, int multiSampleType);
    [PreserveSig] int CheckDepthStencilMatch(uint adapter, int deviceType, int adapterFormat,
        int renderTargetFormat, int depthStencilFormat);
    [PreserveSig] int GetDeviceCaps(uint adapter, int deviceType, IntPtr caps);
    [PreserveSig] IntPtr GetAdapterMonitor(uint adapter);
    [PreserveSig] int CreateDevice(uint adapter, int deviceType, IntPtr focusWindow,
        uint behaviorFlags, [In, Out] ref D3DPRESENT_PARAMETERS parameters,
        [MarshalAs(UnmanagedType.Interface)] out IDirect3DDevice8 device);
}

[ComImport]
[Guid("7385E5DF-8FE8-41D5-86B6-D7B48547B6CF")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IDirect3DDevice8
{
    [PreserveSig] int TestCooperativeLevel();
    [PreserveSig] uint GetAvailableTextureMem();
    [PreserveSig] int ResourceManagerDiscardBytes(uint bytes);
    [PreserveSig] int GetDirect3D([MarshalAs(UnmanagedType.Interface)] out IDirect3D8 direct3D);
    [PreserveSig] int GetDeviceCaps(IntPtr caps);
    [PreserveSig] int GetDisplayMode(out D3DDISPLAYMODE mode);
    [PreserveSig] int GetCreationParameters(IntPtr parameters);
    [PreserveSig] int SetCursorProperties(uint xHotSpot, uint yHotSpot, IntPtr bitmap);
    [PreserveSig] void SetCursorPosition(uint x, uint y, uint flags);
    [PreserveSig] int ShowCursor(int show);
    [PreserveSig] int CreateAdditionalSwapChain(
        [In, Out] ref D3DPRESENT_PARAMETERS parameters, out IntPtr swapChain);
    [PreserveSig] int Reset([In, Out] ref D3DPRESENT_PARAMETERS parameters);
    [PreserveSig] int Present(
        IntPtr sourceRect, IntPtr destinationRect, IntPtr window, IntPtr dirtyRegion);

    [PreserveSig] int GetBackBuffer(uint backBuffer, int type, out IDirect3DSurface8 surface);
    [PreserveSig] int GetRasterStatus(IntPtr rasterStatus);
    [PreserveSig] void SetGammaRamp(uint flags, IntPtr ramp);
    [PreserveSig] void GetGammaRamp(IntPtr ramp);
    [PreserveSig] int CreateTexture(uint width, uint height, uint levels,
        uint usage, int format, int pool, out IDirect3DTexture8 texture);
    [PreserveSig] int CreateVolumeTexture(uint width, uint height, uint depth,
        uint levels, uint usage, int format, int pool, out IntPtr texture);
    [PreserveSig] int CreateCubeTexture(uint edgeLength, uint levels,
        uint usage, int format, int pool, out IntPtr texture);
    [PreserveSig] int CreateVertexBuffer(uint length, uint usage, uint fvf,
        int pool, out IDirect3DVertexBuffer8 buffer);
    [PreserveSig] int CreateIndexBuffer(uint length, uint usage, int format,
        int pool, out IDirect3DIndexBuffer8 buffer);
    [PreserveSig] int CreateRenderTarget(uint width, uint height, int format,
        int multiSample, [MarshalAs(UnmanagedType.Bool)] bool lockable,
        out IDirect3DSurface8 surface);
    [PreserveSig] int CreateDepthStencilSurface(uint width, uint height, int format,
        int multiSample, out IDirect3DSurface8 surface);
    [PreserveSig] int CreateImageSurface(uint width, uint height, int format,
        out IDirect3DSurface8 surface);
    [PreserveSig] int CopyRects(IDirect3DSurface8 source, IntPtr sourceRects,
        uint count, IDirect3DSurface8 destination, IntPtr destinationPoints);
    [PreserveSig] int UpdateTexture(IntPtr source, IntPtr destination);
    [PreserveSig] int GetFrontBuffer(IDirect3DSurface8 destination);
    [PreserveSig] int SetRenderTarget(IDirect3DSurface8? renderTarget, IDirect3DSurface8? depthStencil);
    [PreserveSig] int GetRenderTarget(out IDirect3DSurface8 surface);
    [PreserveSig] int GetDepthStencilSurface(out IDirect3DSurface8 surface);
    [PreserveSig] int BeginScene();
    [PreserveSig] int EndScene();
    [PreserveSig] int Clear(uint count, IntPtr rects, uint flags, uint color, float z, uint stencil);
    [PreserveSig] int SetTransform(int state, [In] ref TD3DMatrix matrix);
}

[ComImport]
[Guid("B96EEBCA-B326-4EA5-882F-2FF5BAE021DD")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IDirect3DSurface8
{
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct D3DSURFACE_DESC
{
    public int Format;
    public int Type;
    public uint Usage;
    public int Pool;
    public uint Size;
    public int MultiSampleType;
    public uint Width;
    public uint Height;
}

[StructLayout(LayoutKind.Sequential)]
public struct D3DLOCKED_RECT
{
    public int Pitch;
    public IntPtr Bits;
}

[ComImport]
[Guid("E4CDD575-2866-4F01-B12E-7EECE1EC9358")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IDirect3DTexture8
{
    // IDirect3DResource8 slots.
    [PreserveSig] int GetDevice(out IDirect3DDevice8 device);
    [PreserveSig] int SetPrivateData(in Guid key, IntPtr data, uint size, uint flags);
    [PreserveSig] int GetPrivateData(in Guid key, IntPtr data, ref uint size);
    [PreserveSig] int FreePrivateData(in Guid key);
    [PreserveSig] uint SetPriority(uint priority);
    [PreserveSig] uint GetPriority();
    [PreserveSig] void PreLoad();
    [PreserveSig] int GetType();

    // IDirect3DBaseTexture8 slots.
    [PreserveSig] uint SetLOD(uint lod);
    [PreserveSig] uint GetLOD();
    [PreserveSig] uint GetLevelCount();

    [PreserveSig] int GetLevelDesc(uint level, out D3DSURFACE_DESC description);
    [PreserveSig] int GetSurfaceLevel(uint level, out IDirect3DSurface8 surface);
    [PreserveSig] int LockRect(uint level, out D3DLOCKED_RECT lockedRect, IntPtr rect, uint flags);
    [PreserveSig] int UnlockRect(uint level);
}

[ComImport]
[Guid("8AEEEAC7-05F9-44D4-B591-000B0DF1CB95")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IDirect3DVertexBuffer8
{
    // IDirect3DResource8 slots.
    [PreserveSig] int GetDevice(out IDirect3DDevice8 device);
    [PreserveSig] int SetPrivateData(in Guid key, IntPtr data, uint size, uint flags);
    [PreserveSig] int GetPrivateData(in Guid key, IntPtr data, ref uint size);
    [PreserveSig] int FreePrivateData(in Guid key);
    [PreserveSig] uint SetPriority(uint priority);
    [PreserveSig] uint GetPriority();
    [PreserveSig] void PreLoad();
    [PreserveSig] int GetType();

    [PreserveSig] int Lock(uint offset, uint size, out IntPtr data, uint flags);
    [PreserveSig] int Unlock();
}

[ComImport]
[Guid("0E689C9A-053D-44A0-9D92-DB0E3D750F86")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IDirect3DIndexBuffer8
{
    // IDirect3DResource8 slots.
    [PreserveSig] int GetDevice(out IDirect3DDevice8 device);
    [PreserveSig] int SetPrivateData(in Guid key, IntPtr data, uint size, uint flags);
    [PreserveSig] int GetPrivateData(in Guid key, IntPtr data, ref uint size);
    [PreserveSig] int FreePrivateData(in Guid key);
    [PreserveSig] uint SetPriority(uint priority);
    [PreserveSig] uint GetPriority();
    [PreserveSig] void PreLoad();
    [PreserveSig] int GetType();

    [PreserveSig] int Lock(uint offset, uint size, out IntPtr data, uint flags);
    [PreserveSig] int Unlock();
}

[StructLayout(LayoutKind.Sequential)]
public struct TD3DMatrix
{
    public float _11, _12, _13, _14;
    public float _21, _22, _23, _24;
    public float _31, _32, _33, _34;
    public float _41, _42, _43, _44;
}

public static class D3D8
{
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int SetStreamSourceCall(
        IntPtr self, uint stream, IntPtr buffer, uint stride);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int SetIndicesCall(
        IntPtr self, IntPtr buffer, uint baseVertexIndex);

    public static int SetStreamSource(
        IDirect3DDevice8 device, uint stream, IDirect3DVertexBuffer8? buffer, uint stride)
    {
        IntPtr self = Marshal.GetComInterfaceForObject(device, typeof(IDirect3DDevice8));
        IntPtr nativeBuffer = IntPtr.Zero;
        try
        {
            if (buffer != null)
                nativeBuffer = Marshal.GetComInterfaceForObject(buffer, typeof(IDirect3DVertexBuffer8));

            IntPtr slot = Marshal.ReadIntPtr(Marshal.ReadIntPtr(self), 83 * IntPtr.Size);
            return Marshal.GetDelegateForFunctionPointer<SetStreamSourceCall>(slot)(
                self, stream, nativeBuffer, stride);
        }
        finally
        {
            if (nativeBuffer != IntPtr.Zero)
                Marshal.Release(nativeBuffer);
            Marshal.Release(self);
        }
    }

    public static int SetIndices(
        IDirect3DDevice8 device, IDirect3DIndexBuffer8? buffer, uint baseVertexIndex)
    {
        IntPtr self = Marshal.GetComInterfaceForObject(device, typeof(IDirect3DDevice8));
        IntPtr nativeBuffer = IntPtr.Zero;
        try
        {
            if (buffer != null)
                nativeBuffer = Marshal.GetComInterfaceForObject(buffer, typeof(IDirect3DIndexBuffer8));

            IntPtr slot = Marshal.ReadIntPtr(Marshal.ReadIntPtr(self), 85 * IntPtr.Size);
            return Marshal.GetDelegateForFunctionPointer<SetIndicesCall>(slot)(
                self, nativeBuffer, baseVertexIndex);
        }
        finally
        {
            if (nativeBuffer != IntPtr.Zero)
                Marshal.Release(nativeBuffer);
            Marshal.Release(self);
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int VertexShaderCall(IntPtr self, uint shader);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int PrimitiveCall(IntPtr self, int type, uint start, uint count);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int IndexedPrimitiveCall(IntPtr self, int type,
        uint minIndex, uint vertexCount, uint startIndex, uint primitiveCount);

    private static TMethod DrawMethod<TMethod>(IntPtr self, int slot) where TMethod : class
    {
        IntPtr entry = Marshal.ReadIntPtr(Marshal.ReadIntPtr(self), slot * IntPtr.Size);
        return (TMethod)(object)Marshal.GetDelegateForFunctionPointer(entry, typeof(TMethod));
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int RenderStateCall(IntPtr self, int state, uint value);

    public const int D3DRS_ZENABLE = 7;
    public const uint D3DZB_FALSE = 0;
    public const uint D3DZB_TRUE = 1;

    public static int SetRenderState(IDirect3DDevice8 device, int state, uint value)
    {
        IntPtr self = Marshal.GetComInterfaceForObject(device, typeof(IDirect3DDevice8));
        try { return DrawMethod<RenderStateCall>(self, 50)(self, state, value); }
        finally { Marshal.Release(self); }
    }

    public static int SetVertexShader(IDirect3DDevice8 device, uint shader)
    {
        IntPtr self = Marshal.GetComInterfaceForObject(device, typeof(IDirect3DDevice8));
        try { return DrawMethod<VertexShaderCall>(self, 76)(self, shader); }
        finally { Marshal.Release(self); }
    }

    public static int DrawPrimitive(IDirect3DDevice8 device, int type, uint start, uint count)
    {
        IntPtr self = Marshal.GetComInterfaceForObject(device, typeof(IDirect3DDevice8));
        try { return DrawMethod<PrimitiveCall>(self, 70)(self, type, start, count); }
        finally { Marshal.Release(self); }
    }

    public static int DrawIndexedPrimitive(IDirect3DDevice8 device, int type,
        uint minIndex, uint vertexCount, uint startIndex, uint primitiveCount)
    {
        IntPtr self = Marshal.GetComInterfaceForObject(device, typeof(IDirect3DDevice8));
        try
        {
            return DrawMethod<IndexedPrimitiveCall>(self, 71)(
                self, type, minIndex, vertexCount, startIndex, primitiveCount);
        }
        finally { Marshal.Release(self); }
    }

    public const uint D3DADAPTER_DEFAULT = 0;
    public const uint D3DENUM_NO_WHQL_LEVEL = 2;
    public const int D3DFMT_UNKNOWN = 0;
    public const int D3DFMT_A8R8G8B8 = 21;
    public const int D3DFMT_X8R8G8B8 = 22;
    public const int D3DFMT_R5G6B5 = 23;
    public const int D3DFMT_X1R5G5B5 = 24;
    public const int D3DFMT_A1R5G5B5 = 25;
    public const int D3DFMT_D16 = 80;
    public const int D3DFMT_INDEX16 = 101;
    public const int D3DMULTISAMPLE_NONE = 0;
    public const int D3DDEVTYPE_HAL = 1;
    public const uint D3DCREATE_FPU_PRESERVE = 0x02;
    public const uint D3DCREATE_SOFTWARE_VERTEXPROCESSING = 0x20;
    public const uint D3DCREATE_HARDWARE_VERTEXPROCESSING = 0x40;

    [DllImport("d3d8.dll", ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern IntPtr Direct3DCreate8(uint sdkVersion);

    public static IDirect3D8? Create(uint sdkVersion)
    {
        IntPtr pointer = Direct3DCreate8(sdkVersion);
        if (pointer == IntPtr.Zero)
            return null;

        try
        {
            return (IDirect3D8)Marshal.GetTypedObjectForIUnknown(pointer, typeof(IDirect3D8));
        }
        finally
        {
            Marshal.Release(pointer);
        }
    }
}