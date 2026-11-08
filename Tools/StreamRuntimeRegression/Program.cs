using System.Diagnostics;
using FireURQ.Porter.Translation;

string directory = Path.Combine(
    Path.GetTempPath(), "FireURQ.StreamRuntime." +
    Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(directory);
File.WriteAllText(
    Path.Combine(directory, "TObject.cs"), CSharpObjectRuntime.Source);
File.WriteAllText(
    Path.Combine(directory, "Streams.cs"), CSharpStreamRuntime.Source);
string project = Path.Combine(directory, "Probe.csproj");
File.WriteAllText(project, """
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
</Project>
""");
File.WriteAllText(Path.Combine(directory, "Program.cs"), """
using FireURQ.Runtime;
using static FireURQ.Runtime.DelphiStreamConstants;

int total = 0;
int failed = 0;
void Check(string name, bool passed)
{
    total++;
    if (!passed) failed++;
    Console.WriteLine($"[{(passed ? "PASS" : "FAIL")}] {name}");
}

byte[] data = Enumerable.Range(0, 256).Select(value => (byte)value).ToArray();
var memory = TMemoryStream.Create();
Check("New memory stream is empty", memory.Size == 0 && memory.Position == 0);
Check("Write reports the byte count", memory.Write(data, data.Length) == 256);
Check("All byte values are preserved without text conversion",
    memory.Size == 256 && memory.Position == 256 &&
    memory.ToArray().SequenceEqual(data));

memory.Seek(-4, soFromEnd);
byte[] tail = new byte[8];
Check("Read permits a short result at EOF",
    memory.Read(tail, tail.Length) == 4 &&
    tail.Take(4).SequenceEqual(data.Skip(252)));
Check("Read returns zero at EOF", memory.Read(tail, 1) == 0);
memory.Seek(10, soFromBeginning);
Check("Relative Seek advances from the current position",
    memory.Seek(2, soFromCurrent) == 12);
memory.SetSize(5);
Check("Memory truncation clamps the current position",
    memory.Size == 5 && memory.Position == 5);
memory.Clear();
Check("Clear resets size and position", memory.Size == 0 && memory.Position == 0);

var source = new TMemoryStream();
source.WriteBuffer(data, data.Length);
source.Position = 100;
var fullCopy = new TMemoryStream();
Check("CopyFrom with zero copies the whole source from its beginning",
    fullCopy.CopyFrom(source, 0) == 256 &&
    fullCopy.ToArray().SequenceEqual(data) && source.Position == 256);

source.Position = 10;
var partialCopy = new TMemoryStream();
Check("Positive CopyFrom begins at the source current position",
    partialCopy.CopyFrom(source, 3) == 3 &&
    partialCopy.ToArray().SequenceEqual(new byte[] { 10, 11, 12 }) &&
    source.Position == 13);

var shortNative = new ShortReadStream(data);
var shortAdapter = new TManagedStream(shortNative);
byte[] exact = new byte[256];
shortAdapter.ReadBuffer(exact, exact.Length);
Check("ReadBuffer handles repeated short underlying reads",
    exact.SequenceEqual(data));

bool eofDetected = false;
try
{
    shortAdapter.ReadBuffer(new byte[1], 1);
}
catch (EndOfStreamException)
{
    eofDetected = true;
}
Check("ReadBuffer detects incomplete data", eofDetected);

var borrowedNative = new MemoryStream();
var borrowed = new TManagedStream(borrowedNative, false);
borrowed.Free();
Check("Borrowed stream remains open after wrapper destruction",
    borrowedNative.CanWrite);
borrowedNative.Dispose();

var ownedNative = new MemoryStream();
var owned = new TManagedStream(ownedNative, true);
owned.Free();
Check("Owned stream closes after wrapper destruction",
    !ownedNative.CanWrite);

var nativeMemory = new TMemoryStream();
Check("Empty memory stream exposes a null pointer",
    nativeMemory.Memory == IntPtr.Zero);
nativeMemory.SetSize(32);
IntPtr address = nativeMemory.Memory;
Check("Allocated memory exposes a native address",
    address != IntPtr.Zero);

GC.Collect();
GC.WaitForPendingFinalizers();
GC.Collect();
System.Runtime.InteropServices.Marshal.WriteByte(address, 5, 200);
Check("Native memory remains valid across garbage collection",
    nativeMemory.ToArray()[5] == 200);

nativeMemory.Position = 0;
nativeMemory.WriteBuffer(new byte[] { 123 }, 1);
Check("Writes within capacity preserve the pinned address",
    nativeMemory.Memory == address &&
    System.Runtime.InteropServices.Marshal.ReadByte(address) == 123);

nativeMemory.Position = 32;
nativeMemory.WriteBuffer(new byte[65536], 65536);
IntPtr grownAddress = nativeMemory.Memory;
Check("Growth preserves contents and exposes the current buffer",
    grownAddress != IntPtr.Zero &&
    nativeMemory.Size == 65568 &&
    System.Runtime.InteropServices.Marshal.ReadByte(grownAddress) == 123 &&
    System.Runtime.InteropServices.Marshal.ReadByte(grownAddress, 5) == 200);

nativeMemory.Clear();
Check("Clear releases the buffer and resets its pointer",
    nativeMemory.Size == 0 && nativeMemory.Position == 0 &&
    nativeMemory.Memory == IntPtr.Zero);
nativeMemory.Free();

var scalars = new TMemoryStream();
scalars.WriteBuffer(true, 1);
scalars.WriteBuffer(0x12345678, 4);
Check("Scalar Boolean occupies one byte",
    scalars.ToArray().SequenceEqual(
        new byte[] { 1, 0x78, 0x56, 0x34, 0x12 }));

scalars.Position = 0;
bool readBoolean = false;
int readInteger = 0;
scalars.ReadBuffer(ref readBoolean, 1);
scalars.ReadBuffer(ref readInteger, 4);
Check("Scalar ReadBuffer updates its destination",
    readBoolean && readInteger == 0x12345678);

bool oversizedScalarRejected = false;
try
{
    scalars.ReadBuffer(ref readInteger, 8);
}
catch (ArgumentOutOfRangeException)
{
    oversizedScalarRejected = true;
}
Check("Scalar buffer size prevents an overrun", oversizedScalarRejected);
scalars.Free();

IntPtr nativeBuffer =
    System.Runtime.InteropServices.Marshal.AllocHGlobal(8);
try
{
    byte[] nativeData = { 0, 127, 128, 255, 4, 5, 6, 7 };
    System.Runtime.InteropServices.Marshal.Copy(
        nativeData, 0, nativeBuffer, nativeData.Length);

    var pointerStream = new TMemoryStream();
    pointerStream.WriteBuffer(nativeBuffer, nativeData.Length);
    Check("Pointer WriteBuffer preserves every byte",
        pointerStream.ToArray().SequenceEqual(nativeData));

    System.Runtime.InteropServices.Marshal.Copy(
        new byte[8], 0, nativeBuffer, 8);
    pointerStream.Position = 0;
    pointerStream.ReadBuffer(nativeBuffer, 8);
    byte[] nativeRead = new byte[8];
    System.Runtime.InteropServices.Marshal.Copy(
        nativeBuffer, nativeRead, 0, 8);
    Check("Pointer ReadBuffer updates native memory",
        nativeRead.SequenceEqual(nativeData));
    Check("Zero-byte pointer operations accept a null address",
        pointerStream.Read(IntPtr.Zero, 0) == 0 &&
        pointerStream.Write(IntPtr.Zero, 0) == 0);
    pointerStream.Free();

    var incomplete = new TMemoryStream();
    incomplete.WriteBuffer(new byte[] { 1, 2, 3 }, 3);
    incomplete.Position = 0;
    System.Runtime.InteropServices.Marshal.Copy(
        new byte[8], 0, nativeBuffer, 8);
    bool partialReadDetected = false;
    try
    {
        incomplete.ReadBuffer(nativeBuffer, 8);
    }
    catch (EndOfStreamException)
    {
        partialReadDetected = true;
    }
    System.Runtime.InteropServices.Marshal.Copy(
        nativeBuffer, nativeRead, 0, 8);
    Check("Incomplete native read reports EOF after transferring available bytes",
        partialReadDetected &&
        nativeRead.SequenceEqual(new byte[] { 1, 2, 3, 0, 0, 0, 0, 0 }));
    incomplete.Free();
}
finally
{
    System.Runtime.InteropServices.Marshal.FreeHGlobal(nativeBuffer);
}

memory.Free();
source.Free();
fullCopy.Free();
partialCopy.Free();
shortAdapter.Free();
Check("Owned short-read source is closed", !shortNative.CanRead);

Console.WriteLine($"Stream-runtime checks: {total - failed}/{total} passed.");
return failed == 0 ? 0 : 1;

sealed class ShortReadStream : MemoryStream
{
    public ShortReadStream(byte[] data) : base(data) { }

    public override int Read(byte[] buffer, int offset, int count) =>
        base.Read(buffer, offset, Math.Min(count, 1));

    public override int Read(Span<byte> buffer) =>
        base.Read(buffer[..Math.Min(buffer.Length, 1)]);
}
""");

var start = new ProcessStartInfo("dotnet") { UseShellExecute = false };
start.ArgumentList.Add("run");
start.ArgumentList.Add("--project");
start.ArgumentList.Add(project);
start.ArgumentList.Add("--configuration");
start.ArgumentList.Add("Release");
using var process = Process.Start(start);
if (process is null)
{
    Console.WriteLine("[FAIL] Could not start stream runtime test.");
    return 1;
}
process.WaitForExit();
Console.WriteLine("Generated compilation directory: " + directory);
return process.ExitCode;