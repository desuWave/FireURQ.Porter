using System.Diagnostics;
using FireURQ.Porter.Analysis;
using FireURQ.Porter.Lexer;
using FireURQ.Porter.Parser;
using FireURQ.Porter.Translation;

int total = 0;
int failed = 0;
void Check(string name, bool passed)
{
    total++;
    if (!passed) failed++;
    Console.WriteLine($"[{(passed ? "PASS" : "FAIL")}] {name}");
}

ProjectTranslationInput Prepare(string root, string name, string source)
{
    Directory.CreateDirectory(root);
    string path = Path.Combine(root, name + ".pas");
    File.WriteAllText(path, source);
    var unit = new DelphiParser(new DelphiLexer(source).Tokenize()).ParseUnit();
    var model = SemanticModelBuilder.Build(unit);
    var bodies = BodyTokenAnalyzer.Analyze(unit, path)
        .Where(body => body.BodyTokens.Count > 0)
        .Select(body => (
            Body: body,
            Behavior: BehaviorAnalyzer.Analyze(path, body.Name, body.BodyTokens)))
        .ToList();
    return new ProjectTranslationInput(root, path, unit, model, bodies);
}

string directory = Path.Combine(
    Path.GetTempPath(), "FireURQ.ProjectStreams." +
    Guid.NewGuid().ToString("N"));
string output = Path.Combine(directory, "Generated");
var input = Prepare(Path.Combine(directory, "Input"), "StreamProject", """
unit StreamProject;
interface
procedure WriteNumber(stream: TStream; value: Integer);
procedure ReadNumber(stream: TStream; var value: Integer);
procedure ReleaseMemory(value: TMemoryStream);
function JumpToLast(stream: TStream): Int64;
function OpenRead(const filename: string): TFileStream;
implementation
procedure WriteNumber(stream: TStream; value: Integer);
begin
  stream.WriteBuffer(value, 4);
end;
procedure ReadNumber(stream: TStream; var value: Integer);
begin
  stream.ReadBuffer(value, 4);
end;
procedure ReleaseMemory(value: TMemoryStream);
begin
  value.Free;
end;
function JumpToLast(stream: TStream): Int64;
begin
  Result := stream.Seek(-1, soFromEnd);
end;
function OpenRead(const filename: string): TFileStream;
begin
  Result := TFileStream.Create(filename, fmOpenRead or fmShareDenyNone);
end;
end.
""");
var results = new ProjectTranslator().Translate(new[] { input }, output);
Check("Stream project translation succeeds",
    results.Count == 1 && results[0].Success);
if (results.Count != 1 || !results[0].Success)
{
    foreach (var result in results) Console.WriteLine(result.Error);
    return 1;
}
Check("Stream support emitted automatically",
    File.Exists(Path.Combine(output, "__runtime", "Streams.cs")));
string generated = File.ReadAllText(results[0].OutputFile);
Check("Stream constants imported automatically",
    generated.Contains("using static global::FireURQ.Runtime.DelphiStreamConstants;"));
Check("Stream Free uses the null-safe helper",
    generated.Contains("global::FireURQ.Runtime.TObject.Free(value)"));
Check("All five implementation bodies emitted",
    results[0].FunctionsTranslated == 5);

string plainDirectory = Path.Combine(
    Path.GetTempPath(), "FireURQ.NoStreams." + Guid.NewGuid().ToString("N"));
string plainOutput = Path.Combine(plainDirectory, "Generated");
var plain = Prepare(Path.Combine(plainDirectory, "Input"), "PlainProject", """
unit PlainProject;
interface
function Value: Integer;
implementation
function Value: Integer;
begin
  Result := 42;
end;
end.
""");
var plainResults = new ProjectTranslator().Translate(new[] { plain }, plainOutput);
Check("Plain project translation succeeds",
    plainResults.Count == 1 && plainResults[0].Success);
Check("Plain project does not receive stream support",
    !File.Exists(Path.Combine(plainOutput, "__runtime", "Streams.cs")));
Check("Plain project does not import missing stream constants",
    plainResults.Count == 1 && plainResults[0].Success &&
    !File.ReadAllText(plainResults[0].OutputFile).Contains("DelphiStreamConstants"));

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

int failed = 0;
void Check(string name, bool passed)
{
    if (!passed) failed++;
    Console.WriteLine($"[{(passed ? "PASS" : "FAIL")}] {name}");
}

var stream = new TMemoryStream();
StreamProjectModule.WriteNumber(stream, 0x12345678);
Check("Project-generated write preserves bytes",
    stream.ToArray().SequenceEqual(new byte[] { 0x78, 0x56, 0x34, 0x12 }));
stream.Position = 0;
int number = 0;
StreamProjectModule.ReadNumber(stream, ref number);
Check("Project-generated read updates the caller", number == 0x12345678);
Check("Project-generated seek resolves its constant",
    StreamProjectModule.JumpToLast(stream) == 3);
StreamProjectModule.ReleaseMemory(stream);
StreamProjectModule.ReleaseMemory(null);
Console.WriteLine("[PASS] Project-generated Free accepts nil");

string path = Path.Combine(
    Path.GetTempPath(), "FireURQ.StreamProject." + Guid.NewGuid().ToString("N"));
File.WriteAllBytes(path, new byte[] { 10, 20, 30 });
var file = StreamProjectModule.OpenRead(path);
byte[] data = new byte[3];
file.ReadBuffer(data, 3);
Check("Project-generated file factory resolves open and sharing modes",
    data.SequenceEqual(new byte[] { 10, 20, 30 }));
file.Free();
File.Delete(path);
return failed == 0 ? 0 : 1;
""");

var start = new ProcessStartInfo("dotnet") { UseShellExecute = false };
start.ArgumentList.Add("run");
start.ArgumentList.Add("--project");
start.ArgumentList.Add(project);
start.ArgumentList.Add("--configuration");
start.ArgumentList.Add("Release");
using (var process = Process.Start(start))
{
    if (process is null)
        Check("Translated stream project compiles and executes", false);
    else
    {
        process.WaitForExit();
        Check("Translated stream project compiles and executes", process.ExitCode == 0);
    }
}
Console.WriteLine($"Project-stream checks: {total - failed}/{total} passed.");
Console.WriteLine("Generated compilation directory: " + directory);
return failed == 0 ? 0 : 1;