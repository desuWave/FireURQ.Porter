param(
    [Parameter(Mandatory = $true)][string]$VerificationDirectory,
    [string]$QuestPath = 'E:\EVI_DATA\EVI_Projects\fireurq-master\tests\ver1.8\202.simple_links.qst'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$work = Join-Path $root ('.fireurq-work\QuestLoad-' + [Guid]::NewGuid().ToString('N'))
$oldTemp = $env:TEMP
$oldTmp = $env:TMP
$exitCode = 1
try {
    $generated = Join-Path (Resolve-Path -LiteralPath $VerificationDirectory).Path 'translation-output'
    $quest = (Resolve-Path -LiteralPath $QuestPath).Path
    $context = [IO.File]::ReadAllText((Join-Path $generated 'fireurq-master\furqContext.cs'))
    $pattern = '(?ms)^    public void Load\(in string aFilename, TFURQCodeLoadProc aLoadProc\)\s*\r?\n    \{.*?^    \}'
    $methods = [regex]::Matches($context, $pattern)
    if ($methods.Count -ne 1) { throw 'Expected one generated TFURQCode.Load method.' }
    New-Item -ItemType Directory -Path $work -Force | Out-Null
    $env:TEMP = $work
    $env:TMP = $work
    foreach ($file in @('Variant.cs', 'JclStringLists.cs', 'StringListCommon.cs')) {
        Copy-Item -LiteralPath (Join-Path $generated "__runtime\$file") -Destination $work
    }
    @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>
'@ | Set-Content -LiteralPath (Join-Path $work 'QuestLoad.csproj') -Encoding UTF8
    $source = @'
using System.Text;
using FireURQ.Runtime;
using static FireURQ.Runtime.DelphiJclStringLists;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
var loader = new LoaderProbe();
var reads = new List<string>();
loader.Load(args[0], (in string path) =>
{
    reads.Add(path);
    return File.ReadAllText(path, Encoding.GetEncoding(1251));
});
if (reads.Count != 1 || reads[0] != args[0])
    throw new InvalidOperationException("Unexpected source reads: " + string.Join(", ", reads));
loader.CheckLocation("0", "pln ");
loader.CheckLocation("steklo", "pln It is GLAAAAS!!!!");
Console.WriteLine("PASS: generated Load read the author fixture and registered both locations.");

delegate string TFURQCodeLoadProc(in string path);
interface IFURQCodePoint { }
sealed record TFURQCodePoint(int FileIndex, int LineNumber) : IFURQCodePoint
{
    public static TFURQCodePoint Create(int file, int line) => new(file, line);
}

sealed class LoaderProbe
{
    private const string c_sUsePrefix = "use_";
    private const string c_sInclude = "%include ";
    private const string c_sEnd = "end";
    private readonly IJclStringList f_Source = JclStringList;
    private readonly IJclStringList f_Files = JclStringList;
    private readonly IJclStringList f_Labels = JclStringList;
    private readonly IJclStringList f_ActionList = JclStringList;

    // Source hashing is outside this fixture's loader assertions.
    private void CalcSourceHash() { }

    public void CheckLocation(string name, string expectedText)
    {
        int index = f_Labels.IndexOf(name);
        if (index < 0)
            throw new InvalidOperationException("Missing label: " + name);
        int line = (int)(double)f_Labels.Variants[index];
        if (!f_Source[line + 1].StartsWith(expectedText, StringComparison.Ordinal))
            throw new InvalidOperationException("Unexpected source for label: " + name);
        Console.WriteLine("PASS: label " + name + " at source index " + line);
    }

__GENERATED_LOAD__
}
'@
    $source.Replace('__GENERATED_LOAD__', $methods[0].Value) |
        Set-Content -LiteralPath (Join-Path $work 'Program.cs') -Encoding UTF8

    $ErrorActionPreference = 'Continue'
    $output = @(& dotnet run --project (Join-Path $work 'QuestLoad.csproj') -c Release -- $quest 2>&1 |
        ForEach-Object { [string]$_ })
    $exitCode = $LASTEXITCODE
    $ErrorActionPreference = 'Stop'
    $report = (@("Quest loader probe: $work") + $output + "Quest loader probe exit code: $exitCode") -join "`r`n"
} catch {
    $report = "STOP: $($_.Exception.Message)"
} finally {
    $env:TEMP = $oldTemp
    $env:TMP = $oldTmp
}

Write-Output $report
Set-Clipboard -Value $report -ErrorAction Stop
exit $exitCode
