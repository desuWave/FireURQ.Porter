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

string source = """
unit DispatchTest;
interface
type
TBase = class
private
  function Hidden: Integer; virtual;
private
  function Secret: Integer;
  Stored: Integer;
  procedure SetStored(const Value: Integer);
protected
  Loaded: Boolean;
public
  property Setting: Integer read Stored write setstored;
  property DirectSetting: Integer read Stored write Stored;
  property Enabled: Boolean read Loaded write Loaded;
  function IsDisabledFor(Other: TBase): Boolean;
  function InvertStored: Integer;
  function Ready: Boolean;
  function GetMask: Integer;
  function InvertMask: Integer;
  function IsOtherNotReady(Other: TBase): Boolean;
  function Accepts(Value: Integer): Boolean;
  function MaskOf(Value: Integer): Integer;
  function Rejects(Value: Integer): Boolean;
  function OtherRejects(Other: TBase; Value: Integer): Boolean;
  function InvertArgument(Value: Integer): Integer;
  function LocalRejects(Input: Integer): Boolean;
  function LocalInvert: Integer;
  function MissingObject(Other: TBase): Boolean;
  function MissingPointer(Address: Pointer): Boolean;
  function Value: Integer; virtual;
  function ReadHidden: Integer;
  function ReadSecret: Integer;
end;
TChild = class(TBase)
private
  function Hidden: Integer; override;
public
  function Value: Integer; override;
  function IsNotLoaded: Boolean;
  function IsNotReady: Boolean;
end;
implementation
function TBase.Hidden: Integer;
begin
  Result := 10;
end;
procedure TBase.SetStored(const Value: Integer);
begin
  Stored := Value;
end;
function TBase.IsDisabledFor(Other: TBase): Boolean;
begin
  Result := not Other.Enabled;
end;
function TBase.InvertStored: Integer;
begin
  Result := not Stored;
end;
function TBase.Ready: Boolean;
begin
  Result := Loaded;
end;
function TBase.GetMask: Integer;
begin
  Result := Stored;
end;
function TBase.InvertMask: Integer;
begin
  Result := not GetMask;
end;
function TBase.IsOtherNotReady(Other: TBase): Boolean;
begin
  Result := not Other.ready;
end;
function TBase.Accepts(Value: Integer): Boolean;
begin
  Result := Stored = Value;
end;
function TBase.MaskOf(Value: Integer): Integer;
begin
  Result := Value;
end;
function TBase.Rejects(Value: Integer): Boolean;
begin
  Result := not Accepts(Value);
end;
function TBase.OtherRejects(Other: TBase; Value: Integer): Boolean;
begin
  Result := not Other.Accepts(Value);
end;
function TBase.InvertArgument(Value: Integer): Integer;
begin
  Result := not MaskOf(Value);
end;
function TBase.LocalRejects(Input: Integer): Boolean;
  function Accepts(Value: Integer): Boolean;
  begin
    Result := Value = 7;
  end;
  function RejectLocal(Value: Integer): Boolean;
  begin
    Result := not Accepts(Value);
  end;
begin
  Result := RejectLocal(Input);
end;
function TBase.LocalInvert: Integer;
  function GetLocalMask: Integer;
  begin
    Result := 19;
  end;
begin
  Result := not GetLocalMask;
end;
function TBase.MissingObject(Other: TBase): Boolean;
begin
  Result := not Assigned(Other);
end;
function TBase.MissingPointer(Address: Pointer): Boolean;
begin
  Result := not Assigned(Address);
end;
function TChild.IsNotReady: Boolean;
begin
  Result := not Ready;
end;
function TChild.IsNotLoaded: Boolean;
begin
  Result := not Loaded;
end;
function TBase.Secret: Integer;
begin
  Result := 7;
end;
function TBase.Value: Integer;
begin
  Result := 1;
end;
function TBase.ReadHidden: Integer;
begin
  Result := Hidden();
end;
function TBase.ReadSecret: Integer;
begin
  Result := Secret();
end;
function TChild.Hidden: Integer;
begin
  Result := 20;
end;
function TChild.Value: Integer;
begin
  Result := 2;
end;
end.
""";

string directory = Path.Combine(
    Path.GetTempPath(), "FireURQ.DispatchCompile." + Guid.NewGuid().ToString("N"));
string inputRoot = Path.Combine(directory, "Input");
string outputRoot = Path.Combine(directory, "Generated");
Directory.CreateDirectory(inputRoot);
string path = Path.Combine(inputRoot, "DispatchTest.pas");
File.WriteAllText(path, source);

var unit = new DelphiParser(new DelphiLexer(source).Tokenize()).ParseUnit();
var model = SemanticModelBuilder.Build(unit);
var bodies = BodyTokenAnalyzer.Analyze(unit, path)
    .Where(body => body.BodyTokens.Count > 0)
    .Select(body => (
        Body: body,
        Behavior: BehaviorAnalyzer.Analyze(path, body.Name, body.BodyTokens)))
    .ToList();

