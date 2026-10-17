# FireURQ.Porter

**FireURQ.Porter** is a Delphi source-porting and analysis toolkit being developed to support the port of the FireURQ interpreter and its D2D/DirectX8 subsystem to C#/.NET.

It is not merely a text-to-text converter. Its pipeline combines lexical analysis, parsing, AST construction, semantic modelling, cross-root dependency analysis, declaration/implementation matching, method-body analysis, and Delphi-to-C# translation.

> **Current status:** the Porter application builds, and the selected FireURQ + D2D source set has been processed successfully. The generated C# is still undergoing semantic cleanup and integration; this repository does **not** claim that the complete translated FireURQ application already compiles or runs.

## Latest verified full-source run

The latest recorded run used two source roots together:

- FireURQ interpreter source
- D2D / DirectX8 subsystem source

| Metric | Result |
|---|---:|
| Delphi source files discovered | 58 |
| Files parsed successfully | 58 / 58 |
| Parser failures | 0 |
| Tokens | 156,762 |
| Classes | 133 |
| Interfaces | 28 |
| Types | 123 |
| Constants | 808 |
| Functions | 714 |
| Project graph nodes | 3,862 |
| Project graph edges | 6,192 |
| Symbols | 4,513 |
| Semantic references/edges | 13,002 |
| Units in combined project dependency graph | 58 |
| Unit dependency edges | 4,735 |
| Files translated successfully | 58 / 58 |
| Files failed during translation | 0 |
| Function bodies translated | 603 |
| TODO markers in generated output | 120 |

These are results from the recorded local run, not a claim that every translated function is semantically complete. The 120 remaining TODO markers are part of the outstanding porting work.

The run generated analysis reports, a combined project dependency graph, and translated source files. The precise output paths depend on the local working directory and command invocation.

## Processing pipeline

```text
Delphi source files (FireURQ + D2D)
                |
                v
          Lexical analysis
                |
                v
              Parser
                |
                v
               AST
                |
                v
       Semantic modelling
                |
                v
   Cross-root dependency graph
                |
                v
 Declaration / implementation matching
                |
                v
       Method-body analysis
                |
                v
       Delphi-to-C# translation
                |
                v
    Semantic cleanup and integration
                |
                v
      Buildable FireURQ on .NET
```

The final integration step is **not yet complete**.

## Capabilities

### Lexer and parser

- Tokenizes Delphi source and parses it into structured AST representations.
- Supports the legacy Windows code-page input used by the original source files.
- Represents declarations, types, constants, classes, methods, and other program structures for later analysis.

### Semantic and project analysis

- Builds semantic representations of units, types, classes, constants, functions, methods, parameters, and references.
- Analyses multiple source roots as one project, preserving relationships that cross between FireURQ and D2D.
- Builds dependency information and exports graph data as JSON.

### Declaration and implementation analysis

- Indexes method bodies independently of declarations.
- Provides method and implementation censuses, declaration/header tracing, method-resolution auditing, body-owner probing, and expression/body analysis.
- Matches implementations to declarations to support translation of actual method bodies.

### Delphi-to-C# translation

- Translates declarations and method implementations into C#.
- Includes transformations for constructs encountered in the real source, including Delphi `case` to C# `switch`, operator handling, and Pascal record constant initializers.
- Keeps translation improvements tied to actual FireURQ/D2D source patterns rather than only synthetic examples.

### Verified record-initializer fix

A concrete regression in the generated `d2dGUIMenus.cs` output was fixed. The Pascal constant `cDefaultMenuVisualStyle` now translates to a C# object initializer, preserving all eight fields and their values:

```csharp
public static readonly Td2dMenuVisualStyle cDefaultMenuVisualStyle = new Td2dMenuVisualStyle { rBGColor = 0xFFFFFFFF, rBorderColor = 0xFFA0A0A0, rTextColor = 0xFF000000, rHIndent = 2, rVIndent = 2, rSelectionColor = 0xFF0000A0, rSelectedColor = 0xFFFFFFFF, rDisabledColor = 0xFFC0C0C0 };
```

