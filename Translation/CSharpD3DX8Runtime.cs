namespace FireURQ.Porter.Translation;

public static class CSharpD3DX8Runtime
{
    public const string Source = """
using System;
using System.Runtime.InteropServices;

namespace FireURQ.Runtime;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct TD3DXImageInfo
{
    public uint Width, Height, Depth, MipLevels;
    public int Format, ResourceType, ImageFileFormat;
}

public static class D3DX8
{
    [DllImport("D3DX81ab.dll", ExactSpelling = true,
        CallingConvention = CallingConvention.StdCall)]
    public static extern int D3DXGetImageInfoFromFileInMemory(
        IntPtr data, uint size, out TD3DXImageInfo info);
}
""";
}