var input = new ProjectTranslationInput(inputRoot, path, unit, model, bodies);
var results = new ProjectTranslator().Translate(new[] { input }, outputRoot);

Check("Project translation succeeds",
    results.Count == 1 && results[0].Success);
if (results.Count != 1 || !results[0].Success)
{
    foreach (var result in results) Console.WriteLine(result.Error);
    return 1;
}

string generated = File.ReadAllText(results[0].OutputFile);
Console.WriteLine("=== Generated method signatures ===");
foreach (string line in generated.Split('\n').Where(line =>
    line.Contains(" int ") && line.Contains("(")))
    Console.WriteLine(line.Trim());

Check("Public virtual method emitted",
    generated.Contains("public virtual int Value("));
Check("Public override method emitted",
    generated.Contains("public override int Value("));
Check("Protected virtual method emitted",
    generated.Contains("protected virtual int Hidden("));
Check("Protected override method emitted",
    generated.Contains("protected override int Hidden("));
Check("Private method visibility preserved",
    generated.Contains("private int Secret("));
Check("All twenty-five implementations emitted",
    results[0].FunctionsTranslated == 25);
Check("Assigned predicates use logical not for references and pointers",
    generated.Contains("!(Other != null)") && generated.Contains("!(Address != IntPtr.Zero)"));
Check("Nested functions use their declared return types under not",
    generated.Contains("!Accepts(Value)") && generated.Contains("~GetLocalMask()"));
Check("Calls with arguments preserve Boolean and integer not",
    generated.Contains("!Accepts(Value)") && generated.Contains("!Other.Accepts(Value)") &&
    generated.Contains("~MaskOf(Value)"));
Check("Bare Boolean functions use logical not and receive parentheses",
    generated.Contains("!Ready()") && generated.Contains("!Other.Ready()"));
Check("Bare integer function uses bitwise not and receives parentheses",
    generated.Contains("~GetMask()"));
Check("Boolean property and inherited field use logical not",
    generated.Contains("!Other.Enabled") && generated.Contains("!Loaded"));
Check("Integer field uses bitwise not", generated.Contains("~Stored"));
Check("Ordinary setter passes only the incoming value",
    generated.Contains("set => SetStored(value);") &&
    !generated.Contains("SetStored(Value, value)"));
Check("Field-backed property still assigns the field",
    generated.Contains("set => Stored = value;"));

File.WriteAllText(Path.Combine(directory, "Dispatch.csproj"), """
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
</Project>
""");
File.WriteAllText(Path.Combine(directory, "Program.cs"), """
public static class Program
{
    public static int Main()
    {
        TBase plain = new TBase();
        var child = new TChild();

        plain.Setting = 42;
        child.DirectSetting = 19;
        plain.Enabled = true;
        bool passed =
            plain.IsDisabledFor(child) && !child.IsDisabledFor(plain) &&
            child.IsNotLoaded() && plain.InvertStored() == ~42 &&
            child.IsNotReady() && plain.IsOtherNotReady(child) &&
            !child.IsOtherNotReady(plain) && plain.InvertMask() == ~42 &&
            plain.Rejects(19) && !plain.Rejects(42) &&
            plain.OtherRejects(child, 42) && !plain.OtherRejects(child, 19) &&
            plain.InvertArgument(23) == ~23 &&
            plain.LocalRejects(42) && !plain.LocalRejects(7) && plain.LocalInvert() == ~19 &&
            plain.MissingObject(null) && !plain.MissingObject(child) &&
            plain.MissingPointer(System.IntPtr.Zero) && !plain.MissingPointer(new System.IntPtr(1)) &&
            plain.DirectSetting == 42 &&
            child.Setting == 19 &&
            plain.Value() == 1 &&
            child.Value() == 2 &&
            plain.ReadHidden() == 10 &&
            child.ReadHidden() == 20 &&
            plain.ReadSecret() == 7 &&
            child.ReadSecret() == 7;

        System.Console.WriteLine(passed
            ? "[PASS] Public and protected virtual dispatch; private method calls"
            : "[FAIL] Instance dispatch behavior");
        return passed ? 0 : 1;
    }
}
""");

var start = new ProcessStartInfo("dotnet") { UseShellExecute = false };
start.ArgumentList.Add("run");
start.ArgumentList.Add("--project");
start.ArgumentList.Add(Path.Combine(directory, "Dispatch.csproj"));
start.ArgumentList.Add("--configuration");
start.ArgumentList.Add("Release");

using (var process = Process.Start(start))
{
    if (process is null)
        Check("Generated project compiles and executes", false);
    else
    {
        process.WaitForExit();
        Check("Generated project compiles and executes", process.ExitCode == 0);
    }
}

Console.WriteLine($"Instance-dispatch checks: {total - failed}/{total} passed.");
Console.WriteLine("Generated compilation directory: " + directory);
return failed == 0 ? 0 : 1;