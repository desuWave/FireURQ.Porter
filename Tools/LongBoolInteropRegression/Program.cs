using System.Runtime.InteropServices;

int total = 0;
int failed = 0;

void Check(string name, bool passed)
{
    total++;
    if (!passed) failed++;
    Console.WriteLine($"[{(passed ? "PASS" : "FAIL")}] {name}");
}

Check("Native LongBool field occupies four bytes",
    Marshal.SizeOf<LongBoolField>() == 4);

Check("Next field begins after four-byte LongBool",
    Marshal.OffsetOf<LongBoolRecord>(
        nameof(LongBoolRecord.Next)).ToInt64() == 4);

Check("Record native size is eight bytes",
    Marshal.SizeOf<LongBoolRecord>() == 8);

IntPtr memory = Marshal.AllocHGlobal(8);

try
{
    foreach (int raw in new[] { 0, 1, -1, 2, int.MinValue })
    {
        Marshal.WriteInt32(memory, 0, raw);
        Marshal.WriteInt32(memory, 4, 123456);

        var value = Marshal.PtrToStructure<LongBoolRecord>(memory);

        Check($"Native value {raw} becomes {raw != 0}",
            value.Value == (raw != 0) && value.Next == 123456);
    }

    foreach (bool value in new[] { false, true })
    {
        Marshal.StructureToPtr(
            new LongBoolRecord { Value = value, Next = 987654 },
            memory, false);

        int raw = Marshal.ReadInt32(memory);
        Check($"Managed {value} marshals with correct truth value",
            (raw != 0) == value &&
            Marshal.ReadInt32(memory, 4) == 987654);
    }
}
finally
{
    Marshal.FreeHGlobal(memory);
}

if (OperatingSystem.IsWindows())
{
    IntPtr library = NativeLibrary.Load("kernel32.dll");

    try
    {
        IntPtr address = NativeLibrary.GetExport(library, "IsValidCodePage");
        var isValidCodePage =
            Marshal.GetDelegateForFunctionPointer<CheckCodePage>(address);

        Check("Real Windows BOOL return: valid UTF-8 code page",
            isValidCodePage(65001));

        Check("Real Windows BOOL return: invalid code page",
            !isValidCodePage(uint.MaxValue));

        GC.KeepAlive(isValidCodePage);
    }
    finally
    {
        NativeLibrary.Free(library);
    }
}
else
{
    Console.WriteLine("[SKIP] Windows native-call checks");
}

Console.WriteLine($"LongBool interop checks: {total - failed}/{total} passed.");
return failed == 0 ? 0 : 1;

[StructLayout(LayoutKind.Sequential)]
public struct LongBoolField
{
    [MarshalAs(UnmanagedType.Bool)]
    public bool Value;
}

[StructLayout(LayoutKind.Sequential)]
public struct LongBoolRecord
{
    [MarshalAs(UnmanagedType.Bool)]
    public bool Value;
    public int Next;
}

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
[return: MarshalAs(UnmanagedType.Bool)]
public delegate bool CheckCodePage(uint codePage);
