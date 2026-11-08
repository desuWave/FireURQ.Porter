using System.Diagnostics;
using FireURQ.Porter.Translation;

string directory = Path.Combine(
    Path.GetTempPath(), "FireURQ.FileStreams." +
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

string directory = Path.Combine(
    Path.GetTempPath(), "FireURQ.FileData." + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(directory);
string path = Path.Combine(directory, "дані.bin");
byte[] original = { 0, 1, 127, 128, 255 };

var created = TFileStream.Create(path, fmCreate);
created.WriteBuffer(original, original.Length);
Check("Create writes exact bytes", created.Size == original.Length);
created.Free();

var reader = TFileStream.Create(path, fmOpenRead);
byte[] read = new byte[original.Length];
reader.ReadBuffer(read, read.Length);
Check("Read preserves bytes and Unicode file path",
    read.SequenceEqual(original) && reader.FileName == path);
bool writeDenied = false;
try
{
    reader.WriteBuffer(new byte[] { 1 }, 1);
}
catch (NotSupportedException)
{
    writeDenied = true;
}
Check("Read-only stream rejects writing", writeDenied);
reader.Free();

var writer = TFileStream.Create(path, fmOpenWrite);
writer.Position = 1;
writer.WriteBuffer(new byte[] { 42 }, 1);
writer.Free();
byte[] modified = File.ReadAllBytes(path);
Check("OpenWrite changes bytes without truncating existing storage",
    modified.Length == original.Length &&
    modified[0] == 0 && modified[1] == 42 && modified[4] == 255);

var append = TFileStream.Create(path, fmOpenReadWrite);
append.Seek(0, soFromEnd);
append.WriteBuffer(new byte[] { 99 }, 1);
append.Free();
Check("Seek from end supports append", File.ReadAllBytes(path).Last() == 99 &&
    new FileInfo(path).Length == original.Length + 1);

if (OperatingSystem.IsWindows())
{
    var sharedReader = TFileStream.Create(
        path, fmOpenRead | fmShareDenyWrite);
    var secondReader = TFileStream.Create(
        path, fmOpenRead | fmShareDenyWrite);
    Check("DenyWrite permits another reader", secondReader.Size > 0);
    secondReader.Free();

    bool writerBlocked = false;
    try
    {
        var blocked = TFileStream.Create(
            path, fmOpenWrite | fmShareDenyNone);
        blocked.Free();
    }
    catch (IOException)
    {
        writerBlocked = true;
    }
    Check("DenyWrite blocks another writer", writerBlocked);
    sharedReader.Free();

    var exclusive = TFileStream.Create(path, fmOpenRead | fmShareExclusive);
    bool readerBlocked = false;
    try
    {
        var blocked = TFileStream.Create(path, fmOpenRead | fmShareDenyNone);
        blocked.Free();
    }
    catch (IOException)
    {
        readerBlocked = true;
    }
    Check("Exclusive sharing blocks another reader", readerBlocked);
    exclusive.Free();

    var firstShared = TFileStream.Create(path, fmOpenReadWrite | fmShareDenyNone);
    var secondShared = TFileStream.Create(path, fmOpenReadWrite | fmShareDenyNone);
    Check("DenyNone permits another read-write opener", secondShared.Size > 0);
    secondShared.Free();
    firstShared.Free();

    var rightsArgument = TFileStream.Create(path, fmOpenRead, fmShareDenyNone);
    bool rightsDidNotChangeSharing = false;
    try
    {
        var blocked = TFileStream.Create(path, fmOpenRead | fmShareDenyNone);
        blocked.Free();
    }
    catch (IOException)
    {
        rightsDidNotChangeSharing = true;
    }
    Check("Third Rights argument does not change Windows sharing",
        rightsDidNotChangeSharing);
    rightsArgument.Free();
}
else
{
    Console.WriteLine("[SKIP] Windows file-sharing checks require Windows.");
}

var replacement = TFileStream.Create(path, fmCreate);
Check("Create truncates an existing file", replacement.Size == 0);
replacement.Free();

bool missingRejected = false;
try
{
    var missing = TFileStream.Create(
        Path.Combine(directory, "missing.bin"), fmOpenRead);
    missing.Free();
}
catch (FileNotFoundException)
{
    missingRejected = true;
}
Check("OpenRead does not create a missing file", missingRejected);

File.Delete(path);
Check("Free releases the file handle", !File.Exists(path));
Directory.Delete(directory);

Console.WriteLine($"File-stream checks: {total - failed}/{total} passed.");
return failed == 0 ? 0 : 1;
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
    Console.WriteLine("[FAIL] Could not start file stream test.");
    return 1;
}
process.WaitForExit();
Console.WriteLine("Generated compilation directory: " + directory);
return process.ExitCode;