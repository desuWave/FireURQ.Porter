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

SemanticUnit Parse(string source) =>
    SemanticModelBuilder.Build(new DelphiParser(
        new DelphiLexer(source).Tokenize()).ParseUnit());

var unit = Parse("""
unit Dynamic_Bass;
interface
type
STREAMPROC = function(handle: Cardinal; buffer: Pointer;
  length: Cardinal; user: Pointer): Cardinal; stdcall;
DOWNLOADPROC = procedure(buffer: Pointer); stdcall;
const
STREAMPROC_DUMMY: STREAMPROC = STREAMPROC(0);
STREAMPROC_PUSH: STREAMPROC = STREAMPROC(-1);
var
BASS_StreamCreate: function(freq, chans, flags: Cardinal;
  proc: STREAMPROC; user: Pointer): Cardinal; stdcall;
Download: procedure(proc: DOWNLOADPROC); stdcall;
Managed: procedure(proc: STREAMPROC) of object;
implementation
end.
""");

var resolver = new TypeResolver(new[] { unit });
var translator = new CSharpDeclarationTranslator(resolver, unit.Name);
string declarations = translator.Translate(unit);
string members = translator.TranslateModuleMembers(unit);

Console.WriteLine("=== Generated special constants ===");
Console.WriteLine(members);

Check("Callback delegate remains declared",
    declarations.Contains("public delegate uint STREAMPROC("));
Check("Dummy is a readonly zero pointer",
    members.Contains(
        "readonly global::System.IntPtr STREAMPROC_DUMMY = global::System.IntPtr.Zero;"));
Check("Push is a readonly minus-one pointer",
    members.Contains(
        "readonly global::System.IntPtr STREAMPROC_PUSH = new global::System.IntPtr(-1);"));
Check("Native creation parameter accepts pointer",
    declarations.Contains("global::System.IntPtr @proc"));
Check("Other callback type remains a delegate parameter",
    declarations.Contains("DOWNLOADPROC @proc"));
Check("Managed procedure keeps STREAMPROC parameter",
    declarations.Contains("ManagedDelegate(STREAMPROC @proc)"));

var unrelated = Parse("""
unit Unrelated;
interface
type STREAMPROC = function: Cardinal; stdcall;
var Native: procedure(proc: STREAMPROC); stdcall;
implementation
end.
""");
var unrelatedResolver = new TypeResolver(new[] { unrelated });
string unrelatedText = new CSharpDeclarationTranslator(
    unrelatedResolver, unrelated.Name).Translate(unrelated);
Check("Same type name in another unit is not adapted",
    unrelatedText.Contains("NativeDelegate(STREAMPROC @proc)"));

string compileDirectory = Path.Combine(
    Path.GetTempPath(), "FireURQ.SentinelCompile." + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(compileDirectory);
File.WriteAllText(Path.Combine(compileDirectory, "Sentinels.csproj"), """
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
</Project>
""");

File.WriteAllText(Path.Combine(compileDirectory, "Program.cs"),
    "using System;\nusing System.Runtime.InteropServices;\n" +
    declarations + "\npublic static class BassModule {\n" + members + "\n}\n" +
    """
public static class Program
{
    public static int Main()
    {
        IntPtr received = IntPtr.Zero;
        BassModule.BASS_StreamCreate = (freq, chans, flags, proc, user) =>
        {
            received = proc;
            return 7;
        };

        bool passed = BassModule.BASS_StreamCreate(
            44100, 2, 0, BassModule.STREAMPROC_DUMMY, IntPtr.Zero) == 7 &&
            received == IntPtr.Zero;

        BassModule.BASS_StreamCreate(
            44100, 2, 0, BassModule.STREAMPROC_PUSH, IntPtr.Zero);
        passed &= received.ToInt64() == -1;

        STREAMPROC callback = (handle, buffer, length, user) => length;
        IntPtr address = Marshal.GetFunctionPointerForDelegate(callback);
        BassModule.BASS_StreamCreate(44100, 2, 0, address, IntPtr.Zero);
        passed &= received == address;
        GC.KeepAlive(callback);

        Console.WriteLine(passed
            ? "[PASS] Generated signature accepts both markers and callback address"
            : "[FAIL] Generated signature or marker value");
        return passed ? 0 : 1;
    }
}
""");

var start = new ProcessStartInfo("dotnet") { UseShellExecute = false };
start.ArgumentList.Add("run");
start.ArgumentList.Add("--project");
start.ArgumentList.Add(Path.Combine(compileDirectory, "Sentinels.csproj"));
start.ArgumentList.Add("--configuration");
start.ArgumentList.Add("Release");
using (var process = Process.Start(start))
{
    if (process is null)
        Check("Generated code compiles and executes", false);
    else
    {
        process.WaitForExit();
        Check("Generated code compiles and executes", process.ExitCode == 0);
    }
}

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
string path = @"E:\EVI_DATA\EVI_Projects\d2dge-furq_2_2_4\Dynamic_Bass.pas";
bool ok = new DelphiConditionalPreprocessor().TryProcess(
    new DelphiLexer(File.ReadAllText(
        path, Encoding.GetEncoding(1251))).Tokenize(),
    out var tokens, out var error, sourcePath: path);
Check("Real Dynamic_Bass preprocessing", ok);
if (!ok)
    Console.WriteLine(error);
else
{
    var model = SemanticModelBuilder.Build(new DelphiParser(tokens).ParseUnit());
    var realResolver = new TypeResolver(new[] { model });
    var realTranslator = new CSharpDeclarationTranslator(realResolver, model.Name);
    string realDeclarations = realTranslator.Translate(model);
    string realMembers = realTranslator.TranslateModuleMembers(model);

    foreach (string line in (realDeclarations + "\n" + realMembers).Split('\n')
        .Where(line => line.Contains("BASS_StreamCreateDelegate(") ||
            line.Contains(" STREAMPROC_DUMMY = ") ||
            line.Contains(" STREAMPROC_PUSH = ")))
        Console.WriteLine(line);

    Check("Real special values use pointers",
        realMembers.Contains(
            "readonly global::System.IntPtr STREAMPROC_DUMMY = global::System.IntPtr.Zero;") &&
        realMembers.Contains(
            "readonly global::System.IntPtr STREAMPROC_PUSH = new global::System.IntPtr(-1);"));
    Check("Real creation delegate accepts pointer",
        realDeclarations.Split('\n').Any(line =>
            line.Contains("BASS_StreamCreateDelegate(") &&
            line.Contains("global::System.IntPtr @proc")));
    Check("Real callback delegate is preserved",
        realDeclarations.Contains("public delegate uint STREAMPROC("));
}

Console.WriteLine($"Stream-sentinel checks: {total - failed}/{total} passed.");
Console.WriteLine("Generated compilation directory: " + compileDirectory);
return failed == 0 ? 0 : 1;