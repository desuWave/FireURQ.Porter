using System.Diagnostics;
using FireURQ.Porter.Translation;

string directory = Path.Combine(
    Path.GetTempPath(), "FireURQ.VariantValues." +
    Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(directory);
File.WriteAllText(
    Path.Combine(directory, "Variant.cs"), CSharpVariantRuntime.Source);
string project = Path.Combine(directory, "Probe.csproj");
File.WriteAllText(project, """
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>
""");
File.WriteAllText(Path.Combine(directory, "Program.cs"), """
using FireURQ.Runtime;
using static FireURQ.Runtime.DelphiVariants;

int total = 0;
int failed = 0;
void Check(string name, bool passed)
{
    total++;
    if (!passed) failed++;
    Console.WriteLine($"[{(passed ? "PASS" : "FAIL")}] {name}");
}

Variant empty = default;
Variant unknown = Variant.Null;
Check("Default Variant is Unassigned",
    VarType(empty) == varEmpty && VarIsEmpty(empty) && !VarIsNull(empty));
Check("Null is distinct from Unassigned",
    VarType(unknown) == varNull && VarIsNull(unknown) && !VarIsEmpty(unknown));

Variant integer = -42;
Check("Integer preserves its type and signed value",
    VarType(integer) == varInteger &&
    integer.TryGetInteger(out int number) && number == -42);
Variant real = 1.25;
Check("Double preserves its type and value",
    VarType(real) == varDouble &&
    real.TryGetDouble(out double fraction) && fraction == 1.25);
Variant boolean = true;
Check("Boolean remains distinct from numeric types",
    VarType(boolean) == varBoolean &&
    boolean.TryGetBoolean(out bool flag) && flag && !VarIsNumeric(boolean));
Variant text = "ривіт, FireURQ";
Check("String preserves text and varString tag",
    VarType(text) == varString &&
    text.TryGetString(out string content) && content == "ривіт, FireURQ");
Variant numericText = "123";
Check("Numeric-looking string remains a string",
    !VarIsNumeric(numericText) && VarType(numericText) == varString);
Check("Only supported numeric values are numeric",
    VarIsNumeric(integer) && VarIsNumeric(real) &&
    !VarIsNumeric(empty) && !VarIsNumeric(unknown) && !VarIsNumeric(text));

Variant zero = 0;
Variant emptyText = "";
Variant falseValue = false;
Check("Zero, empty string and false are assigned values",
    !VarIsEmpty(zero) && !VarIsEmpty(emptyText) && !VarIsEmpty(falseValue));
Check("Typed access rejects an incompatible tag",
    !text.TryGetInteger(out _) && !unknown.TryGetDouble(out _));

Variant copy = text;
VarClear(ref text);
Check("VarClear resets only the selected value",
    VarIsEmpty(text) &&
    copy.TryGetString(out string copied) && copied == "ривіт, FireURQ");
VarClear(ref unknown);
Check("VarClear changes Null to Unassigned",
    VarIsEmpty(unknown) && !VarIsNull(unknown));

Check("Integer converts to a real value", (double)integer == -42.0);
Check("Double conversion preserves its value", (double)real == 1.25);
Check("Unassigned converts to numeric zero", (double)Variant.Unassigned == 0.0);
Check("Delphi true converts to minus one", (double)boolean == -1.0);
Check("False converts to numeric zero", (double)falseValue == 0.0);
Check("Standard numeric string conversion remains numeric",
    (double)numericText == 123.0);
Check("String conversion preserves its text",
    (string)copy == "ривіт, FireURQ");
Check("Unassigned converts to an empty string",
    (string)Variant.Unassigned == "");

bool nullNumberRejected = false;
try
{
    double unused = Variant.Null;
}
catch (InvalidCastException)
{
    nullNumberRejected = true;
}
Check("Null is not silently converted to numeric zero", nullNumberRejected);

bool nullStringRejected = false;
try
{
    string unused = Variant.Null;
}
catch (InvalidCastException)
{
    nullStringRejected = true;
}
Check("Null is not silently converted to an empty string", nullStringRejected);

// These helpers reproduce the branches in furqTypes.pas.
double EnsureReal(Variant value) =>
    VarType(value) == varString ? ((string)value).Length : (double)value;
string EnsureString(Variant value) =>
    VarType(value) == varString ? (string)value : "";

double EqualUrql(Variant left, Variant right) =>
    VarType(left) == varString
        ? (EnsureString(left) == EnsureString(right) ? 1.0 : 0.0)
        : (EnsureReal(left) == EnsureReal(right) ? 1.0 : 0.0);

Check("URQL string-to-real conversion uses length",
    EnsureReal("123") == 3.0);
Check("URQL non-string-to-string conversion uses empty text",
    EnsureString(123) == "" && EnsureString(Variant.Null) == "");
Check("URQL mixed addition uses string length",
    EnsureReal("123") + EnsureReal(2) == 5.0);
Check("URQL string addition preserves concatenation",
    EnsureString("12") + EnsureString("3") == "123");
Check("URQL equality preserves left-operand conversion rules",
    EqualUrql("a", 1) == 0.0 && EqualUrql(1, "a") == 1.0);

Variant nullLeft = Variant.Null;
Variant nullRight = Variant.Null;
Check("Default Null equality accepts two Null values",
    nullLeft == nullRight && !(nullLeft != nullRight));
Check("Null differs from Unassigned",
    nullLeft != Variant.Unassigned && !(nullLeft == Variant.Unassigned));
Check("Null differs from numeric zero",
    nullLeft != zero && zero != nullLeft);
Check("Null differs from empty text",
    nullLeft != emptyText && emptyText != nullLeft);
Check("Assigned numeric values compare normally",
    integer == (Variant)(-42.0) && integer != (Variant)42);
Check("Strings compare by their content and case",
    (Variant)"abc" == (Variant)"abc" &&
    (Variant)"abc" != (Variant)"ABC");
Check("Numeric comparisons permit numeric strings",
    numericText == (Variant)123);
Check("Unassigned follows numeric and string context",
    Variant.Unassigned == zero && Variant.Unassigned == emptyText);
Check("Equal numeric variants have compatible hashes",
    ((Variant)123).GetHashCode() == ((Variant)123.0).GetHashCode() &&
    numericText.GetHashCode() == ((Variant)123).GetHashCode());
Check("Null comparisons do not convert Null to a scalar",
    nullLeft.Equals(nullRight) && !nullLeft.Equals(zero));

Console.WriteLine($"Variant-value checks: {total - failed}/{total} passed.");
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
    Console.WriteLine("[FAIL] Could not start generated Variant test.");
    return 1;
}
process.WaitForExit();
Console.WriteLine("Generated compilation directory: " + directory);
return process.ExitCode;