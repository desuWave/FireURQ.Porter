FireURQ.Porter

FireURQ.Porter is a specialized source-porting and analysis toolset for the FireURQ interpreter.

It is not limited to a simple source-to-source translator.

Porter provides a complete processing pipeline for the current FireURQ porting work:

Delphi source → lexical analysis → parsing → AST → semantic analysis → dependency analysis → implementation matching → body analysis → C# translation → generated project/source data

The project is being developed as part of the FireURQ → C#/.NET porting effort and is intended to make the structure and behavior of the original Delphi codebase explicit enough to continue the port toward a buildable and functional C# implementation.

---

Current status

«Development status: active porting / semantic cleanup»

The current version of FireURQ.Porter is capable of processing the complete currently selected FireURQ source set used for the porting work.

Full-source processing result

The current combined source run covers 58 Delphi source files from two source roots:

- FireURQ — 31 files
- D2D / DirectX8 subsystem — 27 files

Current processing result:

Stage| Result
Delphi files discovered| 58
Files parsed successfully| 58 / 58
Files failed to parse| 0
Translated function bodies| 261
Remaining TODO markers in translation run| 0

The source analysis also produces project/dependency graph information for the combined source tree.

The project graph is intentionally treated as a cross-root graph: declarations and implementations may belong to different source roots, so resolving each root independently would lose important relationships.

---

What Porter currently does

1. Delphi lexical analysis

Porter contains its own Delphi lexer.

It converts source text into a token stream which is subsequently consumed by the parser.

The executable also handles legacy Windows code-page input used by the original sources.

---

2. Delphi parsing

The parser converts Delphi source into a structured AST.

The AST represents the source program in a form that can be inspected and processed by later stages rather than translating text directly.

This is important for FireURQ because the original project contains declarations, implementations, classes, types, constants, methods and dependencies that cannot be translated reliably using simple textual substitutions.

---

3. AST and semantic representation

After parsing, Porter builds semantic representations of the source.

The semantic stage is used to identify and relate:

- units;
- classes;
- types;
- constants;
- functions;
- methods;
- declarations;
- implementations;
- parameters;
- return types;
- references between source elements.

This allows later stages to operate on program structure instead of isolated lines of Delphi code.

---

4. Cross-root dependency analysis

Porter can analyse multiple source roots as one project.

For the FireURQ port this is particularly important because the original code is split between the main FireURQ source tree and the D2D/DirectX8 subsystem.

The analyzer builds a project-level dependency graph and can export that graph as JSON.

The repository currently contains generated project graph data:

- "fireurq.graph.json"
- "fireurq.project.graph.json"

These files are part of the current porting analysis and make the discovered project structure inspectable outside the analyzer itself.

---

5. Method implementation matching

Porter separates the problem of finding a method declaration from finding its actual implementation.

The current toolset contains dedicated functionality for:

- method census;
- implementation census;
- implementation/header census;
- implementation/header tracing;
- method-resolution auditing;
- probing the owner of a method body.

This is necessary for Delphi code where declarations and implementations are not necessarily located together.

---

6. Method-body analysis

Porter can index and inspect method bodies independently from their declarations.

Current tooling includes:

- body indexing;
- body browsing;
- body-owner probing;
- batch body analysis;
- expression probing;
- behavioral analysis of method bodies.

This gives the translation pipeline information about the actual implementation rather than attempting to generate C# from declarations alone.

---

7. Delphi → C# translation

The repository contains a Delphi-to-C# translation pipeline.

It includes separate handling for:

- declarations;
- semantic functions;
- method implementations;
- analyzed method behavior.

The current translation pipeline has already been run across the full 58-file FireURQ/D2D source set.

The translator is therefore no longer operating only on isolated demonstration snippets: it has been exercised against the complete currently selected source set.

---

8. Declaration translation

Declarations are translated separately from method bodies.

This allows the porting process to establish the C# type/declaration structure before resolving all implementation details.

The translation test pipeline explicitly performs:

1. lexical analysis;
2. parsing;
3. semantic analysis;
4. body discovery;
5. declaration translation;
6. implementation matching;
7. behavior analysis;
8. function translation.

---

9. Delphi language transformations

The translation layer already handles a growing set of Delphi constructs and transformations required by the FireURQ source.

Examples of implemented translation work include:

- Delphi operators → C# operators;
- "case" structures → C# "switch";
- Delphi declarations → C# declarations;
- Delphi method implementations → C# methods.

The translation layer is intentionally being extended according to constructs encountered in the real FireURQ source rather than being limited to a synthetic language subset.

---

Analysis and diagnostic tools

The executable currently exposes a number of specialized analysis modes.

Full directory analysis

FireURQ.Porter --analyze <directory>

Analyze a source directory.

Multiple source roots can be supplied to the analysis pipeline.

---

Export dependency graph

FireURQ.Porter --analyze <directory> --export-graph <file.json>

Build the project dependency graph and export it as JSON.

---

Analyze a single Delphi file

FireURQ.Porter <file.pas>

The single-file pipeline performs lexical analysis, parsing, AST output, semantic analysis and dependency-graph construction.

---

Export a single-file dependency graph

FireURQ.Porter <file.pas> --export-graph <file.json>

---

Translation test

FireURQ.Porter --translate-test

Runs the translation pipeline against the repository's translation test source.

