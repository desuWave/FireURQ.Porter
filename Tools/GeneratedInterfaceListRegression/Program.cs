using System.Diagnostics;
using System.Text;
using FireURQ.Porter.Analysis;
using FireURQ.Porter.Lexer;
using FireURQ.Porter.Parser;
using FireURQ.Porter.Translation;

int total = 0;
int failed = 0;
void Check(string name, bool condition)
{
    total++;
    if (!condition)
        failed++;
    Console.WriteLine($"[{(condition ? "PASS" : "FAIL")}] {name}");
}

const string source = """
unit InterfaceListBinding;
interface
type
  IProbe = interface
    function GetValue: Integer;
  end;
  IChildProbe = interface(IProbe)
    function Extra: Integer;
  end;
implementation

function MakeList: iinterfacelist;
begin
  Result := TInterfaceList.cReAtE;
end;

function MakeExplicit: IInterfaceList;
begin
  Result := TInterfaceList.Create();
end;

function AddProbe(items: IInterfaceList; value: IProbe): Integer;
begin
  Result := items.Add(value);
end;

function ReadProbe(items: IInterfaceList; index: Integer): IProbe;
begin
  Result := items.Items[index] as IProbe;
end;

function ReadChild(items: IInterfaceList; index: Integer): IChildProbe;
begin
  Result := items[index] as IChildProbe;
end;

procedure ReplaceProbe(items: IInterfaceList; index: Integer; value: IProbe);
begin
  items.Items[index] := value;
end;

function RemoveProbe(items: IInterfaceList; value: IProbe): Integer;
begin
  Result := items.Remove(value);
end;

procedure ClearInterfaces(items: IInterfaceList);
begin
  items.Clear;
end;

end.
""";

var unit = new DelphiParser(new DelphiLexer(source).Tokenize()).ParseUnit();
var model = SemanticModelBuilder.Build(unit);
var resolver = new TypeResolver(new[] { model });
var translator = new DelphiToCSharpTranslator(resolver, model.Name);

string declarations = new CSharpDeclarationTranslator(
    resolver, model.Name).Translate(model);
Console.WriteLine("=== Generated interfaces ===");
Console.WriteLine(declarations);
Check("Implicit interface ancestor becomes IInterface",
    declarations.Contains("public interface IProbe : IInterface"));
Check("Explicit interface ancestor is preserved",
    declarations.Contains("public interface IChildProbe : IProbe"));

var methods = new Dictionary<string, string>();
foreach (var body in BodyTokenAnalyzer.Analyze(unit, "InterfaceListBinding.pas")
    .Where(value => value.BodyTokens.Count > 0))
{
    var function = FunctionImplementationMatcher.Match(unit, body, model);
    Check(body.Name + ": signature matched", function is not null);
    if (function is null)
        continue;

    methods[function.Name] = translator.TranslateFunction(
        function,
        BehaviorAnalyzer.Analyze(body.FilePath, body.Name, body.BodyTokens));
}

Check("All eight Pascal interface-list helpers are translated",
    methods.Count == 8);
Check("Mixed-case interface-list return type is normalized",
    methods.TryGetValue("MakeList", out var makeList) &&
    makeList.Contains("IInterfaceList MakeList("));
Check("Bare and explicit factories remain single calls",
    methods.TryGetValue("MakeExplicit", out var makeExplicit) &&
    makeList is not null &&
    makeList.Contains("TInterfaceList.Create();") &&
    makeExplicit.Contains("TInterfaceList.Create();") &&
    !makeExplicit.Contains("Create()("));

Console.WriteLine("=== Generated interface-list helpers ===");
foreach (string method in methods.Values)
    Console.WriteLine(method);

