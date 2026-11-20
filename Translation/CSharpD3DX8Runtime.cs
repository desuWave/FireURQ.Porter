namespace FireURQ.Porter.Translation;

public static class CSharpD3DX8Runtime
{
    public const string Source = """
global using static FireURQ.Runtime.D3DX8;

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
    public const uint D3DX_DEFAULT = uint.MaxValue;
    public const uint D3DX_FILTER_NONE = 1;

    public static void D3DXMatrixIdentity(ref TD3DMatrix value)
    {
        value = default;
        value._11 = value._22 = value._33 = value._44 = 1f;
    }

    public static void D3DXMatrixTranslation(
        ref TD3DMatrix value, float x, float y, float z)
    {
        D3DXMatrixIdentity(ref value);
        value._41 = x;
        value._42 = y;
        value._43 = z;
    }

    public static void D3DXMatrixScaling(
        ref TD3DMatrix value, float x, float y, float z)
    {
        value = default;
        value._11 = x;
        value._22 = y;
        value._33 = z;
        value._44 = 1f;
    }

    public static void D3DXMatrixRotationZ(ref TD3DMatrix value, float angle)
    {
        value = default;
        float cosine = MathF.Cos(angle);
        float sine = MathF.Sin(angle);
        value._11 = cosine;
        value._12 = sine;
        value._21 = -sine;
        value._22 = cosine;
        value._33 = 1f;
        value._44 = 1f;
    }

    public static void D3DXMatrixOrthoOffCenterLH(
        ref TD3DMatrix value, float left, float right, float bottom, float top,
        float nearPlane, float farPlane)
    {
        value = default;
        value._11 = 2f / (right - left);
        value._22 = 2f / (top - bottom);
        value._33 = 1f / (farPlane - nearPlane);
        value._41 = (left + right) / (left - right);
        value._42 = (top + bottom) / (bottom - top);
        value._43 = nearPlane / (nearPlane - farPlane);
        value._44 = 1f;
    }

    public static void D3DXMatrixMultiply(
        ref TD3DMatrix result, in TD3DMatrix left, in TD3DMatrix right)
    {
        TD3DMatrix value = default;
        value._11 = left._11 * right._11 + left._12 * right._21 + left._13 * right._31 + left._14 * right._41;
        value._12 = left._11 * right._12 + left._12 * right._22 + left._13 * right._32 + left._14 * right._42;
        value._13 = left._11 * right._13 + left._12 * right._23 + left._13 * right._33 + left._14 * right._43;
        value._14 = left._11 * right._14 + left._12 * right._24 + left._13 * right._34 + left._14 * right._44;
        value._21 = left._21 * right._11 + left._22 * right._21 + left._23 * right._31 + left._24 * right._41;
        value._22 = left._21 * right._12 + left._22 * right._22 + left._23 * right._32 + left._24 * right._42;
        value._23 = left._21 * right._13 + left._22 * right._23 + left._23 * right._33 + left._24 * right._43;
        value._24 = left._21 * right._14 + left._22 * right._24 + left._23 * right._34 + left._24 * right._44;
        value._31 = left._31 * right._11 + left._32 * right._21 + left._33 * right._31 + left._34 * right._41;
        value._32 = left._31 * right._12 + left._32 * right._22 + left._33 * right._32 + left._34 * right._42;
        value._33 = left._31 * right._13 + left._32 * right._23 + left._33 * right._33 + left._34 * right._43;
        value._34 = left._31 * right._14 + left._32 * right._24 + left._33 * right._34 + left._34 * right._44;
        value._41 = left._41 * right._11 + left._42 * right._21 + left._43 * right._31 + left._44 * right._41;
        value._42 = left._41 * right._12 + left._42 * right._22 + left._43 * right._32 + left._44 * right._42;
        value._43 = left._41 * right._13 + left._42 * right._23 + left._43 * right._33 + left._44 * right._43;
        value._44 = left._41 * right._14 + left._42 * right._24 + left._43 * right._34 + left._44 * right._44;
        result = value;
    }

    public static int D3DXCreateTexture(
        IDirect3DDevice8 device, uint width, uint height, uint levels,
        uint usage, int format, int pool, out IDirect3DTexture8 texture) =>
        device.CreateTexture(width, height, levels, usage, format, pool, out texture);

    [DllImport("D3DX81ab.dll", ExactSpelling = true,
        CallingConvention = CallingConvention.StdCall)]
    private static extern int D3DXCreateTextureFromFileInMemoryEx(
        IDirect3DDevice8 device, IntPtr data, uint size, uint width, uint height,
        uint levels, uint usage, int format, int pool, uint filter, uint mipFilter,
        uint colorKey, out TD3DXImageInfo sourceInfo, IntPtr palette,
        out IDirect3DTexture8 texture);

    public static int CreateTextureFromFileInMemoryEx(
        IDirect3DDevice8 device, IntPtr data, uint size, uint width, uint height,
        uint levels, uint usage, int format, int pool, uint filter, uint mipFilter,
        uint colorKey, out TD3DXImageInfo sourceInfo, object? palette,
        out IDirect3DTexture8 texture) =>
        D3DXCreateTextureFromFileInMemoryEx(device, data, size, width, height,
            levels, usage, format, pool, filter, mipFilter, colorKey,
            out sourceInfo, IntPtr.Zero, out texture);

    [DllImport("D3DX81ab.dll", ExactSpelling = true,
        CallingConvention = CallingConvention.StdCall)]
    internal static extern int D3DXCreateFontIndirect(
        IDirect3DDevice8 device, in TLogFont description, out ID3DXFont font);

    [DllImport("D3DX81ab.dll", ExactSpelling = true,
        CallingConvention = CallingConvention.StdCall)]
    public static extern int D3DXGetImageInfoFromFileInMemory(
        IntPtr data, uint size, out TD3DXImageInfo info);
}
""";
}