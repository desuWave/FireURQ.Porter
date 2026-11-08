using System.Diagnostics;
using System.Text;
using FireURQ.Porter.Analysis;
using FireURQ.Porter.Lexer;
using FireURQ.Porter.Parser;
using FireURQ.Porter.Translation;

int total = 0;
int failed = 0;
void Check(string name, bool condition)
{
    total++;
    if (!condition)
        failed++;
    Console.WriteLine($"[{(condition ? "PASS" : "FAIL")}] {name}");
}

string directory = Path.Combine(
    Path.GetTempPath(), $"FireURQ.ProjectAnsiString.{Guid.NewGuid():N}");
string inputRoot = Path.Combine(directory, "Input");
string outputRoot = Path.Combine(directory, "Generated");
Directory.CreateDirectory(inputRoot);
var encoding = new UTF8Encoding(false);

ProjectTranslationInput BuildInput(string name, string source)
{
    string file = Path.Combine(inputRoot, name + ".pas");
    File.WriteAllText(file, source, encoding);
    var unit = new DelphiParser(
        new DelphiLexer(source).Tokenize()).ParseUnit();
    var model = SemanticModelBuilder.Build(unit);
    var bodies = BodyTokenAnalyzer.Analyze(unit, file)
        .Where(body => body.BodyTokens.Count > 0)
        .Select(body => (
            Body: body,
            Behavior: BehaviorAnalyzer.Analyze(
                body.FilePath, body.Name, body.BodyTokens)))
        .ToList();
    return new ProjectTranslationInput(
        inputRoot, file, unit, model, bodies);
}

var input = BuildInput("AnsiStringProject", """
unit AnsiStringProject;
interface
implementation

function ReadAll(stream: TStream; count: Integer): AnsiString;
begin
  SetLength(Result, count);
  stream.ReadBuffer(PChar(Result)^, count);
end;

procedure WriteAll(stream: TStream; const value: AnsiString);
begin
  stream.WriteBuffer(PAnsiChar(value)^, Length(value));
end;

procedure ResizeOnly(var value: ansistring; count: Integer);
begin
  SetLength(value, count);
end;

end.
""");

var results = new ProjectTranslator().Translate(new[] { input }, outputRoot);
Check("AnsiString project translation succeeds",
    results.Count == 1 && results.All(result => result.Success));
Check("Byte-string runtime is emitted automatically",
    File.Exists(Path.Combine(outputRoot, "__runtime", "AnsiStrings.cs")));
Check("Stream and byte-string adapter are emitted automatically",
    File.Exists(Path.Combine(outputRoot, "__runtime", "Streams.cs")) &&
    File.Exists(Path.Combine(outputRoot, "__runtime", "AnsiStringStreams.cs")));

string generated = File.ReadAllText(
    Path.Combine(outputRoot, "Input", "AnsiStringProject.cs"));
Check("All three implementation bodies are emitted",
    generated.Contains("ReadAll(") &&
    generated.Contains("WriteAll(") &&
    generated.Contains("ResizeOnly("));
Check("Mixed-case AnsiString type is normalized",
    generated.Contains("ResizeOnly(ref AnsiString value"));

var byteOnly = BuildInput("ByteOnlyProject", """
unit ByteOnlyProject;
interface
implementation
function ByteCount(value: AnsiString): Integer;
begin
  Result := Length(value);
end;
end.
""");
string byteOutput = Path.Combine(directory, "ByteOnlyGenerated");
var byteResults = new ProjectTranslator().Translate(new[] { byteOnly }, byteOutput);
Check("Byte-only project receives the byte-string runtime",
    byteResults.All(result => result.Success) &&
    File.Exists(Path.Combine(byteOutput, "__runtime", "AnsiStrings.cs")));
Check("Byte-only project does not receive stream dependencies",
    !File.Exists(Path.Combine(byteOutput, "__runtime", "Streams.cs")) &&
    !File.Exists(Path.Combine(byteOutput, "__runtime", "AnsiStringStreams.cs")));

var plain = BuildInput("PlainAnsiProbe", """
unit PlainAnsiProbe;
interface
implementation
function Number: Integer;
begin
  Result := 1;
end;
end.
""");
string plainOutput = Path.Combine(directory, "PlainGenerated");
var plainResults = new ProjectTranslator().Translate(new[] { plain }, plainOutput);
Check("Plain project does not receive byte-string support",
    plainResults.All(result => result.Success) &&
    !File.Exists(Path.Combine(plainOutput, "__runtime", "AnsiStrings.cs")) &&
    !File.Exists(Path.Combine(plainOutput, "__runtime", "AnsiStringStreams.cs")));

var shadow = BuildInput("ShadowAnsiProbe", """
unit ShadowAnsiProbe;
interface
type
  AnsiString = class
  end;
implementation
end.
""");
string shadowOutput = Path.Combine(directory, "ShadowGenerated");
var shadowResults = new ProjectTranslator().Translate(new[] { shadow }, shadowOutput);
Check("Project-defined AnsiString does not enable standard byte-string support",
    shadowResults.All(result => result.Success) &&
    !File.Exists(Path.Combine(shadowOutput, "__runtime", "AnsiStrings.cs")));

if (failed == 0)
{
    string project = Path.Combine(directory, "Probe.csproj");
    File.WriteAllText(project, """
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="Generated/**/*.cs" />
    <Compile Include="Probe.cs" />
  </ItemGroup>
</Project>
""", encoding);

    File.WriteAllText(Path.Combine(directory, "Probe.cs"), """
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

byte[] expected = Enumerable.Range(0, 256).Select(value => (byte)value).ToArray();
var source = TMemoryStream.Create();
var output = TMemoryStream.Create();

try
{
    source.WriteBuffer(expected, expected.Length);
    source.Position = 0;
    AnsiString value = AnsiStringProjectModule.ReadAll(source, expected.Length);
    Check("Project-generated read preserves all 256 bytes",
        value.ToBytes().SequenceEqual(expected));

    AnsiStringProjectModule.WriteAll(output, value);
    Check("Project-generated write preserves every byte",
        output.ToArray().SequenceEqual(expected));

    AnsiString previous = value;
    AnsiStringProjectModule.ResizeOnly(ref value, 3);
    Check("Project-generated resize retains the selected byte prefix",
        value.ToBytes().SequenceEqual(new byte[] { 0, 1, 2 }));
    Check("Project-generated resize preserves a previous copy",
        previous.ToBytes().SequenceEqual(expected));

    source.Position = source.Size - 2;
    bool eof = false;
    try
    {
        _ = AnsiStringProjectModule.ReadAll(source, 4);
    }
    catch (EndOfStreamException)
    {
        eof = true;
    }
    Check("Project-generated ReadBuffer reports incomplete input", eof);
}
finally
{
    TObject.Free(source);
    TObject.Free(output);
}

Console.WriteLine($"Project AnsiString execution: {passed}/{passed + failed} passed.");
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
        "run", "--project", project,
        "--configuration", "Release", "--verbosity", "quiet"
    })
        start.ArgumentList.Add(argument);

    using var process = Process.Start(start);
    if (process is null)
    {
        Check("Translated AnsiString project compiles and executes", false);
    }
    else
    {
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> errors = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        Console.Write(await output);
        Console.Write(await errors);
        Check("Translated AnsiString project compiles and executes",
            process.ExitCode == 0);
    }
}

Console.WriteLine($"Project-AnsiString checks: {total - failed}/{total} passed.");
Console.WriteLine($"Generated compilation directory: {directory}");
Environment.ExitCode = failed == 0 ? 0 : 1;