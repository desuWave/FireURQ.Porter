using System;
using System.Numerics;

namespace FireURQ.Runtime;

public readonly partial struct Extended
{
    public static Extended EnsureReal(Variant value) =>
        value.TryGetString(out string text) ? FromInt64(text.Length) : FromVariant(value);

    public static implicit operator Extended(Variant value) => FromVariant(value);

    // Delphi assigns an Extended expression to a varDouble Variant.
    public static implicit operator Variant(Extended value) => (Variant)value.ToDouble();

    private static Extended FromVariant(Variant value) => value.BoxedValue switch
    {
        sbyte number => FromInt64(number),
        byte number => FromInt64(number),
        short number => FromInt64(number),
        ushort number => FromInt64(number),
        int number => FromInt64(number),
        uint number => FromInt64(number),
        long number => FromInt64(number),
        decimal number => FromCurrency(number),
        _ => FromDouble((double)value)
    };

    private static Extended FromCurrency(decimal value)
    {
        int[] bits = decimal.GetBits(value);
        BigInteger numerator = (uint)bits[0] +
            ((BigInteger)(uint)bits[1] << 32) +
            ((BigInteger)(uint)bits[2] << 64);
        int scale = (bits[3] >> 16) & 0xFF;
        return Quantize(numerator, BigInteger.Pow(10, scale), 0, bits[3] < 0);
    }
}