The test exercises declaration translation and method-body translation.

---

Body browsing

FireURQ.Porter --browse-bodies <directory>

Builds a method-body index and opens the body browser.

---

Body-owner probing

FireURQ.Porter --probe-body-owner <directory> <body>

Attempts to identify the owning implementation for a specified body.

---

Expression probing

FireURQ.Porter --probe-expressions <directory>

Runs expression-level analysis against a source directory.

---

Body analysis

FireURQ.Porter --analyze-bodies <directory>

Performs batch analysis of method bodies.

Progress can be displayed with:

FireURQ.Porter --analyze-bodies <directory> --progress

---

Method resolution audit

FireURQ.Porter --audit-methods <directory>

Audits method-resolution information discovered during analysis.

---

Method census

FireURQ.Porter --census-methods <directory>

Produces a census of method implementations.

---

Header census

FireURQ.Porter --census-headers <directory>

Produces a census of implementation headers.

---

Header tracing

FireURQ.Porter --trace-headers <directory>

Traces implementation/header relationships.

---

Project structure

The repository is organized around the stages of the porting pipeline:

FireURQ.Porter/
├── AST/
├── Analysis/
├── Lexer/
├── Parser/
├── Translation/
├── Tests/
├── Tools/
├── Program.cs
├── FireURQ.Porter.csproj
├── fireurq.graph.json
├── fireurq.project.graph.json
├── lexer.patch
└── porter-current.diff

The main processing layers are deliberately separated so that source parsing, semantic analysis and translation can evolve independently.

---

Build

FireURQ.Porter is currently a .NET 8 console application.

The project file targets:

net8.0

Build it with:

dotnet build

Run it with:

dotnet run -- <arguments>

For example:

dotnet run -- --translate-test

or:

dotnet run -- --analyze <source-directory>

---

What is already working

The current state should be understood precisely.

Working

- FireURQ.Porter itself builds as a .NET 8 executable.
- Delphi source can be lexed.
- Delphi source can be parsed.
- AST structures can be produced.
- Semantic analysis can be performed.
- Multiple source roots can be analysed together.
- Cross-root dependency information can be constructed.
- Dependency graphs can be exported.
- Method declarations and implementations can be analysed separately.
- Method bodies can be indexed and inspected.
- Implementations can be matched against declarations.
- Expressions and method bodies can be probed/analyzed.
- Delphi declarations can be translated to C#.
- Delphi method implementations can be translated to C#.
- The current full-source run processes 58/58 Delphi files without parser failures.
- The current translation run has processed 261 function bodies.

---

What is not finished yet

FireURQ.Porter should not currently be described as a completed FireURQ compiler or as a finished automatic Delphi-to-C# converter.

The generated C# FireURQ source is currently in the porting and semantic-cleanup stage.

In particular, the generated program still requires further work involving issues such as:

- declaration ordering;
- generated source structure;
- individual Delphi constructs whose C# representation still requires correction;
- malformed generated literals/expressions in some translated areas;
- final integration of the translated source into a buildable FireURQ program.

Therefore:

«Porter can successfully process and translate the current FireURQ source set, but the resulting FireURQ C# program is not yet guaranteed to compile or function as the final FireURQ implementation.»

This distinction is intentional.

The purpose of the current stage is to turn the original Delphi program into a structurally understood and increasingly complete C# representation, while exposing the remaining semantic problems so they can be corrected systematically.

---

Design goal

The long-term goal of FireURQ.Porter is not merely to perform textual conversion.

The intended pipeline is:

Original FireURQ Delphi source
            │
            ▼
        Lexer
            │
            ▼
         Parser
            │
            ▼
           AST
            │
            ▼
   Semantic analysis
            │
            ▼
 Dependency / project graph
            │
            ▼
 Declaration + implementation
      resolution
            │
            ▼
      Body analysis
            │
            ▼
      Delphi → C#
       translation
            │
            ▼
 Semantic cleanup / integration
            │
            ▼
   Buildable FireURQ on .NET

FireURQ.Porter is therefore both a translator and a porting-analysis environment.

---

Relationship to the FireURQ port

FireURQ.Porter exists specifically to support the larger effort of bringing the original FireURQ engine from its Delphi/DirectX8 environment into a modern C#/.NET implementation.

The D2D/DirectX8 source tree is not treated as an unrelated external dependency. It is part of the source graph being reconstructed by the porting process.

The resulting C# implementation is intended to become the basis for the future FireURQ 3D/F3D work and its integration into the broader EVI3D project.

---

Current milestone

Current milestone: full-source translation completed; semantic integration in progress.

The important transition represented by the current repository state is:

"Can Porter process the FireURQ sources?"
                ↓
             YES
                ↓
"Can Porter translate the complete selected source set?"
                ↓
             YES
                ↓
"Does the generated FireURQ C# already form the final buildable program?"
                ↓
          NOT YET
                ↓
"Can the remaining problems now be analysed and corrected
 systematically inside Porter?"
                ↓
              YES

This repository therefore represents the active working instrument for the next stage of the FireURQ port rather than a claim that the port is already complete.

---

License

License information will be added/updated as the FireURQ porting project establishes the appropriate distribution terms for the original source material and the resulting port.

---

Project

FireURQ.Porter
Part of the FireURQ / EVI3D porting effort.

Repository:

https://github.com/desuWave/FireURQ.Porter
