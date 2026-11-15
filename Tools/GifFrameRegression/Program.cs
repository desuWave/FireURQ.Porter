using System;
using System.IO;
using FireURQ.Runtime;

if (!OperatingSystem.IsWindows() || args.Length != 1)
    throw new ArgumentException("On Windows, pass the path to the original tests/ver1.9.2/line.gif.");

using var stream = File.OpenRead(args[0]);
using var gif = new GifFrames(stream);
if (gif.Width != 400 || gif.Height != 100 || gif.Count != 4)
    throw new InvalidDataException("Unexpected dimensions or frame count in line.gif.");

var frames = new int[gif.Count][];
for (int i = 0; i < gif.Count; i++)
{
    if (gif.DelayMilliseconds(i) != 200)
        throw new InvalidDataException($"Unexpected delay for frame {i}.");

    frames[i] = gif.DecodeArgb(i);
    if (!Array.Exists(frames[i], pixel => (pixel >>> 24) != 0))
        throw new InvalidDataException($"Frame {i} contains no visible pixels.");

    if (i > 0 && frames[i].AsSpan().SequenceEqual(frames[i - 1]))
        throw new InvalidDataException($"Frame {i} matches the preceding frame.");
}

if (!gif.DecodeArgb(0).AsSpan().SequenceEqual(frames[0]))
    throw new InvalidDataException("Seeking from the last frame to the first changed its pixels.");

if (gif.PlaybackDelayMilliseconds(0, 100) != 240 ||
    gif.PlaybackDelayMilliseconds(0, 200) != 120)
    throw new InvalidDataException("GIF playback delay calculation failed.");

byte[] bitmap = gif.EncodeFrameBitmap(0);
using var header = new BinaryReader(new MemoryStream(bitmap));
if (header.ReadUInt16() != 0x4D42 || header.ReadInt32() != bitmap.Length)
    throw new InvalidDataException("Invalid bitmap file header.");
header.BaseStream.Position = 18;
if (header.ReadInt32() != 400 || header.ReadInt32() != 100 ||
    header.ReadUInt16() != 1 || header.ReadUInt16() != 24 ||
    bitmap.Length != 54 + 400 * 100 * 3)
    throw new InvalidDataException("Invalid GIF frame bitmap layout.");

Console.WriteLine("FireURQ line.gif frame decoding passed.");
Console.WriteLine("FireURQ GIF delay and texture bitmap checks passed.");
