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

Console.WriteLine("FireURQ line.gif frame decoding passed.");
