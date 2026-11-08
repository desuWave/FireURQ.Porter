using System.Diagnostics;
using System.Text;
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

string source = """
unit StreamCallsTest;
interface
type
TStreamAlias = TStream;
TUser = class
public
  procedure ReadBuffer(value: Integer; count: Integer);
end;
implementation
procedure WriteInt(stream: TStream; value: Integer);
begin
  stream.WriteBuffer(value, 4);
end;
procedure ReadInt(stream: TStream; var value: Integer);
begin
  stream.ReadBuffer(value, 4);
end;
procedure WriteBool(stream: TStream; value: Boolean);
begin
  stream.WriteBuffer(value, 1);
end;
procedure ReadBool(stream: TStream; var value: Boolean);
begin
  stream.ReadBuffer(value, 1);
end;
procedure WriteNative(stream: TStream; data: Pointer; count: Integer);
begin
  stream.WriteBuffer(data^, count);
end;
procedure ReadNative(stream: TStream; data: Pointer; count: Integer);
begin
  stream.ReadBuffer(data^, count);
end;
function ReadSome(stream: TStream; var value: Integer): Integer;
begin
  Result := stream.Read(value, 4);
end;
procedure UseOwn(stream: TUser);
begin
  stream.ReadBuffer(5, 4);
end;
procedure ReadAlias(stream: TStreamAlias; var value: Integer);
begin
  stream.ReadBuffer(value, 4);
end;
procedure ReadArray(stream: TStream; buffer: array of Byte);
begin
  stream.ReadBuffer(buffer, 4);
end;
procedure ReadLocal(stream: TStream; var value: Integer);
var
  local: TStream;
begin
  local := stream;
  local.ReadBuffer(value, 4);
end;
end.
""";

var unit = new DelphiParser(new DelphiLexer(source).Tokenize()).ParseUnit();
var model = SemanticModelBuilder.Build(unit);
var resolver = new TypeResolver(new[] { model });
Check("Stream alias resolves to a standard operation",
    resolver.IsInheritedTStreamOperation("TStreamAlias", "ReadBuffer", model.Name));
Check("Custom method is excluded",
    !resolver.IsInheritedTStreamOperation("TUser", "ReadBuffer", model.Name));

var bodies = BodyTokenAnalyzer.Analyze(unit, "StreamCallsTest.pas")
    .Where(body => body.BodyTokens.Count > 0).ToList();
var translator = new DelphiToCSharpTranslator(resolver, model.Name);
var generated = new StringBuilder();
generated.AppendLine("using System;");
generated.AppendLine("using FireURQ.Runtime;");
generated.AppendLine("public static class Cases {");
int translated = 0;
foreach (var body in bodies)
{
    var function = FunctionImplementationMatcher.Match(unit, body, model);
    if (function is null) continue;
    var behavior = BehaviorAnalyzer.Analyze(
        "StreamCallsTest.pas", body.Name, body.BodyTokens);
    generated.Append("public static ");
    generated.AppendLine(translator.TranslateFunction(function, behavior));
    translated++;
}
generated.AppendLine("}");

string code = generated.ToString();
Check("All eleven bodies are matched and translated", translated == 11);
Check("Scalar reads receive ref",
    code.Contains("ReadBuffer(ref value, checked((int)(4)))"));
Check("Pointer dereference becomes a native buffer address",
    code.Contains("ReadBuffer(data, checked((int)(count)))") &&
    code.Contains("WriteBuffer(data, checked((int)(count)))"));
Check("Byte array is passed without ref",
    code.Contains("ReadBuffer(buffer, checked((int)(4)))"));
Check("Custom ReadBuffer call is preserved",
    code.Contains("stream.ReadBuffer(5, 4);"));
Console.WriteLine("=== Generated stream calls ===");
foreach (string line in code.Split('\n').Where(line =>
    line.Contains(".Read(") || line.Contains(".ReadBuffer(") ||
    line.Contains(".WriteBuffer(")))
    Console.WriteLine(line.Trim());

string directory = Path.Combine(
    Path.GetTempPath(), "FireURQ.GeneratedStreams." +
    Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(directory);
File.WriteAllText(Path.Combine(directory, "TObject.cs"), CSharpObjectRuntime.Source);
File.WriteAllText(Path.Combine(directory, "Streams.cs"), CSharpStreamRuntime.Source);
File.WriteAllText(Path.Combine(directory, "Cases.cs"), code);
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
using System.Runtime.InteropServices;

int failed = 0;
void Check(string name, bool passed)
{
    if (!passed) failed++;
    Console.WriteLine($"[{(passed ? "PASS" : "FAIL")}] {name}");
}

var stream = new TMemoryStream();
Cases.WriteBool(stream, true);
Cases.WriteInt(stream, 0x12345678);
Check("Generated scalar writes preserve the binary layout",
    stream.ToArray().SequenceEqual(new byte[] { 1, 0x78, 0x56, 0x34, 0x12 }));
stream.Position = 0;
bool boolean = false;
int number = 0;
Cases.ReadBool(stream, ref boolean);
Cases.ReadInt(stream, ref number);
Check("Generated scalar reads update caller variables",
    boolean && number == 0x12345678);

stream.Position = 1;
number = 0;
Cases.ReadAlias(stream, ref number);
Check("Stream alias retains read semantics", number == 0x12345678);
stream.Position = 1;
number = 0;
Cases.ReadLocal(stream, ref number);
Check("Local stream variable retains read semantics", number == 0x12345678);
stream.Position = 1;
byte[] array = new byte[4];
Cases.ReadArray(stream, array);
Check("Generated array read updates bytes",
    array.SequenceEqual(new byte[] { 0x78, 0x56, 0x34, 0x12 }));
stream.Position = 1;
number = 0;
Check("Read returns the transferred byte count",
    Cases.ReadSome(stream, ref number) == 4 && number == 0x12345678);
stream.Free();

IntPtr memory = Marshal.AllocHGlobal(4);
try
{
    byte[] expected = { 0, 127, 128, 255 };
    Marshal.Copy(expected, 0, memory, 4);
    var native = new TMemoryStream();
    Cases.WriteNative(native, memory, 4);
    Marshal.Copy(new byte[4], 0, memory, 4);
    native.Position = 0;
    Cases.ReadNative(native, memory, 4);
    byte[] actual = new byte[4];
    Marshal.Copy(memory, actual, 0, 4);
    Check("Generated pointer calls transfer raw bytes", actual.SequenceEqual(expected));
    native.Free();
}
finally
{
    Marshal.FreeHGlobal(memory);
}

var custom = new TUser();
Cases.UseOwn(custom);
Check("Custom ReadBuffer executes without stream adaptation",
    custom.Calls == 1 && custom.LastValue == 5 && custom.LastCount == 4);
return failed == 0 ? 0 : 1;

// Test receiver for the independently declared Pascal method.
public class TUser
{
    public int Calls;
    public int LastValue;
    public int LastCount;
    public void ReadBuffer(int value, int count)
    {
        Calls++;
        LastValue = value;
        LastCount = count;
    }
}
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
        Check("Generated stream calls compile and execute", false);
    else
    {
        process.WaitForExit();
        Check("Generated stream calls compile and execute", process.ExitCode == 0);
    }
}

Console.WriteLine($"Generated-stream checks: {total - failed}/{total} passed.");
Console.WriteLine("Generated compilation directory: " + directory);
return failed == 0 ? 0 : 1;