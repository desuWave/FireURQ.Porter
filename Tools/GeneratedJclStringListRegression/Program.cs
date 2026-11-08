using System.Diagnostics;
using System.Text;
using FireURQ.Porter.Analysis;
using FireURQ.Porter.Lexer;
using FireURQ.Porter.Parser;
using FireURQ.Porter.Translation;

int checks = 0;
int failures = 0;
void Check(string name, bool condition)
{
    checks++;
    if (!condition)
        failures++;
    Console.WriteLine($"[{(condition ? "PASS" : "FAIL")}] {name}");
}

const string source = """
unit JclBindingTest;
interface
implementation

function MakeList: IJclStringList;
begin
  Result := JclStringList;
end;

function AddValue(items: IJclStringList; text: string; value: Variant): Integer;
begin
  Result := items.Add(text);
  items.Variants[Result] := value;
end;

function ReadValue(items: IJclStringList; index: Integer): Variant;
begin
  Result := items.Variants[index];
end;

procedure SetChild(parent: IJclStringList; index: Integer; child: IJclStringList);
begin
  parent.Lists[index] := child;
end;

function ReadChild(parent: IJclStringList; index: Integer): IJclStringList;
begin
  Result := parent.Lists[index];
end;

procedure SetReference(items: IJclStringList; index: Integer; value: IJclStringList);
begin
  items.Interfaces[index] := value;
end;

procedure ClearList(items: IJclStringList);
begin
  items.Clear;
end;

end.
""";

var unit = new DelphiParser(new DelphiLexer(source).Tokenize()).ParseUnit();
var model = SemanticModelBuilder.Build(unit);
var translator = new DelphiToCSharpTranslator(
    new TypeResolver(new[] { model }), model.Name);
var methods = new Dictionary<string, string>();

foreach (var body in BodyTokenAnalyzer.Analyze(unit, "JclBindingTest.pas")
    .Where(value => value.BodyTokens.Count > 0))
{
    var function = FunctionImplementationMatcher.Match(unit, body, model);
    Check(body.Name + ": signature matched", function is not null);
    if (function is null)
        continue;

    string translated = translator.TranslateFunction(
        function,
        BehaviorAnalyzer.Analyze(body.FilePath, body.Name, body.BodyTokens));
    methods[function.Name] = translated;
}

Check("All seven Pascal bodies are translated", methods.Count == 7);
Console.WriteLine("=== Generated JCL helpers ===");
foreach (string method in methods.Values)
    Console.WriteLine(method);

if (methods.Count == 7 && failures == 0)
{
    string directory = Path.Combine(
        Path.GetTempPath(), $"FireURQ.GeneratedJcl.{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    var encoding = new UTF8Encoding(false);

    File.WriteAllText(Path.Combine(directory, "Probe.csproj"), """
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>
""", encoding);

    File.WriteAllText(Path.Combine(directory, "Objects.cs"),
        CSharpObjectRuntime.Source, encoding);
    File.WriteAllText(Path.Combine(directory, "Variants.cs"),
        CSharpVariantRuntime.Source, encoding);
    File.WriteAllText(Path.Combine(directory, "Lists.cs"),
        CSharpJclStringListRuntime.Source, encoding);
File.WriteAllText(Path.Combine(directory, "StringListCommon.cs"),
    CSharpStringListCommonRuntime.Source, encoding);

    string helpers = """
using FireURQ.Runtime;
using static FireURQ.Runtime.DelphiJclStringLists;

public static class Generated
{
""" + Environment.NewLine +
        string.Join(Environment.NewLine,
            methods.Values.Select(value => "public static " + value)) +
        Environment.NewLine + "}";

    File.WriteAllText(Path.Combine(directory, "Generated.cs"),
        helpers, encoding);

    File.WriteAllText(Path.Combine(directory, "Program.cs"), """
using FireURQ.Runtime;
using static FireURQ.Runtime.DelphiVariants;

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

IJclStringList first = Generated.MakeList();
IJclStringList second = Generated.MakeList();
Check("Generated factory returns independent empty lists",
    first.Count == 0 && second.Count == 0 &&
    !ReferenceEquals(first, second));

int index = Generated.AddValue(first, "amount", 12);
Check("Generated Add returns the actual index",
    index == 0 && first[index] == "amount");
Check("Generated Variant assignment and read preserve the value",
    (double)Generated.ReadValue(first, index) == 12);

Generated.AddValue(first, "null", Null);
Check("Generated Variant helpers preserve Null",
    VarIsNull(Generated.ReadValue(first, 1)));

IJclStringList parent = Generated.MakeList();
parent.Add("child");
IJclStringList lazyChild = Generated.ReadChild(parent, 0);
Check("Generated Lists read creates and retains a child",
    lazyChild.Count == 0 &&
    ReferenceEquals(lazyChild, parent.Interfaces[0]));

Generated.SetChild(parent, 0, first);
Check("Generated Lists assignment preserves reference identity",
    ReferenceEquals(first, Generated.ReadChild(parent, 0)));

IJclStringList references = Generated.MakeList();
references.Add("reference");
Generated.SetReference(references, 0, first);
Check("Generated Interfaces assignment stores the supplied reference",
    ReferenceEquals(first, references.Interfaces[0]));

Generated.ClearList(parent);
Check("Generated parameterless Clear resets the parent",
    parent.Count == 0 && parent.LastIndex == -1);
Check("Clearing the parent preserves an externally retained child",
    first.Count == 2 &&
    (double)Generated.ReadValue(first, 0) == 12);

Generated.ClearList(references);
Check("Generated Clear retains interface mode",
    references.Count == 0 &&
    references.ObjectsMode == TJclStringListObjectsMode.omInterfaces);

Console.WriteLine($"Generated JCL execution: {passed}/{passed + failed} passed.");
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

    start.Environment["DOTNET_NOLOGO"] = "1";
    start.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";

    using var process = Process.Start(start);
    if (process is null)
    {
        Check("Generated JCL helpers compile and execute", false);
    }
    else
    {
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> errors = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        Console.Write(await output);
        Console.Write(await errors);
        Check("Generated JCL helpers compile and execute", process.ExitCode == 0);
    }

    Console.WriteLine($"Generated compilation directory: {directory}");
}

Console.WriteLine($"Generated-JCL checks: {checks - failures}/{checks} passed.");
Environment.ExitCode = failures == 0 ? 0 : 1;