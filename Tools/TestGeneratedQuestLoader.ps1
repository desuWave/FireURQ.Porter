param(
    [Parameter(Mandatory = $true)][string]$VerificationDirectory,
    [switch]$ShowWindow,
    [string]$ExportLocations,
    [string]$QuestPath = 'E:\EVI_DATA\EVI_Projects\fireurq-master\tests\ver1.8\202.simple_links.qst'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$work = Join-Path $root ('.fireurq-work\QuestLoad-' + [Guid]::NewGuid().ToString('N'))
$oldTemp = $env:TEMP
$oldTmp = $env:TMP
$oldConsoleEncoding = [Console]::OutputEncoding
$exitCode = 1
try {
    $generated = Join-Path (Resolve-Path -LiteralPath $VerificationDirectory).Path 'translation-output'
    if ($ExportLocations -and $ShowWindow) { throw 'Choose ShowWindow or ExportLocations.' }
    if ($ExportLocations) { $ExportLocations = [IO.Path]::GetFullPath($ExportLocations) }
    $quest = (Resolve-Path -LiteralPath $QuestPath).Path
    $context = [IO.File]::ReadAllText((Join-Path $generated 'fireurq-master\furqContext.cs'))
    $pattern = '(?ms)^    public void Load\(in string aFilename, TFURQCodeLoadProc aLoadProc\)\s*\r?\n    \{.*?^    \}'
    $methods = [regex]::Matches($context, $pattern)
    if ($methods.Count -ne 1) { throw 'Expected one generated TFURQCode.Load method.' }
    New-Item -ItemType Directory -Path $work -Force | Out-Null
    $env:TEMP = $work
    $env:TMP = $work
    foreach ($file in @('TObject.cs', 'Variant.cs', 'JclStringLists.cs', 'StringListCommon.cs')) {
        Copy-Item -LiteralPath (Join-Path $generated "__runtime\$file") -Destination $work
    }
    $project = @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>
'@
    if ($ShowWindow) {
        $project = $project.Replace('net8.0</TargetFramework>', "net8.0-windows</TargetFramework><UseWindowsForms>true</UseWindowsForms><DefineConstants>QUEST_WINDOW</DefineConstants>")
    }
    $project | Set-Content -LiteralPath (Join-Path $work 'QuestLoad.csproj') -Encoding UTF8
    $source = @'
using System.Text;
using FireURQ.Runtime;
using static FireURQ.Runtime.DelphiJclStringLists;

Console.OutputEncoding = new UTF8Encoding(false);
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
var loader = new LoaderProbe();
var reads = new List<string>();
var missingIncludes = new List<string>();
void ReportMissingInclude(string path)
{
    string message = "\u0424\u0430\u0439\u043b " + Path.GetFileName(path) +
        " \u043d\u0435 \u043d\u0430\u0439\u0434\u0435\u043d";
    missingIncludes.Add(message);
    Console.WriteLine(message);
}
bool packed = Path.GetExtension(args[0]).Equals(".qsz", StringComparison.OrdinalIgnoreCase);
using var archive = packed ? System.IO.Compression.ZipFile.OpenRead(args[0]) : null;
string archiveRoot = Path.GetFullPath("__qsz_virtual__");
var entries = new Dictionary<string, System.IO.Compression.ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
string entryPath = args[0];
if (archive != null)
{
    foreach (var entry in archive.Entries)
    {
        if (entry.Name.Length != 0)
            entries.TryAdd(entry.FullName.Replace('\\', '/'), entry);
    }

    string? main = entries.ContainsKey("main.qst") ? "main.qst" :
        entries.ContainsKey("game.qst") ? "game.qst" :
        entries.Keys.FirstOrDefault(name => name.EndsWith(".qst", StringComparison.OrdinalIgnoreCase));
    if (main == null)
        throw new InvalidOperationException("QSZ preview requires a QST entry.");
    entryPath = Path.GetFullPath(Path.Combine(archiveRoot, main));
    Console.WriteLine("QSZ entry: " + main);
}
loader.Load(entryPath, (in string path) =>
{
    reads.Add(path);
    if (archive != null)
    {
        string relative = Path.GetRelativePath(archiveRoot, Path.GetFullPath(path)).Replace('\\', '/');
        if (!entries.TryGetValue(relative, out var entry))
        {
            ReportMissingInclude(relative);
            return string.Empty;
        }
        using var reader = new StreamReader(entry.Open(), Encoding.GetEncoding(1251));
        return reader.ReadToEnd();
    }
    try
    {
        return File.ReadAllText(path, Encoding.GetEncoding(1251));
    }
    catch (FileNotFoundException) when (!String.Equals(path, args[0], StringComparison.OrdinalIgnoreCase))
    {
        ReportMissingInclude(path);
        return string.Empty;
    }
    catch (DirectoryNotFoundException) when (!String.Equals(path, args[0], StringComparison.OrdinalIgnoreCase))
    {
        ReportMissingInclude(path);
        return string.Empty;
    }
});
if (args.Length == 2)
{
    loader.Export(args[1], missingIncludes);
    foreach (string read in reads)
        Console.WriteLine("Quest source requested: " + read);
}
else
{
    if (reads.Count != 1 || reads[0] != args[0])
        throw new InvalidOperationException("Unexpected source reads: " + string.Join(", ", reads));
    loader.CheckLocation("0", "pln ");
    loader.CheckLocation("steklo", "pln It is GLAAAAS!!!!");
    Console.WriteLine("PASS: generated Load read the author fixture and registered both locations.");
}

#if QUEST_WINDOW
QuestWindow.Run(loader);
#endif

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

    public IEnumerable<string> ReadLocation(string name) =>
        ReadLocation(name, new HashSet<int>());

    private IEnumerable<string> ReadLocation(string name, HashSet<int> active)
    {
        int index = f_Labels.IndexOf(name);
        if (index < 0 && name != "0")
            throw new InvalidOperationException("Missing label: " + name);

        if (active.Count >= 64 || !active.Add(index))
            throw new InvalidOperationException("Recursive or too deeply nested preview proc: " + name);
        try
        {
            int start = index < 0 ? 0 : (int)(double)f_Labels.Variants[index] + 1;
            for (int line = start; line < f_Source.Count; line++)
            {
                string source = f_Source[line];
                if (source.Equals("end", StringComparison.OrdinalIgnoreCase) || source.StartsWith(":"))
                    yield break;
                if (source.StartsWith("pln ", StringComparison.OrdinalIgnoreCase))
                    yield return source[4..];
                else if (source.StartsWith("proc ", StringComparison.OrdinalIgnoreCase))
                {
                    string target = source[5..].Trim();
                    if (!HasLocation(target))
                        continue;
                    foreach (string output in ReadLocation(target, active))
                        yield return output;
                }
                else if (source.StartsWith("btn ", StringComparison.OrdinalIgnoreCase))
                {
                    int comma = source.IndexOf(',', 4);
                    if (comma < 0)
                        continue;
                    string target = source[4..comma].Trim();
                    string caption = source[(comma + 1)..].Trim();
                    if (target.Length != 0 && caption.Length != 0)
                        yield return "[[" + caption + "|" + target + "]]";
                }
            }
        }
        finally
        {
            active.Remove(index);
        }
    }

    public bool HasLocation(string name) => name == "0" || f_Labels.IndexOf(name) >= 0;

    public void Export(string path, IEnumerable<string> diagnostics)
    {
        var document = new System.Xml.Linq.XElement("quest",
            diagnostics.Select(message => new System.Xml.Linq.XElement("diagnostic", message)));
        var labels = Enumerable.Range(0, f_Labels.Count).Select(index => f_Labels[index]);
        if (f_Labels.IndexOf("0") < 0)
            labels = labels.Prepend("0");
        foreach (string name in labels)
        {
            document.Add(new System.Xml.Linq.XElement("location",
                new System.Xml.Linq.XAttribute("name", name),
                ReadLocation(name).Select(line => new System.Xml.Linq.XElement("line", line))));
        }
        document.Save(path);
    }

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

#if QUEST_WINDOW
static class QuestWindow
{
    public static void Run(LoaderProbe loader)
    {
        var thread = new System.Threading.Thread(() => Show(loader));
        thread.SetApartmentState(System.Threading.ApartmentState.STA);
        thread.Start();
        thread.Join();
    }

    private static void Show(LoaderProbe loader)
    {
        System.Windows.Forms.Application.EnableVisualStyles();
        using var window = new System.Windows.Forms.Form
        {
            Text = "FireURQ Porter - 202.simple_links.qst",
            ClientSize = new System.Drawing.Size(800, 420),
            StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen
        };
        var panel = new System.Windows.Forms.FlowLayoutPanel
        {
            Dock = System.Windows.Forms.DockStyle.Fill,
            AutoScroll = true,
            FlowDirection = System.Windows.Forms.FlowDirection.TopDown,
            WrapContents = false,
            Padding = new System.Windows.Forms.Padding(16)
        };
        window.Controls.Add(panel);

        void ShowLocation(string name)
        {
            foreach (System.Windows.Forms.Control control in panel.Controls
                .Cast<System.Windows.Forms.Control>().ToArray())
                control.Dispose();

            foreach (string line in loader.ReadLocation(name))
            {
                var row = new System.Windows.Forms.FlowLayoutPanel { AutoSize = true, MaximumSize = new System.Drawing.Size(740, 0) };
                panel.Controls.Add(row);
                int offset = 0;
                foreach (System.Text.RegularExpressions.Match match in
                    System.Text.RegularExpressions.Regex.Matches(line, @"\[\[([^\]]+)\]\]"))
                {
                    AddText(row, line[offset..match.Index]);
                    string[] parts = match.Groups[1].Value.Split('|', 2);
                    string caption = parts[0];
                    string target = parts.Length == 2 ? parts[1] : caption;
                    if (target.StartsWith("!"))
                        target = target[1..];
                    if (target.StartsWith("%"))
                        AddText(row, caption.TrimStart('%'));
                    else
                    {
                        var link = new System.Windows.Forms.LinkLabel { Text = caption, AutoSize = true };
                        link.LinkClicked += (_, _) => ShowLocation(target);
                        row.Controls.Add(link);
                    }
                    offset = match.Index + match.Length;
                }
                AddText(row, line[offset..]);
                Console.WriteLine("Shown: " + line);
            }
            Console.WriteLine("Opened quest location: " + name);
        }

        ShowLocation("0");
        System.Windows.Forms.Application.Run(window);
    }

    private static void AddText(System.Windows.Forms.Control row, string text)
    {
        if (text.Length != 0)
            row.Controls.Add(new System.Windows.Forms.Label { Text = text, AutoSize = true });
    }
}
#endif
'@
    $source.Replace('__GENERATED_LOAD__', $methods[0].Value) |
        Set-Content -LiteralPath (Join-Path $work 'Program.cs') -Encoding UTF8

    [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
    $ErrorActionPreference = 'Continue'
    $arguments = @('run', '--project', (Join-Path $work 'QuestLoad.csproj'), '-c', 'Release', '--', $quest)
    if ($ExportLocations) { $arguments += $ExportLocations }
    $output = @(& dotnet @arguments 2>&1 |
        ForEach-Object { [string]$_ })
    $exitCode = $LASTEXITCODE
    $ErrorActionPreference = 'Stop'
    $report = (@("Quest loader probe: $work") + $output + "Quest loader probe exit code: $exitCode") -join "`r`n"
} catch {
    $report = "STOP: $($_.Exception.Message)"
} finally {
    $env:TEMP = $oldTemp
    $env:TMP = $oldTmp
    [Console]::OutputEncoding = $oldConsoleEncoding
}

Write-Output $report
if (!$ExportLocations) { Set-Clipboard -Value $report -ErrorAction Stop }
exit $exitCode