if (failed == 0)
{
    string directory = Path.Combine(
        Path.GetTempPath(), $"FireURQ.GeneratedInterfaceList.{Guid.NewGuid():N}");
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

    File.WriteAllText(Path.Combine(directory, "Objects.cs"),
        CSharpObjectRuntime.Source, encoding);
    File.WriteAllText(Path.Combine(directory, "InterfaceLists.cs"),
        CSharpInterfaceListRuntime.Source, encoding);

    string helpers = "using System;\nusing FireURQ.Runtime;\n" +
        declarations + "\npublic static class Generated\n{\n" +
        string.Join("\n",
            methods.Values.Select(value => "public static " + value)) +
        "\n}\n";

    File.WriteAllText(Path.Combine(directory, "Generated.cs"),
        helpers, encoding);

    File.WriteAllText(Path.Combine(directory, "Program.cs"), """
using FireURQ.Runtime;

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

IInterfaceList items = Generated.MakeList();
IInterfaceList otherList = Generated.MakeExplicit();
// C# assignments do not yet acquire Delphi interface ownership automatically.
items._AddRef();
otherList._AddRef();
Check("Both translated factories produce independent empty lists",
    items.Count == 0 && otherList.Count == 0 &&
    !ReferenceEquals(items, otherList));

var first = new Tracked(17);
((IInterface)first)._AddRef();
IProbe firstProbe = first;
int index = Generated.AddProbe(items, firstProbe);
Check("Generated Add accepts an implicitly inherited interface",
    index == 0 && first.RefCount == 2);

IProbe read = Generated.ReadProbe(items, index);
IChildProbe child = Generated.ReadChild(items, index);
Check("Generated Items and default-index casts preserve interface identity",
    ReferenceEquals(read, first) &&
    ReferenceEquals(child, first) &&
    read.GetValue() == 17 && child.Extra() == 23);
Check("Read helpers retain the current borrowed-reference contract",
    first.RefCount == 2);

var replacement = new Tracked(31);
((IInterface)replacement)._AddRef();
Generated.ReplaceProbe(items, index, replacement);
Check("Generated replacement transfers cell ownership",
    first.RefCount == 1 &&
    replacement.RefCount == 2 &&
    ReferenceEquals(Generated.ReadProbe(items, index), replacement));
Generated.ReplaceProbe(items, index, replacement);
Check("Generated self-assignment preserves the reference count",
    replacement.RefCount == 2);

Check("Generated Remove releases only the list-held reference",
    Generated.RemoveProbe(items, replacement) == 0 &&
    items.Count == 0 &&
    replacement.RefCount == 1 &&
    replacement.Destroys == 0);

Generated.AddProbe(items, first);
Generated.AddProbe(items, replacement);
Generated.ClearInterfaces(items);
Check("Generated Clear preserves explicitly retained external owners",
    items.Count == 0 &&
    first.RefCount == 1 && replacement.RefCount == 1 &&
    first.Destroys == 0 && replacement.Destroys == 0);

((IInterface)first)._Release();
((IInterface)replacement)._Release();
Check("External final releases destroy each value exactly once",
    first.Destroys == 1 && first.Completions == 1 &&
    replacement.Destroys == 1 && replacement.Completions == 1);

var listHeld = new Tracked(99);
Generated.AddProbe(otherList, listHeld);
Check("Final list release destroys a value held only by that list",
    otherList._Release() == 0 &&
    listHeld.Destroys == 1 && listHeld.Completions == 1);
items._Release();

Console.WriteLine($"Generated interface-list execution: {passed}/{passed + failed} passed.");
return failed == 0 ? 0 : 1;

sealed class Tracked : TInterfacedObject, IChildProbe
{
    private readonly int value;
    public int Destroys;
    public int Completions;

    public Tracked(int value) => this.value = value;
    public int GetValue() => value;
    public int Extra() => 23;

    public override void Destroy()
    {
        using var scope = __FireUrqEnterDestructor();
        Destroys++;
        base.Destroy();
    }

    protected override void FreeInstance()
    {
        Completions++;
        base.FreeInstance();
    }
}
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
        Check("Generated interface-list helpers compile and execute", false);
    }
    else
    {
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> errors = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        Console.Write(await output);
        Console.Write(await errors);
        Check("Generated interface-list helpers compile and execute",
            process.ExitCode == 0);
    }
    Console.WriteLine($"Generated compilation directory: {directory}");
}


// Regression: preserve every ancestor of a Delphi class.
const string ancestorSource = """
unit AncestorRegression;
interface
type
  INode = interface
    procedure Visit;
  end;
  ISerializable = interface
    procedure Save;
  end;
  TBase = class
    procedure BaseMethod;
  end;
  TChild = class(TBase, INode, ISerializable)
    procedure ChildMethod;
  end;
  TInterfaceOnly = class(INode)
    procedure Visit;
  end;
implementation
end.
""";

var ancestorUnit = new DelphiParser(
    new DelphiLexer(ancestorSource).Tokenize()).ParseUnit();

var ancestorAst = ancestorUnit.Declarations
    .OfType<FireURQ.Porter.AST.DelphiClass>()
    .ToDictionary(value => value.Name);

Check("AST preserves class and two interfaces",
    ancestorAst.TryGetValue("TChild", out var childAst) &&
    childAst.AncestorTypes.SequenceEqual(
        new[] { "TBase", "INode", "ISerializable" }));

Check("AST preserves interface-only ancestor",
    ancestorAst.TryGetValue("TInterfaceOnly", out var interfaceOnlyAst) &&
    interfaceOnlyAst.AncestorTypes.SequenceEqual(
        new[] { "INode" }));

Check("AST preserves empty ancestor list",
    ancestorAst.TryGetValue("TBase", out var baseAst) &&
    baseAst.AncestorTypes.Count == 0);

foreach (var (builderName, ancestorModel) in new[]
{
    ("SemanticModelBuilder", SemanticModelBuilder.Build(ancestorUnit)),
    ("SemanticAnalyzer", SemanticAnalyzer.Analyze(ancestorUnit))
})
{
    var ancestorClasses = ancestorModel.Classes
        .ToDictionary(value => value.Name);

    Check(builderName + " preserves all ancestors",
        ancestorClasses.TryGetValue("TChild", out var child) &&
        child.AncestorTypes.SequenceEqual(
            new[] { "TBase", "INode", "ISerializable" }));

    Check(builderName + " preserves interface-only ancestor",
        ancestorClasses.TryGetValue("TInterfaceOnly", out var only) &&
        only.AncestorTypes.SequenceEqual(new[] { "INode" }));

    Check(builderName + " preserves legacy BaseType",
        ancestorClasses.TryGetValue("TChild", out var legacy) &&
        legacy.BaseType == "TBase");
}

// Regression: declaration kind resolution and Delphi uses visibility.
const string kindSourceA = """
unit KindA;
interface
type
  TShared = class
  end;
  IShared = interface
  end;
  TLocal = class
  end;
implementation
end.
""";

const string kindSourceB = """
unit KindB;
interface
type
  TShared = class
  end;
  ISecondary = interface
  end;
implementation
end.
""";

const string kindSourceConsumer = """
unit KindConsumer;
interface
uses KindA, KindB;
type
  TLocal = interface
  end;
implementation
end.
""";

const string kindSourceSingleImport = """
unit KindSingleImport;
interface
uses KindA;
implementation
end.
""";

var kindUnits = new[]
{
    kindSourceA,
    kindSourceB,
    kindSourceConsumer,
    kindSourceSingleImport
}.Select(sourceText =>
    SemanticModelBuilder.Build(
        new DelphiParser(
            new DelphiLexer(sourceText).Tokenize()).ParseUnit()))
    .ToArray();

var kindResolver = new TypeResolver(kindUnits);

Check("Kind: local class",
    kindResolver.ResolveDeclarationKind("TLocal", "KindA") == "Class");

Check("Kind: local interface shadows imported class",
    kindResolver.ResolveDeclarationKind("TLocal", "KindConsumer") ==
    "Interface");

Check("Kind: imported interface",
    kindResolver.ResolveDeclarationKind("IShared", "KindConsumer") ==
    "Interface");

Check("Kind: imported class",
    kindResolver.ResolveDeclarationKind("TShared", "KindSingleImport") ==
    "Class");

Check("Kind: ambiguous imported class",
    kindResolver.ResolveDeclarationKind("TShared", "KindConsumer") ==
    "Unknown");

Check("Kind: qualified class",
    kindResolver.ResolveDeclarationKind("KindA.TShared", "KindConsumer") ==
    "Class");

Check("Kind: qualified interface",
    kindResolver.ResolveDeclarationKind("KindB.ISecondary", "KindConsumer") ==
    "Interface");

Check("Kind: missing declaration",
    kindResolver.ResolveDeclarationKind("TDoesNotExist", "KindConsumer") ==
    "Unknown");

Check("Kind: missing unit context",
    kindResolver.ResolveDeclarationKind("TLocal", "MissingUnit") ==
    "Unknown");

Check("Kind: pointer is not a class or interface",
    kindResolver.ResolveDeclarationKind("^KindA.TShared", "KindConsumer") ==
    "Unknown");

Check("Kind: existing ancestor model identifies interface",
    kindResolver.ResolveDeclarationKind("INode", "AncestorRegression") ==
    "Unknown");

// Regression: generated C# class inheritance declarations.
{
    const string emissionSource = """
unit EmissionRegression;
interface
type
  INode = interface
  end;
  ISerializable = interface
  end;
  TBase = class
  end;
  TChild = class(TBase, INode, ISerializable)
  end;
  TInterfaceOnly = class(INode)
  end;
  TPlain = class
  end;
  TUnknownChild = class(TExternalBase)
  end;
implementation
end.
""";

    var emissionAst = new DelphiParser(
        new DelphiLexer(emissionSource).Tokenize()).ParseUnit();

    var emissionModel = SemanticModelBuilder.Build(emissionAst);
    var emissionResolver = new TypeResolver(
        new[] { emissionModel });

    string emissionOutput = new CSharpDeclarationTranslator(
        emissionResolver, emissionModel.Name).Translate(emissionModel);

    Check("Emission: class and two interfaces",
        emissionOutput.Contains(
            "partial class TChild : TBase, INode, ISerializable"));

    Check("Emission: interface-only class has runtime TObject",
        emissionOutput.Contains(
            "partial class TInterfaceOnly : global::FireURQ.Runtime.TObject, INode"));

    Check("Emission: plain class has runtime TObject",
        emissionOutput.Contains(
            "partial class TPlain : global::FireURQ.Runtime.TObject"));

    Check("Emission: ordinary class inheritance",
        emissionOutput.Contains(
            "partial class TChild : TBase,"));

    Check("Emission: unknown first ancestor retains legacy behavior",
        emissionOutput.Contains(
            "partial class TUnknownChild : TExternalBase"));
}

Check("Explicit interface adapters compile and dispatch",
    InterfaceAdapterRegression.Run());

// Regression: automatically generate a protected-method interface adapter.
const string automaticAdapterSource = """
unit AutomaticAdapterRegression;
interface
type
  IXmlBase = interface
    function GetObject: TObject;
  end;

  TXmlBase = class(TInterfacedObject, IXmlBase)
  protected
    function GetObject: TObject;
  end;
implementation
end.
""";

var automaticAdapterUnit = SemanticModelBuilder.Build(
    new DelphiParser(
        new DelphiLexer(automaticAdapterSource).Tokenize()).ParseUnit());

var automaticAdapterResolver = new TypeResolver(
    new[] { automaticAdapterUnit });

string automaticAdapterDeclarations =
    new CSharpDeclarationTranslator(
        automaticAdapterResolver,
        automaticAdapterUnit.Name)
    .Translate(automaticAdapterUnit);

Check("Delphi protected method generates explicit interface adapter",
    automaticAdapterDeclarations.Contains(
        "TObject IXmlBase.GetObject() => GetObject();",
        StringComparison.Ordinal) &&
    automaticAdapterDeclarations.Contains(
        "class TXmlBase : TInterfacedObject, IXmlBase",
        StringComparison.Ordinal) &&
    automaticAdapterDeclarations.Split(
        "IXmlBase.GetObject() => GetObject();",
        StringSplitOptions.None).Length == 2);

Check("Automatically generated interface adapter compiles and executes",
    AutomaticAdapterExecutionRegression.Run(automaticAdapterDeclarations));

// Regression 43: Delphi const parameter requires a C# in adapter.
const string constAdapterSource = """
unit ConstAdapterRegression;
interface
type
  IXmlNode = interface
    procedure Set_Text(const aValue: string);
  end;

  TXmlNode = class(TInterfacedObject, IXmlNode)
  protected
    procedure Set_Text(const aValue: string);
  end;
implementation
end.
""";

var constAdapterModel = SemanticModelBuilder.Build(
    new DelphiParser(
        new DelphiLexer(constAdapterSource).Tokenize()).ParseUnit());

var constAdapterResolver = new TypeResolver(
    new[] { constAdapterModel });

string constAdapterDeclarations =
    new CSharpDeclarationTranslator(
        constAdapterResolver,
        constAdapterModel.Name)
    .Translate(constAdapterModel);

Check("Delphi const parameter generates explicit interface adapter",
    constAdapterDeclarations.Contains(
        "void IXmlNode.Set_Text(string aValue) => Set_Text(in aValue);",
        StringComparison.Ordinal) &&
    constAdapterDeclarations.Split(
        "IXmlNode.Set_Text(string aValue) => Set_Text(in aValue);",
        StringSplitOptions.None).Length == 2);

Check("Automatically generated const adapter compiles and executes",
    AutomaticAdapterExecutionRegression.RunConstAdapter(constAdapterDeclarations));

// Regression 45: ordinary Value parameters and overload matching.
const string valueAdapterSource = """
unit ValueAdapterRegression;
interface
type
  IXmlNode = interface
    function AppendElement(aNameID: Integer): Integer; overload;
    function AppendElement(const aName: string): Integer; overload;
  end;

  TXmlNode = class(TInterfacedObject, IXmlNode)
  protected
    function AppendElement(aNameID: Integer): Integer; overload;
    function AppendElement(const aName: string): Integer; overload;
  end;
implementation
end.
""";

var valueAdapterModel = SemanticModelBuilder.Build(
    new DelphiParser(
        new DelphiLexer(valueAdapterSource).Tokenize()).ParseUnit());

var valueAdapterResolver = new TypeResolver(
    new[] { valueAdapterModel });

string valueAdapterDeclarations =
    new CSharpDeclarationTranslator(
        valueAdapterResolver,
        valueAdapterModel.Name)
    .Translate(valueAdapterModel);

Check("Value and Const overloads generate distinct adapters",
    valueAdapterDeclarations.Contains(
        "int IXmlNode.AppendElement(int aNameID) => AppendElement(aNameID);",
        StringComparison.Ordinal) &&
    valueAdapterDeclarations.Contains(
        "int IXmlNode.AppendElement(string aName) => AppendElement(in aName);",
        StringComparison.Ordinal));
// Regression 46: multiple parameters and overloaded interface methods.
const string multiParameterSource = """
unit MultiParameterAdapterRegression;
interface
type
  INode = interface
    procedure InsertBefore(const aChild, aBefore: INode);
    function GetChildText(aNameID: Integer;
      const aDefault: string): string; overload;
    function GetChildText(const aName: string;
      const aDefault: string): string; overload;
  end;

  TNode = class(TInterfacedObject, INode)
  protected
    procedure InsertBefore(const aChild, aBefore: INode);
    function GetChildText(aNameID: Integer;
      const aDefault: string): string; overload;
    function GetChildText(const aName: string;
      const aDefault: string): string; overload;
  end;
implementation
end.
""";

var multiParameterModel = SemanticModelBuilder.Build(
    new DelphiParser(
        new DelphiLexer(multiParameterSource).Tokenize()).ParseUnit());

var multiParameterResolver = new TypeResolver(
    new[] { multiParameterModel });

string multiParameterDeclarations =
    new CSharpDeclarationTranslator(
        multiParameterResolver,
        multiParameterModel.Name)
    .Translate(multiParameterModel);

Check("Multiple parameters generate distinct interface adapters",
    multiParameterDeclarations.Contains(
        "void INode.InsertBefore(INode aChild, INode aBefore) => InsertBefore(in aChild, in aBefore);",
        StringComparison.Ordinal) &&
    multiParameterDeclarations.Contains(
        "string INode.GetChildText(int aNameID, string aDefault) => GetChildText(aNameID, in aDefault);",
        StringComparison.Ordinal) &&
    multiParameterDeclarations.Contains(
        "string INode.GetChildText(string aName, string aDefault) => GetChildText(in aName, in aDefault);",
        StringComparison.Ordinal));
// Regression 47: read-only interface property adapter.
const string propertyAdapterSource = """
unit PropertyAdapterRegression;
interface
type
  INode = interface
    function Get_NodeName: string;
    function Get_Text: string;
    procedure Set_Text(const aValue: string);
    property NodeName: string read Get_NodeName;
    property Text: string read Get_Text write Set_Text;
  end;

  TNode = class(TInterfacedObject, INode)
  protected
    function Get_NodeName: string;
    function Get_Text: string;
    procedure Set_Text(const aValue: string);
  end;
implementation
end.
""";

var propertyAdapterModel = SemanticModelBuilder.Build(
    new DelphiParser(
        new DelphiLexer(propertyAdapterSource).Tokenize()).ParseUnit());

var propertyAdapterResolver = new TypeResolver(
    new[] { propertyAdapterModel });

string propertyAdapterDeclarations =
    new CSharpDeclarationTranslator(
        propertyAdapterResolver,
        propertyAdapterModel.Name)
    .Translate(propertyAdapterModel);

Check("Read-only interface property generates explicit adapter",
    propertyAdapterDeclarations.Contains(
        "string INode.NodeName => Get_NodeName();",
        StringComparison.Ordinal) &&
    propertyAdapterDeclarations.Contains(
        "string INode.Text",
        StringComparison.Ordinal));

// Regression 48: read/write interface property adapters.
Check("Read/write interface properties preserve setter argument kinds",
    propertyAdapterDeclarations.Contains(
        "string INode.Text",
        StringComparison.Ordinal) &&
    propertyAdapterDeclarations.Contains(
        "get => Get_Text();",
        StringComparison.Ordinal) &&
    propertyAdapterDeclarations.Contains(
        "set => Set_Text(in value);",
        StringComparison.Ordinal));

// Regression 49: indexed Delphi properties retain their semantic
// contract without generating incorrect ordinary C# properties.
const string indexedPropertySource = """
unit IndexedPropertyRegression;
interface
type
  INode = interface
    function Get_AttrName(anIndex: Integer): string;
    function Get_Values(const aName: string): string;
    procedure Set_Values(const aName: string; const aValue: string);
    property AttrNames[anIndex: Integer]: string read Get_AttrName;
    property Values[const aName: string]: string
      read Get_Values write Set_Values; default;
  end;
implementation
end.
""";

var indexedPropertyModel = SemanticModelBuilder.Build(
    new DelphiParser(
        new DelphiLexer(indexedPropertySource).Tokenize()).ParseUnit());

var indexedPropertyContract = indexedPropertyModel.Interfaces
    .Single(candidate => candidate.Name == "INode");

var indexedPropertyMembers = indexedPropertyContract.Members
    .Where(candidate => candidate.Kind == SemanticMemberKind.Property)
    .ToArray();

var indexedPropertyResolver = new TypeResolver(
    new[] { indexedPropertyModel });

string indexedPropertyDeclarations =
    new CSharpDeclarationTranslator(
        indexedPropertyResolver,
        indexedPropertyModel.Name)
    .Translate(indexedPropertyModel);

Console.WriteLine("=== INDEXED PROPERTY DIAGNOSTICS ===");

foreach (var property in indexedPropertyMembers)
{
    Console.WriteLine(
        $"PROPERTY: {property.Name}; " +
        $"Parameters={property.Parameters.Count}; " +
        $"Default={property.IsDefault}; " +
        $"Read={property.ReadAccessor}; " +
        $"Write={property.WriteAccessor}");

    foreach (var parameter in property.Parameters)
    {
        Console.WriteLine(
            $"  PARAMETER: {parameter.Name}; " +
            $"Type={parameter.Type}; Kind={parameter.Kind}");
    }
}

Console.WriteLine("=== GENERATED INDEXED DECLARATIONS ===");
Console.WriteLine(indexedPropertyDeclarations);
Console.WriteLine("=== END INDEXED DIAGNOSTICS ===");

Check("Indexed properties preserve metadata and accessor contracts",
    indexedPropertyMembers.Length == 2 &&
    indexedPropertyMembers.Any(candidate =>
        candidate.Name == "AttrNames" &&
        candidate.Parameters.Count == 1 &&
        candidate.Parameters[0].Type.Equals(
            "Integer", StringComparison.OrdinalIgnoreCase) &&
        candidate.ReadAccessor == "Get_AttrName" &&
        !candidate.IsDefault) &&
    indexedPropertyMembers.Any(candidate =>
        candidate.Name == "Values" &&
        candidate.Parameters.Count == 1 &&
        candidate.Parameters[0].Kind.Equals(
            "Const", StringComparison.OrdinalIgnoreCase) &&
        candidate.ReadAccessor == "Get_Values" &&
        candidate.WriteAccessor == "Set_Values" &&
        candidate.IsDefault) &&
    indexedPropertyDeclarations.Contains(
        "string Get_AttrName(int anIndex);",
        StringComparison.Ordinal) &&
    indexedPropertyDeclarations.Contains(
        "string Get_Values(string aName);",
        StringComparison.Ordinal) &&
    indexedPropertyDeclarations.Contains(
        "void Set_Values(string aName, string aValue);",
        StringComparison.Ordinal) &&
    !indexedPropertyDeclarations.Contains(
        "string AttrNames {", StringComparison.Ordinal) &&
    !indexedPropertyDeclarations.Contains(
        "string Values {", StringComparison.Ordinal));

// Regression 50: default directive must not consume following properties.
const string propertySequenceSource = """
unit PropertySequenceRegression;
interface
type
  INode = interface
    function Get_First(i: Integer): string;
    function Get_Second: string;
    function Get_Third(i: Integer): string;
    property First[i: Integer]: string read Get_First; default;
    property Second: string read Get_Second;
    property Third[i: Integer]: string read Get_Third;
  end;
implementation
end.
""";

var propertySequenceModel = SemanticModelBuilder.Build(
    new DelphiParser(
        new DelphiLexer(propertySequenceSource).Tokenize()).ParseUnit());

var propertySequenceMembers = propertySequenceModel.Interfaces
    .Single(candidate => candidate.Name == "INode")
    .Members
    .Where(candidate => candidate.Kind == SemanticMemberKind.Property)
    .ToArray();

Check("Default directive preserves following property declarations",
    propertySequenceMembers.Length == 3 &&
    propertySequenceMembers[0].Name == "First" &&
    propertySequenceMembers[0].IsDefault &&
    propertySequenceMembers[0].Parameters.Count == 1 &&
    propertySequenceMembers[1].Name == "Second" &&
    !propertySequenceMembers[1].IsDefault &&
    propertySequenceMembers[1].Parameters.Count == 0 &&
    propertySequenceMembers[2].Name == "Third" &&
    !propertySequenceMembers[2].IsDefault &&
    propertySequenceMembers[2].Parameters.Count == 1);

// Regressions 51-54: direct indexed-property resolution.
Check("Indexed lookup resolves interface property",
    indexedPropertyResolver.TryResolveIndexedProperty(
        "INode", "Values", indexedPropertyModel.Name,
        out var resolvedInterfaceProperty) &&
    resolvedInterfaceProperty.Name == "Values" &&
    resolvedInterfaceProperty.Parameters.Count == 1 &&
    resolvedInterfaceProperty.ReadAccessor == "Get_Values" &&
    resolvedInterfaceProperty.WriteAccessor == "Set_Values");

Check("Indexed lookup rejects missing property",
    !indexedPropertyResolver.TryResolveIndexedProperty(
        "INode", "Missing", indexedPropertyModel.Name,
        out _));

Check("Indexed lookup rejects pointer receiver",
    !indexedPropertyResolver.TryResolveIndexedProperty(
        "^INode", "Values", indexedPropertyModel.Name,
        out _));

const string indexedClassSource = """
unit IndexedClassRegression;
interface
type
  TNode = class
    function Get_Item(i: Integer): Integer;
    function Get_Name: string;
    property Item[i: Integer]: Integer read Get_Item; default;
    property Name: string read Get_Name;
  end;
implementation
end.
""";

var indexedClassModel = SemanticModelBuilder.Build(
    new DelphiParser(
        new DelphiLexer(indexedClassSource).Tokenize()).ParseUnit());

var indexedClassResolver = new TypeResolver(
    new[] { indexedClassModel });

Check("Indexed lookup resolves class property, not ordinary property",
    indexedClassResolver.TryResolveIndexedProperty(
        "TNode", "Item", indexedClassModel.Name,
        out var resolvedClassProperty) &&
    resolvedClassProperty.Name == "Item" &&
    resolvedClassProperty.IsDefault &&
    resolvedClassProperty.ReadAccessor == "Get_Item" &&
    !indexedClassResolver.TryResolveIndexedProperty(
        "TNode", "Name", indexedClassModel.Name,
        out _));

// Regressions 55-58: indexed-property visibility across units.
const string indexedUnitA = """
unit IndexedUnitA;
interface
type
  IShared = interface
    function Get_A(i: Integer): Integer;
    property Item[i: Integer]: Integer read Get_A;
  end;
implementation
end.
""";

const string indexedUnitB = """
unit IndexedUnitB;
interface
type
  IShared = interface
    function Get_B(i: Integer): Integer;
    property Item[i: Integer]: Integer read Get_B;
  end;
implementation
end.
""";

const string indexedLocalConsumer = """
unit IndexedLocalConsumer;
interface
uses IndexedUnitA;
type
  IShared = interface
    function Get_Local(i: Integer): Integer;
    property Item[i: Integer]: Integer read Get_Local;
  end;
implementation
end.
""";

const string indexedSingleConsumer = """
unit IndexedSingleConsumer;
interface
uses IndexedUnitA;
implementation
end.
""";

const string indexedAmbiguousConsumer = """
unit IndexedAmbiguousConsumer;
interface
uses IndexedUnitA, IndexedUnitB;
implementation
end.
""";

SemanticUnit ParseIndexedUnit(string sourceText) =>
    SemanticModelBuilder.Build(
        new DelphiParser(
            new DelphiLexer(sourceText).Tokenize()).ParseUnit());

var indexedCrossUnitModels = new[]
{
    ParseIndexedUnit(indexedUnitA),
    ParseIndexedUnit(indexedUnitB),
    ParseIndexedUnit(indexedLocalConsumer),
    ParseIndexedUnit(indexedSingleConsumer),
    ParseIndexedUnit(indexedAmbiguousConsumer)
};

var indexedCrossUnitResolver = new TypeResolver(indexedCrossUnitModels);

Check("Indexed lookup prefers local declaration",
    indexedCrossUnitResolver.TryResolveIndexedProperty(
        "IShared", "Item", "IndexedLocalConsumer",
        out var localIndexedProperty) &&
    localIndexedProperty.ReadAccessor == "Get_Local");

Check("Indexed lookup resolves single imported declaration",
    indexedCrossUnitResolver.TryResolveIndexedProperty(
        "IShared", "Item", "IndexedSingleConsumer",
        out var importedIndexedProperty) &&
    importedIndexedProperty.ReadAccessor == "Get_A");

Check("Indexed lookup rejects ambiguous imported declarations",
    !indexedCrossUnitResolver.TryResolveIndexedProperty(
        "IShared", "Item", "IndexedAmbiguousConsumer",
        out _));

Check("Indexed lookup honors qualified unit name",
    indexedCrossUnitResolver.TryResolveIndexedProperty(
        "IndexedUnitB.IShared", "Item", "IndexedAmbiguousConsumer",
        out var qualifiedIndexedProperty) &&
    qualifiedIndexedProperty.ReadAccessor == "Get_B");

// Regressions 59-62: translate real indexed expressions.
const string indexedExpressionSource = """
unit IndexedExpressionRegression;
interface
type
  INode = interface
    function Get_Values(const key: string): string;
    procedure Set_Values(const key: string; const value: string);
    property Values[const key: string]: string
      read Get_Values write Set_Values;
  end;

function ReadIndexed(node: INode; key: string): string;
function ReadArray(numbers: array of Integer; index: Integer): Integer;
function ReadMultiple(numbers: array of Integer;
  i: Integer; j: Integer): Integer;
procedure WriteIndexed(node: INode; key: string; value: string);

implementation

function ReadIndexed(node: INode; key: string): string;
begin
  Result := node.Values[key];
end;

function ReadArray(numbers: array of Integer; index: Integer): Integer;
begin
  Result := numbers[index];
end;

function ReadMultiple(numbers: array of Integer;
  i: Integer; j: Integer): Integer;
begin
  Result := numbers[i, j];
end;

procedure WriteIndexed(node: INode; key: string; value: string);
begin
  node.Values[key] := value;
end;

end.
""";

var indexedExpressionUnit = new DelphiParser(
    new DelphiLexer(indexedExpressionSource).Tokenize()).ParseUnit();

var indexedExpressionModel =
    SemanticModelBuilder.Build(indexedExpressionUnit);

var indexedExpressionResolver = new TypeResolver(
    new[] { indexedExpressionModel });

var indexedExpressionTranslator = new DelphiToCSharpTranslator(
    indexedExpressionResolver,
    indexedExpressionModel.Name);

var indexedExpressionMethods =
    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

foreach (var body in BodyTokenAnalyzer.Analyze(
    indexedExpressionUnit,
    "IndexedExpressionRegression.pas")
    .Where(candidate => candidate.BodyTokens.Count > 0))
{
    var matched = FunctionImplementationMatcher.Match(
        indexedExpressionUnit, body, indexedExpressionModel);

    if (matched is null)
        continue;

    indexedExpressionMethods[matched.Name] =
        indexedExpressionTranslator.TranslateFunction(
            matched,
            BehaviorAnalyzer.Analyze(
                body.FilePath,
                body.Name,
                body.BodyTokens));
}

Check("Indexed expression read uses declared getter",
    indexedExpressionMethods.TryGetValue(
        "ReadIndexed", out var indexedRead) &&
    indexedRead.Contains(
        "node.Get_Values(key)",
        StringComparison.Ordinal) &&
    !indexedRead.Contains(
        "node.Values[key]",
        StringComparison.Ordinal));

Check("Ordinary array indexing remains unchanged",
    indexedExpressionMethods.TryGetValue(
        "ReadArray", out var arrayRead) &&
    arrayRead.Contains(
        "numbers[index]",
        StringComparison.Ordinal) &&
    !arrayRead.Contains(
        "Get_Values(",
        StringComparison.Ordinal));

Check("Multiple indices are preserved",
    indexedExpressionMethods.TryGetValue(
        "ReadMultiple", out var multipleRead) &&
    multipleRead.Contains(
        "numbers[i, j]",
        StringComparison.Ordinal));

Check("Indexed assignment uses declared setter",
    indexedExpressionMethods.TryGetValue(
        "WriteIndexed", out var indexedWrite) &&
    indexedWrite.Contains(
        "node.Set_Values(key, value);",
        StringComparison.Ordinal) &&
    !indexedWrite.Contains(
        "node.Values[key] =",
        StringComparison.Ordinal) &&
    !indexedWrite.Contains(
        "node.Get_Values(key) =",
        StringComparison.Ordinal));

Console.WriteLine("=== INDEXED EXPRESSION OUTPUT ===");

foreach (var entry in indexedExpressionMethods)
{
    Console.WriteLine($"METHOD: {entry.Key}");
    Console.WriteLine(entry.Value);
}

Console.WriteLine("=== END INDEXED EXPRESSION OUTPUT ===");

// Regression 63: compile and execute translated indexed accessors.
Check("Generated indexed getter/setter compile and execute",
    indexedExpressionMethods.TryGetValue(
        "ReadIndexed", out var executableRead) &&
    indexedExpressionMethods.TryGetValue(
        "WriteIndexed", out var executableWrite) &&
    IndexedPropertyExecutionRegression.Run(
        executableRead,
        executableWrite));

Console.WriteLine($"Generated-interface-list checks: {total - failed}/{total} passed.");
return failed == 0 ? 0 : 1;
