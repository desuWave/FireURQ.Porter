using System;
using System.Runtime.InteropServices;

namespace FireURQ.Runtime;

// DirectX 8 d3dx8core.h. COM interop supplies the three IUnknown slots.
[ComImport]
[Guid("89FAD6A5-024D-49AF-8FE7-F51123B85E25")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface ID3DXFont
{
    [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.Interface)] out IDirect3DDevice8 device);
    [PreserveSig] int GetLogFont(IntPtr logFont);
    [PreserveSig] int Begin();
    [PreserveSig] int DrawTextA(IntPtr text, int count, ref TRect bounds, uint format, uint color);
    [PreserveSig] int DrawTextW(IntPtr text, int count, ref TRect bounds, uint format, uint color);
    [PreserveSig] int End();
    [PreserveSig] int OnLostDevice();
    [PreserveSig] int OnResetDevice();
}
