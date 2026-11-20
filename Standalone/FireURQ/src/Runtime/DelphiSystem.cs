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

    public static void Assert(bool condition) =>
        Assert(condition, "Delphi assertion failed.");

    public static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
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

    public static IntPtr Allocate<T>() where T : struct
    {
        IntPtr pointer = System.Runtime.InteropServices.Marshal.AllocHGlobal(
            System.Runtime.InteropServices.Marshal.SizeOf<T>());
        System.Runtime.InteropServices.Marshal.StructureToPtr(default(T), pointer, false);
        return pointer;
    }

    public static unsafe ref T Dereference<T>(IntPtr pointer) where T : struct =>
        ref System.Runtime.CompilerServices.Unsafe.AsRef<T>(pointer.ToPointer());

    public const int MaxInt = int.MaxValue;
    public const int varDate = 7;
    public const int rfReplaceAll = 1;
    public const int rfIgnoreCase = 2;

    public static Variant VarAsType(Variant value, int typeCode)
    {
        if (value.TypeCode == typeCode)
            return value;

        return typeCode switch
        {
            3 => Convert.ToInt32((double)value),
            5 => (double)value,
            7 when value.BoxedValue is string text => Variant.FromAutomationDate(
                DateTime.Parse(text, System.Globalization.CultureInfo.CurrentCulture).ToOADate()),
            7 => Variant.FromAutomationDate((double)value),
            11 => (double)value != 0,
            256 => (string)value,
            _ => throw new InvalidCastException(
                $"Cannot convert Delphi Variant type {value.TypeCode} to {typeCode}.")
        };
    }

    public static int Abs(int value) => Math.Abs(value);
    public static long Abs(long value) => Math.Abs(value);
    public static float Abs(float value) => MathF.Abs(value);
    public static double Abs(double value) => Math.Abs(value);

    public static void Sleep(uint milliseconds) =>
        System.Threading.Thread.Sleep(checked((int)milliseconds));

    public static void FillChar<T>(ref T value, int count, int fill) where T : struct
    {
        Span<byte> bytes = System.Runtime.InteropServices.MemoryMarshal.AsBytes(
            System.Runtime.InteropServices.MemoryMarshal.CreateSpan(ref value, 1));
        bytes[..Math.Min(Math.Max(count, 0), bytes.Length)].Fill(unchecked((byte)fill));
    }

    public static void FillChar<T>(T[] value, int count, int fill) where T : struct
    {
        Span<byte> bytes = System.Runtime.InteropServices.MemoryMarshal.AsBytes(value.AsSpan());
        bytes[..Math.Min(Math.Max(count, 0), bytes.Length)].Fill(unchecked((byte)fill));
    }

    public static void FillChar<T>(ref T[] value, int count, int fill) where T : struct =>
        FillChar(value, count, fill);

    public static void Move<TSource, TDestination>(
        ref TSource source, ref TDestination destination, int count)
        where TSource : struct where TDestination : struct
    {
        ReadOnlySpan<byte> sourceBytes = System.Runtime.InteropServices.MemoryMarshal.AsBytes(
            System.Runtime.InteropServices.MemoryMarshal.CreateReadOnlySpan(ref source, 1));
        Span<byte> destinationBytes = System.Runtime.InteropServices.MemoryMarshal.AsBytes(
            System.Runtime.InteropServices.MemoryMarshal.CreateSpan(ref destination, 1));
        sourceBytes[..Math.Min(Math.Max(count, 0), Math.Min(sourceBytes.Length, destinationBytes.Length))]
            .CopyTo(destinationBytes);
    }

    public static void Move<TSource, TDestination>(
        TSource[] source, TDestination[] destination, int count)
        where TSource : struct where TDestination : struct
    {
        ReadOnlySpan<byte> sourceBytes =
            System.Runtime.InteropServices.MemoryMarshal.AsBytes(source.AsSpan());
        Span<byte> destinationBytes =
            System.Runtime.InteropServices.MemoryMarshal.AsBytes(destination.AsSpan());
        sourceBytes[..Math.Min(Math.Max(count, 0), Math.Min(sourceBytes.Length, destinationBytes.Length))]
            .CopyTo(destinationBytes);
    }

    public static void Move<TSource, TDestination>(
        ref TSource source, TDestination[] destination, int count)
        where TSource : struct where TDestination : struct
    {
        ReadOnlySpan<byte> sourceBytes = System.Runtime.InteropServices.MemoryMarshal.AsBytes(
            System.Runtime.InteropServices.MemoryMarshal.CreateReadOnlySpan(ref source, 1));
        Span<byte> destinationBytes =
            System.Runtime.InteropServices.MemoryMarshal.AsBytes(destination.AsSpan());
        sourceBytes[..Math.Min(Math.Max(count, 0), Math.Min(sourceBytes.Length, destinationBytes.Length))]
            .CopyTo(destinationBytes);
    }

    public static void Move<TSource, TDestination>(
        TSource[] source, ref TDestination destination, int count)
        where TSource : struct where TDestination : struct
    {
        ReadOnlySpan<byte> sourceBytes =
            System.Runtime.InteropServices.MemoryMarshal.AsBytes(source.AsSpan());
        Span<byte> destinationBytes = System.Runtime.InteropServices.MemoryMarshal.AsBytes(
            System.Runtime.InteropServices.MemoryMarshal.CreateSpan(ref destination, 1));
        sourceBytes[..Math.Min(Math.Max(count, 0), Math.Min(sourceBytes.Length, destinationBytes.Length))]
            .CopyTo(destinationBytes);
    }

    public static IntPtr GetMemory(int size) =>
        System.Runtime.InteropServices.Marshal.AllocHGlobal(size);

    public static void FreeMem(IntPtr pointer, int size = 0)
    {
        if (pointer != IntPtr.Zero)
            System.Runtime.InteropServices.Marshal.FreeHGlobal(pointer);
    }

    public static void Dispose(IntPtr pointer) => FreeMem(pointer);
}
