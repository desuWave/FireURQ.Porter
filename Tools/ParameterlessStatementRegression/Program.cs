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
unit ParameterlessTest;
interface
type
TCallback = procedure;
TWorker = class
public
  procedure Run(target: TWorker; callback: TCallback);
  procedure Cleanup;
  procedure Free;
end;
implementation
procedure TWorker.Run(target: TWorker; callback: TCallback);
begin
  Cleanup;
  target.Free;
  callback;
  Cleanup();
end;
procedure TWorker.Cleanup;
begin
end;
procedure TWorker.Free;
begin
end;
end.
""";

var unit = new DelphiParser(new DelphiLexer(source).Tokenize()).ParseUnit();
var model = SemanticModelBuilder.Build(unit);
var bodies = BodyTokenAnalyzer.Analyze(unit, "ParameterlessTest.pas")
    .Where(item => item.BodyTokens.Count > 0).ToList();
var runBody = bodies.Single(item => item.Name == "TWorker.Run");
var function = FunctionImplementationMatcher.Match(unit, runBody, model);
Check("Run signature matched", function is not null);
if (function is null) return 1;

var behavior = BehaviorAnalyzer.Analyze(
    "ParameterlessTest.pas", runBody.Name, runBody.BodyTokens);
string generated = new DelphiToCSharpTranslator().TranslateFunction(
    function, behavior);
Console.WriteLine("=== Generated procedure statements ===");
Console.WriteLine(generated);

Check("Bare method becomes a call",
    generated.Contains("Cleanup();"));
Check("Object method becomes a call",
    generated.Contains("target.Free();"));
Check("Procedure variable becomes a call",
    generated.Contains("callback();"));
Check("Existing call is not called twice",
    !generated.Contains("Cleanup()()"));
Check("Both Cleanup calls remain",
    generated.Split("Cleanup();").Length - 1 == 2);

string directory = Path.Combine(
    Path.GetTempPath(), "FireURQ.ParameterlessCompile." +
    Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(directory);
string project = Path.Combine(directory, "Probe.csproj");

File.WriteAllText(project, """
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
</Project>
""");

File.WriteAllText(Path.Combine(directory, "Program.cs"), """
using System;
public delegate void TCallback();

public partial class TWorker
{
    public int CleanupCount;
    public int FreeCount;
    public void Cleanup() { CleanupCount++; }
    public void Free() { FreeCount++; }
}
public partial class TWorker
{
""" + "public " + generated + """
}
public static class Program
{
    public static int Main()
    {
        var worker = new TWorker();
        var target = new TWorker();
        int callbacks = 0;
        worker.Run(target, () => callbacks++);
        bool passed = worker.CleanupCount == 2 &&
            worker.FreeCount == 0 &&
            target.CleanupCount == 0 &&
            target.FreeCount == 1 &&
            callbacks == 1;
        Console.WriteLine(passed
            ? "[PASS] Method receivers, callback and exact call counts"
            : "[FAIL] Parameterless statement behavior");
        return passed ? 0 : 1;
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
        Check("Generated calls compile and execute", false);
    else
    {
        process.WaitForExit();
        Check("Generated calls compile and execute", process.ExitCode == 0);
    }
}

Console.WriteLine($"Parameterless-statement checks: {total - failed}/{total} passed.");
Console.WriteLine("Generated compilation directory: " + directory);
return failed == 0 ? 0 : 1;