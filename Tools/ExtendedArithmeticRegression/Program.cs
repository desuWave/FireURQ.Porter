using System;
using FireURQ.Runtime;

var one = Extended.FromInt64(1);
var large = Extended.FromInt64(9007199254740992);
Check("retain the integer above binary64 precision", ((large + one) - large).ToDouble(), 1);
Check("round to binary64 when explicitly requested", (large + one).ToDouble(), 9007199254740992);
var ulp = Extended.FromDouble(Math.ScaleB(1, -63));
var halfUlp = Extended.FromDouble(Math.ScaleB(1, -64));
Check("retain the lowest bit of a 64-bit significand", ((one + ulp) - one).ToDouble(), Math.ScaleB(1, -63));
Check("halfway rounds toward an even significand", ((one + halfUlp) - one).ToDouble(), 0);
Check("halfway above an odd significand rounds up", ((one + ulp + halfUlp) - one).ToDouble(), Math.ScaleB(1, -62));
Check("multiplication and division", (Extended.FromInt64(7) / Extended.FromInt64(4) * Extended.FromInt64(8)).ToDouble(), 14);
Check("binary64 minimum subnormal round-trip", Extended.FromDouble(double.Epsilon).ToDouble(), double.Epsilon);
Check("binary64 subnormal halfway rounds to zero", (Extended.FromDouble(double.Epsilon) / Extended.FromInt64(2)).ToDouble(), 0);
Check("negative zero round-trip", Extended.FromDouble(-0.0).ToDouble(), -0.0);
var singleMidpoint = one + Extended.FromDouble(Math.ScaleB(1, -24));
Check("round directly to Single above a midpoint", (float)(singleMidpoint + ulp), MathF.BitIncrement(1f));
if (!(large + one > large) || large + one == large)
    throw new InvalidOperationException("Extended comparison rounded to binary64 before comparing.");

if (Trunc(-1.75) != -1 || Round(-1.5) != -2 || Round(2.5) != 2 ||
    Round(-2.5) != -2 || Round(large + one) != 9007199254740993L ||
    Trunc(Extended.FromInt64(long.MinValue)) != long.MinValue)
    throw new InvalidOperationException("Trunc/Round lost precision or used the wrong rounding rule.");

try
{
    Round(Extended.FromInt64(long.MaxValue) + one);
    throw new InvalidOperationException("Round accepted a value outside Int64.");
}
catch (OverflowException) { }

#if TEST_VARIANT
Variant number = 9007199254740992.0;
Extended numericInput = Extended.EnsureReal(number);
Variant evaluated = numericInput + one;
Check("assign expression to varDouble", Extended.EnsureReal(evaluated).ToDouble(), 9007199254740992);
Check("EnsureReal uses the length of a string", Extended.EnsureReal((Variant)"42").ToDouble(), 2);
Check("EnsureReal widens a Delphi true value to minus one", Extended.EnsureReal((Variant)true).ToDouble(), -1);
Console.WriteLine("FireURQ Extended Variant boundary checks passed.");
#endif
Console.WriteLine("FireURQ finite Extended arithmetic checks passed.");

static void Check(string label, double actual, double expected)
{
    if (BitConverter.DoubleToInt64Bits(actual) != BitConverter.DoubleToInt64Bits(expected))
        throw new InvalidOperationException($"{label}: expected {expected:R}, found {actual:R}.");
}
