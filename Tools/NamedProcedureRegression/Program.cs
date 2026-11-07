using System.Text;
using FireURQ.Porter.Analysis;
using FireURQ.Porter.Lexer;
using FireURQ.Porter.Parser;
using FireURQ.Porter.Translation;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
int total = 0;
int failed = 0;

void Check(string name, bool passed)
{
    total++;
    if (!passed) failed++;
    Console.WriteLine($"[{(passed ? "PASS" : "FAIL")}] {name}");
}

string source = """
unit DelegateTest;
interface
type
  TNotify = procedure(value: Integer) of object;
  TNative = function(var value: Integer): Boolean; stdcall;
  TAfter = Integer;
implementation
end.
""";

var ast = new DelphiParser(new DelphiLexer(source).Tokenize()).ParseUnit();

foreach (var entry in new[]
{
    (Name: "Builder", Model: SemanticModelBuilder.Build(ast)),
    (Name: "Analyzer", Model: SemanticAnalyzer.Analyze(ast))
})
{
    var model = entry.Model;
    Check($"{entry.Name}: both named signatures preserved",
        model.Types.Count(t => t.ProcedureType is not null) == 2);

    Check($"{entry.Name}: declaring unit preserved",
        model.Types.Where(t => t.ProcedureType is not null)
            .All(t => t.UnitName == "DelegateTest"));

    var translator = new CSharpDeclarationTranslator(
        new TypeResolver(new[] { model }), model.Name);

    string generated = translator.Translate(model);

    Check($"{entry.Name}: managed method delegate",
        generated.Contains("public delegate void TNotify(int @value);"));

    Check($"{entry.Name}: native signature and modifier",
        generated.Contains("public delegate bool TNative(ref int @value);"));

    Check($"{entry.Name}: only native delegate has ABI attribute",
        System.Text.RegularExpressions.Regex.Matches(
            generated, "UnmanagedFunctionPointer").Count == 1 &&
        generated.Contains("CallingConvention.StdCall"));

    if (entry.Name == "Builder")
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            "FireURQ.NamedDelegateCompile." + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string project = Path.Combine(directory, "DelegateCompile.csproj");

        File.WriteAllText(project, """
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>disable</ImplicitUsings>
  </PropertyGroup>
</Project>
""");

        File.WriteAllText(
            Path.Combine(directory, "Delegates.cs"),
            "using System.Runtime.InteropServices;" +
            Environment.NewLine + generated);

        var info = new System.Diagnostics.ProcessStartInfo("dotnet")
        {
            UseShellExecute = false
        };
        info.ArgumentList.Add("build");
        info.ArgumentList.Add(project);
        info.ArgumentList.Add("--configuration");
        info.ArgumentList.Add("Release");
        info.ArgumentList.Add("--nologo");

        int exitCode = 1;
        using (var process = System.Diagnostics.Process.Start(info))
        {
            if (process is not null)
            {
                process.WaitForExit();
                exitCode = process.ExitCode;
            }
        }

        Check("Actual generated delegates compile", exitCode == 0);
    }
}

// Supply the two real Delphi roots as positional test arguments.
foreach (string root in args)
{
    foreach (string path in Directory.GetFiles(
        root, "*.pas", SearchOption.AllDirectories))
    {
        string fileName = Path.GetFileName(path);
        if (fileName is not ("d2dTypes.pas" or "Dynamic_Bass.pas" or "f2Types.pas"))
            continue;

        var processor = new DelphiConditionalPreprocessor();
        bool processed = processor.TryProcess(
            new DelphiLexer(File.ReadAllText(
                path, Encoding.GetEncoding(1251))).Tokenize(),
            out var tokens, out var error, sourcePath: path);

        Check($"Preprocess {fileName}", processed);
        if (!processed)
        {
            Console.WriteLine(error);
            continue;
        }

        var parsed = new DelphiParser(tokens).ParseUnit();
        var model = SemanticModelBuilder.Build(parsed);
        string generated = new CSharpDeclarationTranslator(
            new TypeResolver(new[] { model }), model.Name).Translate(model);

        var named = parsed.Declarations
            .OfType<FireURQ.Porter.AST.DelphiProcedureType>().ToArray();

        Check($"{fileName}: every AST callback reaches the model",
            named.All(p => model.Types.Any(t =>
                t.Name == p.Name && t.ProcedureType is not null)));

        Check($"{fileName}: every named callback is emitted",
            named.All(p => System.Text.RegularExpressions.Regex.IsMatch(
                generated,
                @"\bpublic delegate [^\r\n]+\s" +
                System.Text.RegularExpressions.Regex.Escape(p.Name) + @"\(")));

        Console.WriteLine($"  Named callbacks: {named.Length}");
    }
}

Console.WriteLine($"Named-delegate checks: {total - failed}/{total} passed.");
return failed == 0 ? 0 : 1;
