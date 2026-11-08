using System.Diagnostics;
using System.Text;
using FireURQ.Porter.Translation;

string directory = Path.Combine(
    Path.GetTempPath(), $"FireURQ.AnsiStringBuffers.{Guid.NewGuid():N}");
Directory.CreateDirectory(directory);
var encoding = new UTF8Encoding(false);

File.WriteAllText(Path.Combine(directory, "Probe.csproj"), """
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
  </PropertyGroup>
</Project>
""", encoding);

File.WriteAllText(Path.Combine(directory, "Objects.cs"),
    CSharpObjectRuntime.Source, encoding);
File.WriteAllText(Path.Combine(directory, "Streams.cs"),
    CSharpStreamRuntime.Source, encoding);
File.WriteAllText(Path.Combine(directory, "AnsiStrings.cs"),
    CSharpAnsiStringRuntime.Source, encoding);
File.WriteAllText(Path.Combine(directory, "AnsiStringStreams.cs"),
    CSharpAnsiStringStreamRuntime.Source, encoding);

File.WriteAllText(Path.Combine(directory, "Program.cs"), """
using System.Runtime.InteropServices;
using FireURQ.Runtime;

int passed = 0;
int failed = 0;
void Check(string name, bool condition)
{
    Console.WriteLine($"[{(condition ? "PASS" : "FAIL")}] {name}");
    if (condition)
        passed++;
    else
        failed++;
}

using (var empty = new AnsiStringBuffer(0))
{
    Check("Empty byte buffer exposes a null pointer",
        empty.Pointer == IntPtr.Zero && empty.GetPointer(0) == IntPtr.Zero);
    Check("Empty buffer snapshot is an empty value",
        empty.ToAnsiString() == AnsiString.Empty);
}

AnsiString original = AnsiString.FromBytes(new byte[] { 1, 2, 3, 4 });
var buffer = new AnsiStringBuffer(original);
IntPtr address = buffer.Pointer;
Check("Nonempty byte buffer exposes a native address", address != IntPtr.Zero);
Check("Byte buffer has an additional null terminator",
    Marshal.ReadByte(address, original.Length) == 0);

GC.Collect();
GC.WaitForPendingFinalizers();
Check("Pinned buffer address survives garbage collection",
    buffer.Pointer == address && Marshal.ReadByte(address, 2) == 3);

byte[] replacement = { 0, 128, 255, 65 };
Marshal.Copy(replacement, 0, buffer.GetPointer(4), replacement.Length);
AnsiString snapshot = buffer.ToAnsiString();
Check("Native writes preserve every transferred byte",
    snapshot.ToBytes().SequenceEqual(replacement));
Check("Native buffer writes do not mutate the original value",
    original.ToBytes().SequenceEqual(new byte[] { 1, 2, 3, 4 }));

Marshal.WriteByte(address, 0, 77);
Check("Snapshot is independent of later native writes",
    snapshot[1] == 0 && buffer.ToAnsiString()[1] == 77);
Check("Writing payload bytes preserves the trailing terminator",
    Marshal.ReadByte(address, 4) == 0);

bool pointerRangeRejected = false;
try
{
    buffer.GetPointer(5);
}
catch (ArgumentOutOfRangeException)
{
    pointerRangeRejected = true;
}
Check("Pointer access validates the requested byte count",
    pointerRangeRejected);

buffer.Dispose();
buffer.Dispose();
Check("Dispose is repeatable and resets the exposed pointer",
    buffer.Pointer == IntPtr.Zero);

var stream = TMemoryStream.Create();
var shortStream = TMemoryStream.Create();
var output = TMemoryStream.Create();

try
{
    stream.WriteBuffer(replacement, replacement.Length);
    stream.Position = 0;

    AnsiString destination = AnsiString.Empty.WithLength(4);
    AnsiString oldDestination = destination;
    int read = AnsiStringStreams.Read(stream, ref destination, 4);
    Check("Stream Read updates the destination and returns its byte count",
        read == 4 && destination.ToBytes().SequenceEqual(replacement));
    Check("Stream Read preserves a previously copied value",
        oldDestination.ToBytes().SequenceEqual(new byte[4]));

    stream.Position = 0;
    destination = AnsiString.FromBytes(new byte[] { 9, 9, 9, 9 });
    read = AnsiStringStreams.Read(stream, ref destination, 2);
    Check("Partial-count Read preserves the unused destination suffix",
        read == 2 &&
        destination.ToBytes().SequenceEqual(new byte[] { 0, 128, 9, 9 }));

    shortStream.WriteBuffer(new byte[] { 7, 8 }, 2);
    shortStream.Position = 0;
    destination = AnsiString.FromBytes(new byte[] { 9, 9, 9, 9 });
    read = AnsiStringStreams.Read(shortStream, ref destination, 4);
    Check("Short Read preserves transferred bytes and the remaining suffix",
        read == 2 &&
        destination.ToBytes().SequenceEqual(new byte[] { 7, 8, 9, 9 }));

    shortStream.Position = 0;
    destination = AnsiString.FromBytes(new byte[] { 9, 9, 9, 9 });
    bool eof = false;
    try
    {
        AnsiStringStreams.ReadBuffer(shortStream, ref destination, 4);
    }
    catch (EndOfStreamException)
    {
        eof = true;
    }
    Check("Incomplete ReadBuffer reports EOF after preserving received bytes",
        eof &&
        destination.ToBytes().SequenceEqual(new byte[] { 7, 8, 9, 9 }));

    stream.Position = 0;
    destination = AnsiString.Empty.WithLength(4);
    AnsiStringStreams.ReadBuffer(stream, ref destination, 4);
    Check("Complete ReadBuffer updates all destination bytes",
        destination.ToBytes().SequenceEqual(replacement));

    stream.Position = 0;
    bool oversizedReadRejected = false;
    try
    {
        AnsiStringStreams.Read(stream, ref destination, 5);
    }
    catch (ArgumentOutOfRangeException)
    {
        oversizedReadRejected = true;
    }
    Check("Oversized Read is rejected before consuming stream data",
        oversizedReadRejected && stream.Position == 0 &&
        destination.ToBytes().SequenceEqual(replacement));

    AnsiString source = AnsiString.FromBytes(replacement);
    Check("Stream Write returns the exact transferred count",
        AnsiStringStreams.Write(output, source, source.Length) == 4);
    Check("Stream Write preserves zeros and high byte values",
        output.ToArray().SequenceEqual(replacement));

    output.Clear();
    AnsiStringStreams.WriteBuffer(output, source, 2);
    Check("WriteBuffer respects its requested byte count",
        output.ToArray().SequenceEqual(new byte[] { 0, 128 }));

    bool oversizedWriteRejected = false;
    try
    {
        AnsiStringStreams.WriteBuffer(output, source, 5);
    }
    catch (ArgumentOutOfRangeException)
    {
        oversizedWriteRejected = true;
    }
    Check("Oversized WriteBuffer leaves the output unchanged",
        oversizedWriteRejected &&
        output.ToArray().SequenceEqual(new byte[] { 0, 128 }));

    AnsiString emptyValue = default;
    Check("Zero-byte Read accepts an empty destination",
        AnsiStringStreams.Read(stream, ref emptyValue, 0) == 0 &&
        emptyValue.Length == 0);
    Check("Zero-byte Write accepts an empty value",
        AnsiStringStreams.Write(output, emptyValue, 0) == 0 &&
        output.Size == 2);
}
finally
{
    TObject.Free(stream);
    TObject.Free(shortStream);
    TObject.Free(output);
}

Console.WriteLine($"AnsiString-buffer checks: {passed}/{passed + failed} passed.");
return failed == 0 ? 0 : 1;
""", encoding);

var start = new ProcessStartInfo
{
    FileName = "dotnet",
    WorkingDirectory = directory,
    UseShellExecute = false,
    RedirectStandardOutput = true,
    RedirectStandardError = true
};
foreach (string argument in new[]
{
    "run", "--project", Path.Combine(directory, "Probe.csproj"),
    "--configuration", "Release", "--verbosity", "quiet"
})
    start.ArgumentList.Add(argument);

using var process = Process.Start(start);
if (process is null)
{
    Console.WriteLine("[FAIL] Generated project could not start");
    Environment.ExitCode = 1;
}
else
{
    Task<string> output = process.StandardOutput.ReadToEndAsync();
    Task<string> errors = process.StandardError.ReadToEndAsync();
    process.WaitForExit();
    Console.Write(await output);
    Console.Write(await errors);
    Environment.ExitCode = process.ExitCode;
}
Console.WriteLine($"Generated compilation directory: {directory}");