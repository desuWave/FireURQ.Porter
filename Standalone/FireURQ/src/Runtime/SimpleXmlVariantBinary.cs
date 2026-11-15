using System;
using System.Buffers.Binary;
using System.IO;

namespace FireURQ.Runtime;

// The on-disk portion of TBinXmlReader.ReadVariant and TBinXmlWriter.WriteVariant.
// Keep payload bytes intact, including ANSI strings and the raw WordBool value.
internal static class SimpleXmlVariantBinary
{
    private const ushort WideString = 8;
    private const ushort AnsiString = 256;
    private const ushort ByteArray = 0x2011;

    internal delegate void ReadBuffer(Span<byte> buffer);
    internal delegate void WriteBuffer(ReadOnlySpan<byte> buffer);

    internal static (ushort TypeCode, byte[] Payload) Read(ReadBuffer readExactly)
    {
        int tag = ReadLongint(readExactly);
        if (tag is < 0 or > ushort.MaxValue)
            throw new InvalidDataException($"Invalid SimpleXML variant type {tag}.");

        var type = (ushort)tag;
        int length = FixedSize(type);
        if (length < 0)
        {
            int units = ReadLongint(readExactly);
            int unitSize = type == WideString ? 2 : 1;
            if (units < 0 || units > int.MaxValue / unitSize)
                throw new InvalidDataException($"Invalid SimpleXML variant length {units}.");

            length = units * unitSize;
        }

        if (length == 0)
            return (type, Array.Empty<byte>());

        // Read incrementally so a truncated length cannot force a large allocation.
        using var data = new MemoryStream();
        Span<byte> buffer = stackalloc byte[4096];
        for (int remaining = length; remaining > 0;)
        {
            Span<byte> chunk = buffer[..Math.Min(remaining, buffer.Length)];
            readExactly(chunk);
            data.Write(chunk);
            remaining -= chunk.Length;
        }

        return (type, data.ToArray());
    }

    internal static void Write(WriteBuffer write, ushort type, ReadOnlySpan<byte> payload)
    {
        int fixedSize = FixedSize(type);
        int unitSize = type == WideString ? 2 : 1;
        if (fixedSize >= 0 ? payload.Length != fixedSize : payload.Length % unitSize != 0)
            throw new ArgumentException("Payload length does not match the variant type.", nameof(payload));

        WriteLongint(write, type);
        if (fixedSize < 0)
            WriteLongint(write, payload.Length / unitSize);

        if (!payload.IsEmpty)
            write(payload);
    }

    private static int FixedSize(ushort type) => type switch
    {
        0 or 1 => 0, // Empty, Null
        16 or 17 => 1, // ShortInt, Byte
        2 or 11 or 18 => 2, // SmallInt, WordBool, Word
        3 or 4 or 19 => 4, // Integer, Single, LongWord
        5 or 6 or 7 or 20 => 8, // Double, Currency, Date, Int64
        WideString or AnsiString or ByteArray => -1,
        _ => throw new InvalidDataException($"Unsupported SimpleXML variant type {type}.")
    };

    private static int ReadLongint(ReadBuffer readExactly)
    {
        Span<byte> buffer = stackalloc byte[sizeof(int)];
        readExactly(buffer[..1]);
        byte first = buffer[0];
        if (first < 0x80)
            return first;
        if (first == 0xFF)
        {
            readExactly(buffer);
            return BinaryPrimitives.ReadInt32LittleEndian(buffer);
        }

        readExactly(buffer[..1]);
        return ((first & 0x7F) << 8) | buffer[0];
    }

    private static void WriteLongint(WriteBuffer write, int value)
    {
        Span<byte> buffer = stackalloc byte[1 + sizeof(int)];
        int length;
        if (value < 0x80)
        {
            buffer[0] = (byte)value;
            length = 1;
        }
        else if (value < 0x7F00)
        {
            buffer[0] = (byte)((value >> 8) | 0x80);
            buffer[1] = (byte)(value & 0xFF);
            length = 2;
        }
        else
        {
            // The original reader reserves FF for a full 32-bit integer.
            buffer[0] = 0xFF;
            BinaryPrimitives.WriteInt32LittleEndian(buffer[1..], value);
            length = buffer.Length;
        }

        write(buffer[..length]);
    }
}
