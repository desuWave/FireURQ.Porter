using System;
using System.IO;

namespace FireURQ.Runtime;

// The on-disk portion of TBinXmlReader.ReadVariant and TBinXmlWriter.WriteVariant.
// Keep payload bytes intact, including ANSI strings and the raw WordBool value.
internal static class SimpleXmlVariantBinary
{
    private const ushort WideString = 8;
    private const ushort AnsiString = 256;
    private const ushort ByteArray = 0x2011;

    internal static (ushort TypeCode, byte[] Payload) Read(BinaryReader reader)
    {
        int tag = ReadLongint(reader);
        if (tag is < 0 or > ushort.MaxValue)
            throw new InvalidDataException($"Invalid SimpleXML variant type {tag}.");

        var type = (ushort)tag;
        int length = FixedSize(type);
        if (length < 0)
        {
            int units = ReadLongint(reader);
            int unitSize = type == WideString ? 2 : 1;
            if (units < 0 || units > int.MaxValue / unitSize)
                throw new InvalidDataException($"Invalid SimpleXML variant length {units}.");

            length = units * unitSize;
        }

        Stream stream = reader.BaseStream;
        if (stream.CanSeek && length > stream.Length - stream.Position)
            throw new EndOfStreamException("Truncated SimpleXML variant payload.");

        byte[] payload = reader.ReadBytes(length);
        if (payload.Length != length)
            throw new EndOfStreamException("Truncated SimpleXML variant payload.");

        return (type, payload);
    }

    internal static void Write(BinaryWriter writer, ushort type, ReadOnlySpan<byte> payload)
    {
        int fixedSize = FixedSize(type);
        int unitSize = type == WideString ? 2 : 1;
        if (fixedSize >= 0 ? payload.Length != fixedSize : payload.Length % unitSize != 0)
            throw new ArgumentException("Payload length does not match the variant type.", nameof(payload));

        WriteLongint(writer, type);
        if (fixedSize < 0)
            WriteLongint(writer, payload.Length / unitSize);

        writer.Write(payload);
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

    private static int ReadLongint(BinaryReader reader)
    {
        byte first = reader.ReadByte();
        if (first < 0x80)
            return first;
        if (first == 0xFF)
            return reader.ReadInt32();

        return ((first & 0x7F) << 8) | reader.ReadByte();
    }

    private static void WriteLongint(BinaryWriter writer, int value)
    {
        if (value < 0x80)
        {
            writer.Write((byte)value);
        }
        else if (value < 0x7F00)
        {
            writer.Write((byte)((value >> 8) | 0x80));
            writer.Write((byte)(value & 0xFF));
        }
        else
        {
            // Delphi's reader reserves FF; its two-byte writer collides with
            // that marker at 7F00..7FFF. The reader also accepts this long form.
            writer.Write((byte)0xFF);
            writer.Write(value);
        }
    }
}
