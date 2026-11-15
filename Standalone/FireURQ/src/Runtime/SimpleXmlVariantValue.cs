using System;
using System.Buffers.Binary;
using System.Text;

namespace FireURQ.Runtime;

internal static class SimpleXmlVariantValue
{
    internal static Variant Read(SimpleXmlVariantBinary.ReadBuffer readExactly, Encoding ansiEncoding)
    {
        var (type, payload) = SimpleXmlVariantBinary.Read(readExactly);
        object? value = type switch
        {
            0 or 1 => null,
            2 => BinaryPrimitives.ReadInt16LittleEndian(payload),
            3 => BinaryPrimitives.ReadInt32LittleEndian(payload),
            4 => BinaryPrimitives.ReadSingleLittleEndian(payload),
            5 or 7 => BinaryPrimitives.ReadDoubleLittleEndian(payload),
            6 => BinaryPrimitives.ReadInt64LittleEndian(payload) / 10000m,
            8 => Encoding.Unicode.GetString(payload),
            11 => BinaryPrimitives.ReadInt16LittleEndian(payload) != 0,
            16 => unchecked((sbyte)payload[0]),
            17 => payload[0],
            18 => BinaryPrimitives.ReadUInt16LittleEndian(payload),
            19 => BinaryPrimitives.ReadUInt32LittleEndian(payload),
            20 => BinaryPrimitives.ReadInt64LittleEndian(payload),
            256 => ansiEncoding.GetString(payload),
            0x2011 => payload,
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
        return Variant.FromSimpleXml(type, value, payload);
    }

    internal static void Write(SimpleXmlVariantBinary.WriteBuffer write, Variant value, Encoding ansiEncoding)
    {
        if (value.HasSimpleXmlPayload)
        {
            SimpleXmlVariantBinary.Write(write, value.TypeCode, value.SimpleXmlPayload);
            return;
        }

        Span<byte> payload = stackalloc byte[sizeof(double)];
        int length;
        switch (value.TypeCode)
        {
            case 0:
            case 1:
                length = 0;
                break;
            case 3 when value.TryGetInteger(out int integer):
                BinaryPrimitives.WriteInt32LittleEndian(payload, integer);
                length = sizeof(int);
                break;
            case 5 when value.TryGetDouble(out double number):
                BinaryPrimitives.WriteDoubleLittleEndian(payload, number);
                length = sizeof(double);
                break;
            case 11 when value.TryGetBoolean(out bool boolean):
                BinaryPrimitives.WriteInt16LittleEndian(payload, boolean ? (short)-1 : (short)0);
                length = sizeof(short);
                break;
            case 256 when value.TryGetString(out string text):
                SimpleXmlVariantBinary.Write(write, value.TypeCode, ansiEncoding.GetBytes(text));
                return;
            default:
                throw new ArgumentException($"Unsupported Variant type {value.TypeCode}.", nameof(value));
        }

        SimpleXmlVariantBinary.Write(write, value.TypeCode, payload[..length]);
    }
}
