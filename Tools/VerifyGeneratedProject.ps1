param(
    [string]$RootPath = (Split-Path -Parent $PSScriptRoot),
    [int]$PreviousErrorCount = 142,
    [switch]$CheckD3D8RenderTargets,
    [switch]$CheckQuestEntryPoints,
    [string[]]$SourceRoots = @(
        'E:\EVI_DATA\EVI_Projects\fireurq-master',
        'E:\EVI_DATA\EVI_Projects\d2dge-furq_2_2_4'
    )
)

$ErrorActionPreference = 'Stop'
$utf8 = [System.Text.UTF8Encoding]::new($false)
$report = [System.Collections.Generic.List[string]]::new()

try {
    $workRoot = Join-Path $RootPath '.fireurq-work'
    $directory = Join-Path $workRoot (
        'FireURQ.ProjectVerification.' + [Guid]::NewGuid().ToString('N'))
    [System.IO.Directory]::CreateDirectory($directory) | Out-Null
    $report.Add("Verification directory: $directory")
    $oldTemp = $env:TEMP
    $oldTmp = $env:TMP
    $env:TEMP = $directory
    $env:TMP = $directory

    function Invoke-DotnetLogged {
        param([string[]]$ToolArguments, [string]$LogName)

        $captured = & dotnet @ToolArguments 2>&1
        $code = $LASTEXITCODE
        $lines = [string[]]@(
            $captured | ForEach-Object { [string]$_ }
        )
        [System.IO.File]::WriteAllLines(
            (Join-Path $directory $LogName), $lines, $utf8)
        return @{ Lines = $lines; ExitCode = $code }
    }

    $SourceRoots = @($SourceRoots | ForEach-Object { (Resolve-Path -LiteralPath $_).Path })
    $ready = $true
    foreach ($sourceRoot in $SourceRoots) {
        if (-not [System.IO.Directory]::Exists($sourceRoot)) {
            $report.Add("STOP: Source directory is missing: $sourceRoot")
            $ready = $false
        }
    }

    if ($ready) {
        $build = Invoke-DotnetLogged -ToolArguments @(
            'build', (Join-Path $RootPath 'FireURQ.Porter.csproj'),
            '--configuration', 'Release', '--nologo'
        ) -LogName 'translator-build.log'

        $report.Add('=== Translator Release build ===')
        foreach ($line in $build.Lines) {
            $report.Add($line)
        }
        $report.Add("Translator build exit code: $($build.ExitCode)")
        $ready = $build.ExitCode -eq 0
    }

    if ($ready) {
        $fileCount = 0
        foreach ($sourceRoot in $SourceRoots) {
            $fileCount += @(
                Get-ChildItem $sourceRoot -Recurse -File -Filter '*.pas'
            ).Count
        }

        $dll = Join-Path $RootPath 'bin\Release\net8.0\FireURQ.Porter.dll'
        Push-Location $directory
        try {
            $translation = Invoke-DotnetLogged -ToolArguments (
                @($dll, '--analyze') + $SourceRoots
            ) -LogName 'translation.log'
        }
        finally {
            Pop-Location
        }

        $report.Add('=== Translation summary ===')
        $report.Add("Delphi files found: $fileCount")
        foreach ($line in $translation.Lines) {
            if ($line -match '^\s*(Files found|Parsed|Failed|Translation files|Translation succeeded|Translation failed|Functions translated|TODO markers)\s*:') {
                $report.Add($line)
            }
        }
        $report.Add("Translation exit code: $($translation.ExitCode)")

        $generatedRoot = Join-Path $directory 'translation-output'
        $ready = $translation.ExitCode -eq 0 -and
            [System.IO.Directory]::Exists($generatedRoot)

        if ($ready) {
            if ($CheckQuestEntryPoints) {
                $quest = Join-Path $SourceRoots[0] 'tests\ver1.8\202.simple_links.qst'
                $report.Add("=== Quest entry point audit ===")
                $report.Add("Author fixture present: $(Test-Path -LiteralPath $quest -PathType Leaf)")
                $generatedFiles = @(
                    @{ Path = 'fireurq-master\furqContext.cs'; Names = @('Load', 'DeleteComments') }
                    @{ Path = 'fireurq-master\f2\f2ScnMain.cs'; Names = @('DoOpenQuest', 'OpenCodeFileProc', 'StartOver') }
                )
                foreach ($entry in $generatedFiles) {
                    $file = Join-Path $generatedRoot $entry.Path
                    if (!(Test-Path -LiteralPath $file -PathType Leaf)) {
                        $report.Add("Missing generated file: $($entry.Path)")
                        continue
                    }
                    foreach ($name in $entry.Names) {
                        $pattern = '^\s*(?:(?:public|private|protected|internal|static|virtual|override)\s+)*(?:void|string)\s+' + $name + '\('
                        $hits = @(Select-String -LiteralPath $file -Pattern $pattern)
                        if (!$hits.Count) { $report.Add("No generated declaration: $name"); continue }
                        foreach ($hit in $hits) {
                            $report.Add(('{0}:{1}: {2}' -f $entry.Path, $hit.LineNumber, $hit.Line.Trim()))
                        }
                    }
                }
            }

            if ($CheckD3D8RenderTargets) {
                $core = [IO.File]::ReadAllText((Join-Path $generatedRoot 'd2dge-furq_2_2_4\d2dCore.cs'))
                $types = [IO.File]::ReadAllText((Join-Path $generatedRoot 'd2dge-furq_2_2_4\d2dTypes.cs'))
                $checks = [ordered]@{
                    'Reference type' = ($types -match '\bsealed class Td2dRenderTarget\b')
                    'Current target' = ($core -match '\bTd2dRenderTarget f_CurTarget;')
                    'Typed target list' = ($core -match '\bList<Td2dRenderTarget> f_Targets;')
                    'Optional screen target' = ($core -match '\bGfx_BeginScene\(Td2dRenderTarget aTarget = default\)')
                    'Target creation' = ($core -match '\bTd2dRenderTarget Target_Create\(' -and $core -match 'out l_Target\.Tex\)')
                    'Texture description' = ($core -match '\bGetLevelDesc\(0, out l_Desc\)')
                    'Target removal' = ($core -match '\bf_Targets\.RemoveAt\(l_Idx\)')
                    'Reset HRESULT' = ($core -match 'Reset\(ref f_D3DPP\) < 0')
                    'Legacy target operations removed' = ($core -notmatch '\b(?:New\(l_Target\)|Dispose\(l_Target\)|Pd2dRenderTarget\(|f_Targets\.Items\b)')
                }
                foreach ($check in $checks.GetEnumerator()) {
                    $status = if ($check.Value) { 'PASS' } else { 'FAIL' }
                    $report.Add("[$status] $($check.Key)")
                }
            }

            $count = @(
                Get-ChildItem $generatedRoot -Recurse -File -Filter '*.cs'
            ).Count
            $report.Add("Generated C# files: $count")
            foreach ($name in @(
                'TObject.cs', 'Variant.cs', 'Streams.cs',
                'JclStringLists.cs', 'Lists.cs', 'InterfaceLists.cs',
                'AnsiStrings.cs', 'AnsiStringStreams.cs',
                'StringListCommon.cs', 'StringLists.cs'
            )) {
                $present = [System.IO.File]::Exists(
                    (Join-Path $generatedRoot "__runtime\$name"))
                $report.Add("Runtime $name present: $present")
            }
        }
        else {
            $report.Add('STOP: Translation failed or output is missing.')
        }
    }

    if ($ready) {
        $project = Join-Path $directory 'GeneratedFireURQ.csproj'
        $projectText = '<Project Sdk="Microsoft.NET.Sdk">' +
            '<PropertyGroup>' +
            '<TargetFramework>net8.0</TargetFramework>' +
            '<AllowUnsafeBlocks>true</AllowUnsafeBlocks>' +
            '<ImplicitUsings>enable</ImplicitUsings>' +
            '<Nullable>enable</Nullable>' +
            '<EnableDefaultCompileItems>false</EnableDefaultCompileItems>' +
            '</PropertyGroup><ItemGroup>' +
            '<Compile Include="translation-output/**/*.cs" />' +
            '</ItemGroup></Project>'
        [System.IO.File]::WriteAllText($project, $projectText, $utf8)

        $compiler = Invoke-DotnetLogged -ToolArguments @(
            'build', $project, '--configuration', 'Release', '--nologo'
        ) -LogName 'generated-build.log'

        $diagnostics = [System.Collections.Generic.List[object]]::new()
        $seen = [System.Collections.Generic.HashSet[string]]::new()
        $pattern = '^(?<Path>.+\.cs)\((?<Line>\d+),(?<Column>\d+)\):\s+error\s+(?<Code>CS\d+):\s+(?<Message>.*?)(?:\s+\[[^\]]+\.csproj\])?$'

        foreach ($line in $compiler.Lines) {
            $match = [regex]::Match($line, $pattern)
            if (-not $match.Success) {
                continue
            }

            $item = [pscustomobject]@{
                Path = $match.Groups['Path'].Value
                Line = [int]$match.Groups['Line'].Value
                Column = [int]$match.Groups['Column'].Value
                Code = $match.Groups['Code'].Value
                Message = $match.Groups['Message'].Value
            }
            $key = '{0}:{1}:{2}:{3}:{4}' -f `
                $item.Path, $item.Line, $item.Column,
                $item.Code, $item.Message
            if ($seen.Add($key)) {
                $diagnostics.Add($item)
            }
        }

        $report.Add('=== Compiler error categories ===')
        $table = $diagnostics |
            Group-Object Code |
            Sort-Object Count -Descending |
            Select-Object Count, Name |
            Format-Table -AutoSize |
            Out-String
        $report.Add($table.TrimEnd())
        $report.Add("Unique source diagnostics: $($diagnostics.Count)")
        $report.Add("Previous source diagnostics: $PreviousErrorCount")
        $report.Add("Reduction: $($PreviousErrorCount - $diagnostics.Count)")

        $report.Add('=== Interface-list diagnostics (up to 60) ===')
        $interfaceErrors = @(
            $diagnostics | Where-Object {
                $_.Message -match '\b(IInterface|IInterfaceList|TInterfaceList)\b' -or
                $_.Path -match '\\__runtime\\InterfaceLists\.cs$'
            }
        )
        $report.Add("Interface-related diagnostic count: $($interfaceErrors.Count)")
        foreach ($item in ($interfaceErrors | Select-Object -First 60)) {
            $report.Add(('{0}({1},{2}): {3}: {4}' -f `
                $item.Path, $item.Line, $item.Column,
                $item.Code, $item.Message))
        }

        $report.Add('=== First diagnostic and source per category ===')
        foreach ($group in ($diagnostics | Group-Object Code)) {
            $item = $group.Group[0]
            $report.Add(('{0}({1},{2}): {3}: {4}' -f `
                $item.Path, $item.Line, $item.Column,
                $item.Code, $item.Message))

            if ([System.IO.File]::Exists($item.Path)) {
                $lines = [System.IO.File]::ReadAllLines($item.Path, $utf8)
                $first = [Math]::Max(1, $item.Line - 2)
                $last = [Math]::Min($lines.Length, $item.Line + 2)
                for ($number = $first; $number -le $last; $number++) {
                    $report.Add(('{0}: {1}' -f $number, $lines[$number - 1]))
                }
            }
        }

        $report.Add('=== Missing type inventory ===')
        $missingTypes = foreach ($item in $diagnostics) {
            if ($item.Code -eq 'CS0246') {
                $match = [regex]::Match($item.Message, "'(?<Name>[^']+)'")
                if ($match.Success) {
                    $match.Groups['Name'].Value
                }
            }
        }
        $table = $missingTypes |
            Group-Object |
            Sort-Object Count -Descending |
            Select-Object Count, Name |
            Format-Table -AutoSize |
            Out-String
        $report.Add($table.TrimEnd())

        $report.Add('=== Build totals ===')
        foreach ($line in ($compiler.Lines | Select-Object -Last 12)) {
            $report.Add($line)
        }
        $report.Add("Generated build exit code: $($compiler.ExitCode)")
    }

    $report.Add("Full logs: $directory")
    [System.IO.File]::WriteAllLines(
        (Join-Path $directory 'summary.log'), $report.ToArray(), $utf8)
}
catch {
    $report.Add("STOP: $($_.Exception.Message)")
}
finally {
    if (Test-Path Variable:oldTemp) {
        $env:TEMP = $oldTemp
        $env:TMP = $oldTmp
    }
}

$output = $report -join [Environment]::NewLine
Write-Host $output
try {
    Set-Clipboard -Value $output -ErrorAction Stop
    Write-Host 'Output copied to clipboard.'
}
catch {
    Write-Host "Clipboard failed: $($_.Exception.Message)"
}