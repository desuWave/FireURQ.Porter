using System.Diagnostics;
using System.Text;
using FireURQ.Porter.Translation;

string directory = Path.Combine(
    Path.GetTempPath(), $"FireURQ.AnsiStringValues.{Guid.NewGuid():N}");
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

File.WriteAllText(Path.Combine(directory, "AnsiStrings.cs"),
    CSharpAnsiStringRuntime.Source, encoding);

File.WriteAllText(Path.Combine(directory, "Program.cs"), """
using System.Text;
using FireURQ.Runtime;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
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

AnsiString empty = default;
Check("Default AnsiString is empty",
    empty.Length == 0 && empty.ToBytes().Length == 0);
Check("All empty representations compare equally",
    empty == AnsiString.Empty &&
    empty == AnsiString.FromBytes(Array.Empty<byte>()));

byte[] allBytes = Enumerable.Range(0, 256).Select(value => (byte)value).ToArray();
AnsiString binary = AnsiString.FromBytes(allBytes);
Check("All 256 byte values survive without text conversion",
    binary.Length == 256 && binary.ToBytes().SequenceEqual(allBytes));
Check("Indexing starts at one and addresses bytes",
    binary[1] == 0 && binary[129] == 128 && binary[256] == 255);

allBytes[0] = 99;
Check("Changing the input array does not mutate the value", binary[1] == 0);
byte[] exported = binary.ToBytes();
exported[1] = 99;
Check("Changing an exported array does not mutate the value", binary[2] == 1);

AnsiString withNull = AnsiString.FromBytes(new byte[] { 65, 0, 66 });
Check("Embedded zero does not truncate the value",
    withNull.Length == 3 && withNull[2] == 0 && withNull[3] == 66);

AnsiString copied = withNull;
copied = copied.WithByte(1, 90);
Check("Updating a copied value preserves its original",
    copied[1] == 90 && withNull[1] == 65 && copied[2] == 0);
Check("Updating a byte preserves the remaining bytes",
    copied.ToBytes().SequenceEqual(new byte[] { 90, 0, 66 }));

AnsiString grown = withNull.WithLength(6);
Check("Growing preserves the byte prefix",
    grown.Length == 6 &&
    grown.ToBytes().Take(3).SequenceEqual(withNull.ToBytes()));
Check("Newly allocated suffix bytes are zero",
    grown.ToBytes().Skip(3).All(value => value == 0));
Check("Resizing a copy preserves the original length", withNull.Length == 3);

AnsiString shrunk = grown.WithLength(2);
Check("Shrinking retains only the selected byte prefix",
    shrunk.ToBytes().SequenceEqual(new byte[] { 65, 0 }));
Check("Resizing to zero returns an empty value",
    grown.WithLength(0) == AnsiString.Empty);

AnsiString suffix = AnsiString.FromBytes(new byte[] { 255, 128 });
AnsiString joined = withNull + suffix;
Check("Concatenation preserves zeros and high bytes",
    joined.ToBytes().SequenceEqual(new byte[] { 65, 0, 66, 255, 128 }));
Check("Concatenation preserves both original operands",
    withNull.Length == 3 && suffix.Length == 2);
Check("Empty concatenation preserves the value",
    empty + withNull == withNull && withNull + empty == withNull);

Check("Copy uses one-based byte positions",
    joined.Copy(2, 3).ToBytes().SequenceEqual(new byte[] { 0, 66, 255 }));
Check("Copy bounds a large requested length",
    joined.Copy(4, int.MaxValue) == suffix);
Check("Copy beyond the end returns empty", joined.Copy(100, 5) == empty);
Check("Copy with a nonpositive count returns empty",
    joined.Copy(1, 0) == empty && joined.Copy(1, -1) == empty);

var cp1251 = Encoding.GetEncoding(
    1251, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
var cp866 = Encoding.GetEncoding(
    866, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
var strictUtf8 = new UTF8Encoding(false, true);

// ASCII Unicode escapes avoid clipboard damage to Cyrillic source text.
string text = "\u041F\u0440\u0438\u0432\u0435\u0442";
AnsiString ansi = AnsiString.FromText(text, cp1251);
Check("Explicit CP1251 encoding produces the expected bytes",
    ansi.ToBytes().SequenceEqual(
        new byte[] { 0xCF, 0xF0, 0xE8, 0xE2, 0xE5, 0xF2 }));
Check("Explicit CP1251 decoding restores the text", ansi.Decode(cp1251) == text);

AnsiString utf8Name = AnsiString.FromText(text, strictUtf8);
Check("Length counts bytes rather than Unicode characters",
    ansi.Length == 6 && utf8Name.Length == 12);
Check("UTF8 bytes are decoded only with the selected encoding",
    utf8Name.Decode(strictUtf8) == text);

AnsiString oem = AnsiString.FromText(text, cp866);
AnsiString converted = AnsiString.FromText(oem.Decode(cp866), cp1251);
Check("Explicit OEM-to-ANSI conversion restores the intended text",
    converted == ansi && oem[1] == 0x8F);

byte[] extraBytes = { 0, 255, 128, 1, 0, 254 };
AnsiString extraField = AnsiString.FromBytes(extraBytes);
Check("ZIP extra-field bytes remain independent of text encoding",
    extraField.ToBytes().SequenceEqual(extraBytes));

bool invalidUtf8Rejected = false;
try
{
    _ = extraField.Decode(strictUtf8);
}
catch (DecoderFallbackException)
{
    invalidUtf8Rejected = true;
}
Check("Caller-selected strict decoding rejects invalid UTF8",
    invalidUtf8Rejected && extraField.ToBytes().SequenceEqual(extraBytes));

Check("Equality compares complete byte contents",
    AnsiString.FromBytes(new byte[] { 65, 0, 66 }) == withNull &&
    AnsiString.FromBytes(new byte[] { 65, 0, 67 }) != withNull);
Check("Byte equality does not fold letter case",
    AnsiString.FromBytes(new byte[] { 65 }) !=
    AnsiString.FromBytes(new byte[] { 97 }));
Check("Equal byte values have equal hashes",
    AnsiString.FromBytes(withNull.ToBytes()).GetHashCode() ==
    withNull.GetHashCode());

Console.WriteLine($"AnsiString-value checks: {passed}/{passed + failed} passed.");
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
    Console.WriteLine("[FAIL] Generated project could not start");
    Environment.ExitCode = 1;
}
else
{
    Task<string> output = process.StandardOutput.ReadToEndAsync();
    Task<string> errors = process.StandardError.ReadToEndAsync();
    process.WaitForExit();
    Console.Write(await output);
    Console.Write(await errors);
    Environment.ExitCode = process.ExitCode;
}
Console.WriteLine($"Generated compilation directory: {directory}");