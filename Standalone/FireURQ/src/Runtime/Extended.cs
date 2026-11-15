using System;
using System.Numerics;

namespace FireURQ.Runtime;

// Finite Win32 Delphi Extended arithmetic. The value is significand * 2^exponent.
internal readonly struct Extended
{
    private readonly ulong significand;
    private readonly int exponent;
    private readonly bool negative;

    private Extended(ulong significand, int exponent, bool negative)
    {
        this.significand = significand;
        this.exponent = exponent;
        this.negative = negative;
    }

    internal static Extended FromInt64(long value) =>
        Quantize(BigInteger.Abs(new BigInteger(value)), BigInteger.One, 0, value < 0);

    internal static Extended FromDouble(double value)
    {
        ulong bits = unchecked((ulong)BitConverter.DoubleToInt64Bits(value));
        ulong fraction = bits & 0x000F_FFFF_FFFF_FFFF;
        int biasedExponent = (int)((bits >> 52) & 0x7FF);
        bool sign = (bits >> 63) != 0;
        if (biasedExponent == 0x7FF)
            throw new ArgumentOutOfRangeException(nameof(value), "A finite value is required.");

        ulong magnitude = biasedExponent == 0 ? fraction : fraction | (1UL << 52);
        int power = biasedExponent == 0 ? -1074 : biasedExponent - 1023 - 52;
        return Quantize(magnitude, BigInteger.One, power, sign);
    }

    internal double ToDouble()
    {
        ulong sign = negative ? 1UL << 63 : 0;
        if (significand == 0)
            return FromBits(sign);

        int top = Log2(significand, BigInteger.One) + exponent;
        if (top > 1023)
            return FromBits(sign | 0x7FF0_0000_0000_0000);
        int unit = Math.Max(top - 52, -1074);
        ulong rounded = (ulong)RoundRatio(significand, BigInteger.One, exponent - unit);
        if (rounded == 1UL << 53)
        {
            rounded >>= 1;
            unit++;
        }
        if (rounded >= 1UL << 52)
        {
            int unbiased = unit + 52;
            if (unbiased > 1023)
                return FromBits(sign | 0x7FF0_0000_0000_0000);
            return FromBits(sign | ((ulong)(unbiased + 1023) << 52) |
                (rounded & 0x000F_FFFF_FFFF_FFFF));
        }

        return FromBits(sign | rounded);
    }

    private static double FromBits(ulong bits) =>
        BitConverter.Int64BitsToDouble(unchecked((long)bits));

    public static Extended operator -(Extended value) =>
        new(value.significand, value.exponent, !value.negative);

    public static Extended operator +(Extended left, Extended right)
    {
        if (left.significand == 0 && right.significand == 0)
            return new Extended(0, 0, left.negative && right.negative);
        if (left.significand == 0)
            return right;
        if (right.significand == 0)
            return left;

        int unit = Math.Min(left.exponent, right.exponent);
        BigInteger sum = left.SignedAt(unit) + right.SignedAt(unit);
        return Quantize(BigInteger.Abs(sum), BigInteger.One, unit, sum.Sign < 0);
    }

    public static Extended operator -(Extended left, Extended right) => left + -right;

    public static Extended operator *(Extended left, Extended right) =>
        Quantize((BigInteger)left.significand * right.significand, BigInteger.One,
            left.exponent + right.exponent, left.negative ^ right.negative);

    public static Extended operator /(Extended left, Extended right)
    {
        if (right.significand == 0)
            throw new DivideByZeroException();

        return Quantize(left.significand, right.significand,
            left.exponent - right.exponent, left.negative ^ right.negative);
    }

    private BigInteger SignedAt(int unit)
    {
        BigInteger magnitude = (BigInteger)significand << (exponent - unit);
        return negative ? -magnitude : magnitude;
    }

    private static Extended Quantize(BigInteger numerator, BigInteger denominator, int power, bool sign)
    {
        if (numerator.IsZero)
            return new Extended(0, 0, sign);

        int top = Log2(numerator, denominator) + power;
        if (top > 16383)
            throw new OverflowException("Extended exponent overflow.");
        int unit = Math.Max(top - 63, -16445);
        BigInteger rounded = RoundRatio(numerator, denominator, power - unit);
        if (rounded.GetBitLength() > 64)
        {
            rounded >>= 1;
            unit++;
        }
        if (unit > 16320)
            throw new OverflowException("Extended exponent overflow.");

        return new Extended((ulong)rounded, unit, sign);
    }

    private static int Log2(BigInteger numerator, BigInteger denominator)
    {
        int difference = checked((int)(numerator.GetBitLength() - denominator.GetBitLength()));
        bool below = difference >= 0
            ? numerator < (denominator << difference)
            : (numerator << -difference) < denominator;
        return below ? difference - 1 : difference;
    }

    private static BigInteger RoundRatio(BigInteger numerator, BigInteger denominator, int shift)
    {
        if (shift >= 0)
            numerator <<= shift;
        else
            denominator <<= -shift;

        BigInteger quotient = BigInteger.DivRem(numerator, denominator, out BigInteger remainder);
        int half = (remainder << 1).CompareTo(denominator);
        return half > 0 || (half == 0 && !quotient.IsEven) ? quotient + 1 : quotient;
    }
}
