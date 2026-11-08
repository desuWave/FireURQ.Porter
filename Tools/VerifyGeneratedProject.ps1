param(
    [string]$RootPath = (Split-Path -Parent $PSScriptRoot),
    [int]$PreviousErrorCount = 142,
    [string[]]$SourceRoots = @(
        'E:\EVI_DATA\EVI_Projects\fireurq-master',
        'E:\EVI_DATA\EVI_Projects\d2dge-furq_2_2_4'
    )
)

$ErrorActionPreference = 'Stop'
$utf8 = [System.Text.UTF8Encoding]::new($false)
$report = [System.Collections.Generic.List[string]]::new()

try {
    $directory = Join-Path ([System.IO.Path]::GetTempPath()) (
        'FireURQ.ProjectVerification.' + [Guid]::NewGuid().ToString('N'))
    [System.IO.Directory]::CreateDirectory($directory) | Out-Null
    $report.Add("Verification directory: $directory")

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

$output = $report -join [Environment]::NewLine
Write-Host $output
try {
    Set-Clipboard -Value $output -ErrorAction Stop
    Write-Host 'Output copied to clipboard.'
}
catch {
    Write-Host "Clipboard failed: $($_.Exception.Message)"
}