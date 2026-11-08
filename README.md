# FireURQ.Porter

Delphi analysis and source-porting toolkit for migrating FireURQ and its D2D/DirectX8 subsystem to C#/.NET 8.

## Current status

The translator builds successfully. The complete generated application does not yet compile.

Latest recorded full-source verification:
- 58 Delphi files discovered, parsed and translated; zero failures.
- 1,674 function bodies translated; 67 TODO markers.
- 68 generated C# files.
- Translator Release build: zero warnings and errors.
- Generated build: 105 unique errors and 22 warnings.
- Errors: 101 unresolved types, three missing Direct3D constants and one inherited abstract-member conflict.

## Implemented capabilities

- Preprocessing, parsing, AST and semantic modelling, cross-unit resolution and dependency analysis.
- Overload-aware implementation matching, grouped locals and absolute-alias metadata.
- Supported class casts, parameterless calls, virtual dispatch and Boolean alias operators.
- Null-safe TObject.Free, destructor scopes and explicit interface reference counting.
- Memory/file streams, scalar/array/native buffers, short reads and exact reads.
- Pointer lists and owning/non-owning object lists, including extraction and sorting.
- Interface lists with explicit retained references per cell.
- JCL string lists with associated values, sorting, duplicate policies and nested lists.
- Standard string lists with borrowed object associations and custom sorting.
- Byte-preserving AnsiString values, explicit encodings, pinned buffers and stream adapters.
- Independently tested TPoint/TRect layouts and native-address operations: 19/19 checks passed.

## Verification

Regression projects are under Tools. Many compile generated C# and execute behavioural assertions.
Run a regression with: dotnet run --project Tools/GeometryRuntimeRegression/GeometryRuntimeRegression.csproj --configuration Release
Build the translator with: dotnet build FireURQ.Porter.csproj --configuration Release
Analyse sources with: dotnet run -- --analyze "path-to-FireURQ" "path-to-D2D"
Verify generated compilation with: & ./Tools/VerifyGeneratedProject.ps1 -PreviousErrorCount 105

## Compatibility boundaries and remaining work

- Geometry runtime emission, PPoint translation and WinAPI argument adaptation remain unfinished.
- C# interface assignment and borrowed reads do not automatically reproduce Delphi reference management.
- JCL associated-interface storage has a separate lifetime contract from interface-list cells.
- Standard string-list object associations are borrowed; descendants may explicitly own cleanup.
- Object-list callback tokens are managed object handles, not raw object-data addresses.
- AnsiString text conversions require explicit encoding; general pointer/record-field adaptation remains unfinished.
- Direct3D, VCL, graphics and other external dependencies remain unresolved.
- Constructor/factory translation, member-hiding semantics and the remaining TODO markers require further work.
- Missing declarations can conceal additional compiler diagnostics.

Successful translator builds and focused regressions do not establish that the complete application compiles or behaves correctly.

## Structure

AST, Lexer, Parser and Analysis contain the processing pipeline. Translation contains C# generation and runtime templates. Tests contains fixtures; Tools contains regressions and diagnostics.

## Project

Repository: https://github.com/desuWave/FireURQ.Porter
Main development branch: main.
Goal: support the FireURQ/D2D migration toward the wider FireURQ 3D / EVI3D work.

## Licensing

Consult repository and upstream license terms. This README grants no additional rights to original sources or third-party dependencies.
