global using static FireURQ.Runtime.DelphiSystem;

namespace FireURQ.Runtime;

internal static class DelphiSystem
{
    public static int Length(string? value) => value?.Length ?? 0;
    public static int Length(AnsiString value) => value.Length;
    public static int Length<T>(T[]? value) => value?.Length ?? 0;

    public static sbyte Pred(sbyte value) => unchecked((sbyte)(value - 1));
    public static byte Pred(byte value) => unchecked((byte)(value - 1));
    public static short Pred(short value) => unchecked((short)(value - 1));
    public static ushort Pred(ushort value) => unchecked((ushort)(value - 1));
    public static int Pred(int value) => unchecked(value - 1);
    public static uint Pred(uint value) => unchecked(value - 1);
    public static long Pred(long value) => unchecked(value - 1);
    public static ulong Pred(ulong value) => unchecked(value - 1);
    public static char Pred(char value) => unchecked((char)(value - 1));

    public static int SizeOf<T>(T value)
    {
        if (value is Array array)
        {
            Type? element = value.GetType().GetElementType();
            return element is null ? 0 : checked(array.Length * System.Runtime.InteropServices.Marshal.SizeOf(element));
        }

        return System.Runtime.InteropServices.Marshal.SizeOf<T>();
    }

    public static int Ord(char value) => value;
    public static int Ord(bool value) => value ? 1 : 0;
    public static int Ord<T>(T value) where T : struct, Enum =>
        Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);

    public static int High<T>(T[] value) => (value?.Length ?? 0) - 1;
    public static int Low<T>(T[] value) => 0;

    public static void Assert(bool condition)
    {
        if (!condition)
            throw new InvalidOperationException("Delphi assertion failed.");
    }

    public static bool Failed(int result) => result < 0;

    public static double Int(double value) => Math.Truncate(value);
    public static float Int(float value) => MathF.Truncate(value);

    public static IntPtr PChar(string? value) =>
        System.Runtime.InteropServices.Marshal.StringToHGlobalUni(value ?? string.Empty);

    public static IntPtr PAnsiChar(string? value) =>
        System.Runtime.InteropServices.Marshal.StringToHGlobalAnsi(value ?? string.Empty);

    public static int InterlockedIncrement(ref int value) =>
        System.Threading.Interlocked.Increment(ref value);

    public static int InterlockedDecrement(ref int value) =>
        System.Threading.Interlocked.Decrement(ref value);
}
