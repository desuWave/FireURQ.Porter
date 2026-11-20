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
}
