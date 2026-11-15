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
Console.WriteLine("FireURQ finite Extended arithmetic checks passed.");

static void Check(string label, double actual, double expected)
{
    if (BitConverter.DoubleToInt64Bits(actual) != BitConverter.DoubleToInt64Bits(expected))
        throw new InvalidOperationException($"{label}: expected {expected:R}, found {actual:R}.");
}
