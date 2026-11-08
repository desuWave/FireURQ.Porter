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

const string source = """
unit AnsiStreamBinding;
interface
implementation

procedure Resize(var value: AnsiString; count: Integer);
begin
  SetLength(value, count);
end;

function ByteCount(value: AnsiString): Integer;
begin
  Result := Length(value);
end;

function ReadSome(stream: TStream; var value: AnsiString; count: Integer): Integer;
begin
  Result := stream.Read(PChar(value)^, count);
end;

procedure ReadExact(stream: TStream; var value: AnsiString; count: Integer);
begin
  stream.ReadBuffer(PAnsiChar(value)^, count);
end;

function WriteSome(stream: TStream; value: AnsiString; count: Integer): Integer;
begin
  Result := stream.Write(PAnsiChar(value)^, count);
end;

procedure WriteExact(stream: TStream; const value: AnsiString; count: Integer);
begin
  stream.WriteBuffer(PChar(value)^, count);
end;

function ReadNew(stream: TStream; count: Integer): AnsiString;
var
  buffer: AnsiString;
begin
  SetLength(buffer, count);
  stream.ReadBuffer(PChar(buffer)^, count);
  Result := buffer;
end;

function ReadResult(stream: TStream; count: Integer): AnsiString;
begin
  SetLength(Result, count);
  stream.ReadBuffer(PAnsiChar(Result)^, count);
end;

end.
""";

var unit = new DelphiParser(new DelphiLexer(source).Tokenize()).ParseUnit();
var model = SemanticModelBuilder.Build(unit);
var translator = new DelphiToCSharpTranslator(
    new TypeResolver(new[] { model }), model.Name);
var methods = new Dictionary<string, string>();

foreach (var body in BodyTokenAnalyzer.Analyze(unit, "AnsiStreamBinding.pas")
    .Where(value => value.BodyTokens.Count > 0))
{
    var function = FunctionImplementationMatcher.Match(unit, body, model);
    Check(body.Name + ": signature matched", function is not null);
    if (function is null)
        continue;

    methods[function.Name] = translator.TranslateFunction(
        function,
        BehaviorAnalyzer.Analyze(body.FilePath, body.Name, body.BodyTokens));
}

Check("All eight Pascal helpers are translated", methods.Count == 8);
Check("Local AnsiString is initialized before resizing",
    methods.TryGetValue("ReadNew", out var localRead) &&
    localRead.Contains("AnsiString buffer = default;"));
Check("AnsiString result is initialized before resizing",
    methods.TryGetValue("ReadResult", out var resultRead) &&
    resultRead.Contains("AnsiString result = default;"));

Console.WriteLine("=== Generated AnsiString stream helpers ===");
foreach (string method in methods.Values)
    Console.WriteLine(method);

if (failed == 0)
{
    string directory = Path.Combine(
        Path.GetTempPath(), $"FireURQ.GeneratedAnsiStreams.{Guid.NewGuid():N}");
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

    foreach (var item in new[]
    {
        (Name: "Objects.cs", Source: CSharpObjectRuntime.Source),
        (Name: "Streams.cs", Source: CSharpStreamRuntime.Source),
        (Name: "AnsiStrings.cs", Source: CSharpAnsiStringRuntime.Source),
        (Name: "AnsiStringStreams.cs", Source: CSharpAnsiStringStreamRuntime.Source)
    })
        File.WriteAllText(Path.Combine(directory, item.Name), item.Source, encoding);

    File.WriteAllText(Path.Combine(directory, "Generated.cs"),
        "using System;\nusing FireURQ.Runtime;\npublic static class Generated\n{\n" +
        string.Join("\n", methods.Values.Select(value => "public static " + value)) +
        "\n}\n", encoding);

    File.WriteAllText(Path.Combine(directory, "Program.cs"), """
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

var source = TMemoryStream.Create();
var output = TMemoryStream.Create();
byte[] bytes = Enumerable.Range(0, 256).Select(value => (byte)value).ToArray();

try
{
    source.WriteBuffer(bytes, bytes.Length);
    source.Position = 0;
    AnsiString value = default;
    Generated.Resize(ref value, bytes.Length);
    Check("Generated SetLength creates the requested byte buffer",
        value.Length == 256 && value.ToBytes().All(item => item == 0));
    Check("Generated Length returns the byte count",
        Generated.ByteCount(value) == 256);

    int count = Generated.ReadSome(source, ref value, bytes.Length);
    Check("Generated PChar Read preserves all 256 bytes",
        count == 256 && value.ToBytes().SequenceEqual(bytes));

    source.Position = 0;
    AnsiString oldValue = value;
    Generated.Resize(ref value, 4);
    Generated.ReadExact(source, ref value, 4);
    Check("Generated PAnsiChar ReadBuffer updates its destination",
        value.ToBytes().SequenceEqual(new byte[] { 0, 1, 2, 3 }));
    Check("Generated resizing and reading preserve a previous copy",
        oldValue.ToBytes().SequenceEqual(bytes));

    source.Position = 0;
    AnsiString localResult = Generated.ReadNew(source, bytes.Length);
    Check("Generated local SetLength and native read execute correctly",
        localResult.ToBytes().SequenceEqual(bytes));

    source.Position = 0;
    AnsiString directResult = Generated.ReadResult(source, 4);
    Check("Generated Result buffer is resized and filled",
        directResult.ToBytes().SequenceEqual(new byte[] { 0, 1, 2, 3 }));

    Check("Generated native Write returns its transferred count",
        Generated.WriteSome(output, oldValue, oldValue.Length) == 256 &&
        output.ToArray().SequenceEqual(bytes));
    output.Clear();
    Generated.WriteExact(output, oldValue, oldValue.Length);
    Check("Generated const-buffer WriteBuffer preserves the binary data",
        output.ToArray().SequenceEqual(bytes));

    source.Position = source.Size - 2;
    value = AnsiString.FromBytes(new byte[] { 9, 9, 9, 9 });
    bool eof = false;
    try
    {
        Generated.ReadExact(source, ref value, 4);
    }
    catch (EndOfStreamException)
    {
        eof = true;
    }
    Check("Generated incomplete ReadBuffer preserves received bytes",
        eof &&
        value.ToBytes().SequenceEqual(new byte[] { 254, 255, 9, 9 }));
}
finally
{
    TObject.Free(source);
    TObject.Free(output);
}

Console.WriteLine($"Generated AnsiString execution: {passed}/{passed + failed} passed.");
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
        Check("Generated AnsiString helpers compile and execute", false);
    }
    else
    {
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> errors = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        Console.Write(await output);
        Console.Write(await errors);
        Check("Generated AnsiString helpers compile and execute",
            process.ExitCode == 0);
    }

    Console.WriteLine($"Generated compilation directory: {directory}");
}

Console.WriteLine($"Generated-AnsiString checks: {total - failed}/{total} passed.");
return failed == 0 ? 0 : 1;