The fix addresses trailing whitespace in top-level splitting that previously caused the initializer conversion to fail. The generated output was inspected after the fix.

## Command-line interface

FireURQ.Porter is a .NET 8 console application.

### Build

```powershell
dotnet build
```

### Analyse one or more source roots

```powershell
dotnet run -- --analyze "path\to\FireURQ" "path\to\D2D"
```

Multiple root directories can be passed to a single analysis run.

### Analyse and export a dependency graph

```powershell
dotnet run -- --analyze "path\to\source" --export-graph "path\to\project.graph.json"
```

### Parse and analyse a single Delphi file

```powershell
dotnet run -- "path\to\unit.pas"
```

### Parse a single file and export its dependency graph

```powershell
dotnet run -- "path\to\unit.pas" --export-graph "path\to\unit.graph.json"
```

### Translation test

```powershell
dotnet run -- --translate-test
```

This uses the repository's translation test source and exercises declaration and function translation.

### Method-body analysis

```powershell
dotnet run -- --analyze-bodies "path\to\source"
dotnet run -- --analyze-bodies "path\to\source" --progress
```

### Browse method bodies

```powershell
dotnet run -- --browse-bodies "path\to\source"
```

### Probe the owner of a method body

```powershell
dotnet run -- --probe-body-owner "path\to\source" "MethodName"
```

### Probe expressions

```powershell
dotnet run -- --probe-expressions "path\to\source"
```

### Audit method resolution

```powershell
dotnet run -- --audit-methods "path\to\source"
```

### Census method implementations and headers

```powershell
dotnet run -- --census-methods "path\to\source"
dotnet run -- --census-headers "path\to\source"
dotnet run -- --trace-headers "path\to\source"
```

### Inspect Pascal variant-record handling

```powershell
dotnet run -- --probe-variant-records "path\to\unit.pas"
```

This diagnostic mode checks the AST and semantic representation, inspects the generated C# layout, and performs a runtime layout probe for the supported variant-record case.

## Repository structure

The main code is organized by processing responsibility:

- `AST/` — Delphi syntax-tree representations and related structures
- `Lexer/` — lexical analysis
- `Parser/` — Delphi parsing
- `Analysis/` — semantic, dependency, method, and body analysis
- `Translation/` — Delphi-to-C# translation
- `Tests/` — translation test sources and test material
- `Tools/` — supporting diagnostic utilities
- `Program.cs` — command-line entry point

The repository also contains project-graph and analysis artefacts used by the ongoing porting work.

## What is working

Based on the latest recorded local verification:

- The Porter application builds successfully on .NET 8, with **0 warnings and 0 errors**.
- The selected 58 Delphi files parse successfully.
- The combined FireURQ/D2D analysis and dependency graph are produced.
- All 58 files in the selected run pass through the translation stage.
- The translation stage has processed 603 function bodies.
- The Pascal record constant initializer for `cDefaultMenuVisualStyle` is emitted as a C# object initializer after the whitespace-splitting fix.

## What remains unfinished

FireURQ.Porter is an active porting toolkit, not a finished compiler or a guarantee of a complete automatic conversion.

Remaining work includes:

- resolving the 120 TODO markers recorded in the latest generated output;
- correcting remaining Delphi constructs and C# expressions that do not yet translate correctly;
- completing declaration ordering and generated-source integration where needed;
- checking and fixing compilation errors in the generated C# application;
- validating the behavior of the resulting FireURQ implementation.

**Important distinction:** a successful build of FireURQ.Porter verifies the tool itself. Successful parsing and translation of the 58 source files verifies those pipeline stages. Neither result alone proves that the generated FireURQ application builds or behaves correctly.

## Project goal

The goal is to use structured parsing and semantic analysis to turn the original FireURQ Delphi codebase—including its D2D/DirectX8 subsystem—into an increasingly complete, analyzable C# representation, and then systematically resolve the remaining issues until the port can be built and integrated with the wider FireURQ 3D / EVI3D work.

## License

License and redistribution terms for the original source material and the resulting port should be established explicitly before redistribution.

## Project

- Repository: https://github.com/desuWave/FireURQ.Porter
- Main development branch: `main`
