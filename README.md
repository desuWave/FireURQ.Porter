# FireURQ.Porter

FireURQ.Porter translates Delphi sources from [FireURQ](https://github.com/fireton/fireurq) and [D2D Game Engine](https://github.com/fireton/d2dge) into C# for .NET 8. It also provides focused regression tools and a Windows Direct3D 8 preview.

**The translator builds. The complete generated FireURQ application is still under development and does not compile.** The preview builds selected generated methods with a small Windows host. It can show the splash and main skin, open supported quests from the system menu, follow links, toggle the inventory and display interactive text decorations.

## Requirements

- Git and the .NET 8 SDK for building the translator and running regressions.
- For the Windows verification scripts and GUI, use 64-bit Windows with Windows PowerShell 5.1 (`powershell.exe`), its 32-bit worker under `SysWOW64`, and a graphics driver capable of creating a Direct3D 8 device.
- For the F2 GUI, use `fireurq.pak` from your FireURQ 2.2.4 installation. The preview reads the skin, texture and font assets from the archive. Leave it packed.
- Delphi source checkouts are required for full translation and the GUI preview. Delphi itself is unnecessary for these commands.

## Build the translator

Run the following in PowerShell from a directory where you want the three checkouts:

```powershell
git clone https://github.com/desuWave/FireURQ.Porter.git
git clone https://github.com/fireton/fireurq.git fireurq-master
git clone --branch furq_2_2_4 https://github.com/fireton/d2dge.git d2dge-furq_2_2_4
Set-Location .\FireURQ.Porter

dotnet --list-sdks
dotnet build .\FireURQ.Porter.csproj -c Release
```

Keep the source directory names shown above. The focused probes locate generated units under those directory names. In a new PowerShell session, recreate these variables from the Porter checkout before running the examples:

```powershell
$sources = @(
    (Resolve-Path '..\fireurq-master').Path
    (Resolve-Path '..\d2dge-furq_2_2_4').Path
)
$fireurq = $sources[0]
```

For a quick regression that needs no upstream checkout:

```powershell
dotnet run --project .\Tools\GeometryRuntimeRegression\GeometryRuntimeRegression.csproj -c Release
```

## Generate C# and inspect compiler diagnostics

```powershell
& .\Tools\VerifyGeneratedProject.ps1 -SourceRoots $sources -PreviousErrorCount 71 -CheckQuestEntryPoints -CheckD3D8RenderTargets
```

The verifier prints a `Verification directory:` path under `.fireurq-work`. It keeps the generated C# under `translation-output` and stores `translator-build.log`, `translation.log`, `generated-build.log` and `summary.log` alongside it.

In the last recorded full verification, the translator built cleanly and translated 58 Delphi files and 1,674 function bodies. The generated project reported 71 unique missing-type diagnostics (`CS0246`). Use the individual stage exit codes and diagnostics to read the result. When missing types are resolved, the compiler may expose additional errors.

## Standalone FireURQ build target

[`Standalone/FireURQ`](Standalone/FireURQ) is the application project. It compiles C# sources directly and has no project reference to FireURQ.Porter. The verification script builds this project against its generated C# and places the build artifacts in the verification directory.

To build the target directly from an existing translation, run the following from the repository root. Replace the example path with the `translation-output` directory inside the verification directory reported by the script.

```powershell
dotnet build ".\Standalone\FireURQ\FireURQ.csproj" -c Release "-p:FireURQSourceRoot=C:\path\to\translation-output"
```

This target currently reports the outstanding C# compilation errors. A runnable standalone executable requires the translated application and its entry point to compile. The F2 preview below is available for the Windows GUI checks that already work.

## Run the F2 preview

Replace the example archive path with the actual path to your installed copy:

```powershell
$pak = 'C:\Games\FireURQ 2.2.4\fireurq.pak'
& .\Tools\ShowD3D8RenderTexture.ps1 -SourceRoots $sources -SplashPak $pak
```

The script generates fresh C# before launching the preview. A failed full generated build can appear in the report even when the focused preview opens successfully. Look for `Probe exit code: 0` after closing the window. The preview remains open in interactive mode until you close it or select Exit from the system menu.

### Upstream FireURQ 1.8 fixtures

All standalone scenarios in the upstream [tests/ver1.8 directory](https://github.com/fireton/fireurq/tree/master/tests/ver1.8) were exercised manually in the Windows F2 preview. Open them through the system menu. Keep the adjacent include files in place.

| Fixture | Confirmed preview behavior |
| --- | --- |
| `167.includes.qst` | Links reach the included locations, including text from the nested third include, and return to the start. |
| `167.includes\167.includes.qsz` | The same nested includes work from the archive. The missing `bla.qst` diagnostic appears in red, and the quest remains playable. Opening another quest clears that diagnostic. |
| `197.inv_disable.qst` | The spoon and fork appear in the inventory. The links disable and re-enable the inventory while retaining the current text and both items. |
| `201.comment_error.qst` | A quest without `:0` starts at the beginning and prints `hello, world!` after the block comment. |
| `202.simple_links.qst` | The brick and glass links open their respective locations. |
| `204.link_error.qst` | The outer brackets stay visible around the link, and clicking it shows `clicked`. |
| `206.linkdeco.qst` | The text decoration changes color, runs a looping movement script, and opens a three-item menu whose options reset the decoration. |
| `210.droplinks.qst` | Dropping links leaves the text visible and keeps the restart button available. Restart restores the inline link. |

The included `inc1.qst` and files under `inc/` are exercised through the two `167.includes` scenarios. A separate manual check confirmed that opening a damaged QSZ shows an error and leaves the current quest usable.

To open the simple-links fixture at startup:

```powershell
$quest = Join-Path $fireurq 'tests\ver1.8\202.simple_links.qst'
& .\Tools\ShowD3D8RenderTexture.ps1 -SourceRoots $sources -SplashPak $pak -QuestPath $quest
```

Use `-VersionExe` with the path to the original FireURQ executable to show its product version on the splash. The preview reads its version metadata. Use `-AutoTest` with `-SplashPak` for the timed run that exercises Reset/Present.

For a smaller test of the generated quest loader, paste the actual verification directory reported by the verifier above. `-ShowWindow` opens a separate WinForms test window:

```powershell
$verification = Read-Host 'Paste the Verification directory path'
& .\Tools\TestGeneratedQuestLoader.ps1 -VerificationDirectory $verification -QuestPath $quest -ShowWindow
```

The loader test for this fixture should report `PASS` for locations `0` and `steklo` and exit with code 0. If the F2 preview fails, include the first `STOP:` line or failing HRESULT, the probe exit code and the relevant verification log when reporting the issue. The scripts keep their project verification files under `.fireurq-work`, which can be cleared between runs.

## What is implemented

- Delphi preprocessing, parsing, semantic modelling, dependency analysis and C# generation. Nested Delphi routines retain their declarations and bodies.
- Runtime support for Delphi streams, collections, JCL string lists, AnsiString, geometry and selected native-address operations.
- Direct3D 8 interop and focused probes for buffer locking and binding, render textures, depth targets, projection matrices and Reset/Present.
- F2 splash and main-skin preview, basic quest text and links, and loading of plain QST and ZIP-based QSZ quests with nested includes.

The preview exercises only a subset of quest commands. The original interpreter, full menus and gameplay, save/load, audio and binary QS1/QS2 support are outside the verified preview. The full generated project still has unresolved Delphi/VCL and other external types.

`AST`, `Lexer`, `Parser` and `Analysis` contain the source-processing pipeline. `Translation` contains the C# generator and runtime templates. `Tests` and `Tools` contain fixtures, regressions and diagnostics.

## Licensing

Consult the repository and upstream license terms for source code, resources and third-party components.
