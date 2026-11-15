using System;
using System.IO;
using System.Runtime.InteropServices;
using FireURQ.Runtime;
using static FireURQ.Runtime.GdiPlusFontBitmaps;

const int ink = unchecked((int)0xFF123456);
var source = TGPBitmap.Create(4, 4, PixelFormat32bppARGB);
try
{
    Check(source.LockBits(MakeRect(0, 0, 4, 4), ImageLockModeWrite,
        PixelFormat32bppARGB, out var sourceData));
    try
    {
        Marshal.WriteInt32(IntPtr.Add(sourceData.Scan0, sourceData.Stride + 4), ink);
    }
    finally { Check(source.UnlockBits(ref sourceData)); }

    var glyph = source.Clone(1, 1, 1, 1, PixelFormat32bppARGB);
    try
    {
        if (glyph.GetWidth() != 1 || glyph.GetHeight() != 1)
            throw new InvalidDataException("Glyph crop has the wrong dimensions.");

        var atlas = TGPBitmap.Create(4, 4, PixelFormat32bppARGB);
        try
        {
            var graphics = TGPGraphics.Create(atlas);
            try { Check(graphics.DrawImage(glyph, 2, 2)); }
            finally { graphics.Free(); }

            Check(atlas.LockBits(MakeRect(0, 0, 4, 4), ImageLockModeRead,
                PixelFormat32bppARGB, out var atlasData));
            try
            {
                if (Marshal.ReadInt32(IntPtr.Add(atlasData.Scan0, 2 * atlasData.Stride + 8)) != ink)
                    throw new InvalidDataException("Copied glyph pixel does not match the source.");
            }
            finally { Check(atlas.UnlockBits(ref atlasData)); }

            string png = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".png");
            try
            {
                Check(atlas.Save(png, new Guid("557CF406-1A04-11D3-9A73-0000F81EF32E")));
                if (!File.ReadAllBytes(png).AsSpan().StartsWith(Convert.FromHexString("89504E470D0A1A0A")))
                    throw new InvalidDataException("GDI+ did not write a PNG atlas.");
            }
            finally { File.Delete(png); }
        }
        finally { atlas.Free(); }
    }
    finally { glyph.Free(); }
}
finally { source.Free(); }

Console.WriteLine("FireURQ font bitmap lock, crop, draw and PNG checks passed.");
