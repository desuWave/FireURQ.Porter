using System.Text;
using FireURQ.Porter.AST;
using FireURQ.Porter.Analysis;

namespace FireURQ.Porter.Translation;

public sealed class ProjectTranslator
{
    private readonly CSharpDeclarationTranslator _declarationTranslator;
    private readonly DelphiToCSharpTranslator _functionTranslator;
    private readonly TypeResolver? _resolver;
    private readonly IReadOnlyList<SemanticUnit> _units;
    private readonly bool _usesStreamRuntime;
    private readonly bool _usesJclStringListRuntime;
    private readonly bool _usesStringListRuntime;

    public ProjectTranslator() : this(null, null)
    {
    }

    private ProjectTranslator(
        TypeResolver? resolver,
        string? unitName,
        IReadOnlyList<SemanticUnit>? units = null)
    {
        _resolver = resolver;
        _units = units ?? Array.Empty<SemanticUnit>();
        _usesStreamRuntime = RequiresStreamRuntime(_units, resolver);
        _usesJclStringListRuntime = RequiresJclStringListRuntime(_units, resolver);
        _usesStringListRuntime = resolver is not null &&
            RequiresListRuntime(_units, resolver,
                "TStrings", "TStringList", "TStringListSortCompare");
        _declarationTranslator = new CSharpDeclarationTranslator(resolver, unitName, _units);
        _functionTranslator = new DelphiToCSharpTranslator(resolver, unitName);
    }

    public IReadOnlyList<TranslationResult> Translate(
        IReadOnlyList<ProjectTranslationInput> inputs,
        string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        var results = new List<TranslationResult>();

        string runtimeDirectory = Path.Combine(outputDirectory, "__runtime");
        Directory.CreateDirectory(runtimeDirectory);
        File.WriteAllText(
            Path.Combine(runtimeDirectory, "TObject.cs"),
            CSharpObjectRuntime.Source,
            Encoding.UTF8);
        File.WriteAllText(
            Path.Combine(runtimeDirectory, "Variant.cs"),
            CSharpVariantRuntime.Source,
            Encoding.UTF8);

        var resolver = new TypeResolver(
            inputs.Select(input => input.SemanticModel));

        if (RequiresStreamRuntime(
            inputs.Select(input => input.SemanticModel), resolver))
        {
            File.WriteAllText(
                Path.Combine(runtimeDirectory, "Streams.cs"),
                CSharpStreamRuntime.Source,
                Encoding.UTF8);
        }

        if (RequiresJclStringListRuntime(
            inputs.Select(input => input.SemanticModel), resolver))
        {
            File.WriteAllText(
                Path.Combine(runtimeDirectory, "JclStringLists.cs"),
                CSharpJclStringListRuntime.Source,
                Encoding.UTF8);
        }

        if (RequiresListRuntime(
            inputs.Select(input => input.SemanticModel), resolver,
            "TList", "TObjectList"))
        {
            File.WriteAllText(
                Path.Combine(runtimeDirectory, "Lists.cs"),
                CSharpListRuntime.Source,
                Encoding.UTF8);
        }

        if (RequiresListRuntime(
            inputs.Select(input => input.SemanticModel), resolver,
            "IInterfaceList", "TInterfaceList"))
        {
            File.WriteAllText(
                Path.Combine(runtimeDirectory, "InterfaceLists.cs"),
                CSharpInterfaceListRuntime.Source,
                Encoding.UTF8);
        }

        if (RequiresListRuntime(
            inputs.Select(input => input.SemanticModel), resolver,
            "AnsiString"))
        {
            File.WriteAllText(
                Path.Combine(runtimeDirectory, "AnsiStrings.cs"),
                CSharpAnsiStringRuntime.Source,
                Encoding.UTF8);

            if (RequiresStreamRuntime(
                inputs.Select(input => input.SemanticModel), resolver))
            {
                File.WriteAllText(
                    Path.Combine(runtimeDirectory, "AnsiStringStreams.cs"),
                    CSharpAnsiStringStreamRuntime.Source,
                    Encoding.UTF8);
            }
        }

        if (RequiresJclStringListRuntime(
                inputs.Select(input => input.SemanticModel), resolver) ||
            RequiresListRuntime(
                inputs.Select(input => input.SemanticModel), resolver,
                "TStrings", "TStringList", "TStringListSortCompare"))
        {
            File.WriteAllText(
                Path.Combine(runtimeDirectory, "StringListCommon.cs"),
                CSharpStringListCommonRuntime.Source,
                Encoding.UTF8);
        }

        if (RequiresListRuntime(
            inputs.Select(input => input.SemanticModel), resolver,
            "TStrings", "TStringList", "TStringListSortCompare"))
        {
            File.WriteAllText(
                Path.Combine(runtimeDirectory, "StringLists.cs"),
                CSharpStringListRuntime.Source,
                Encoding.UTF8);
        }

        foreach (ProjectTranslationInput input in inputs)
        {
            var unitTranslator = new ProjectTranslator(
                resolver, input.SemanticModel.Name,
                inputs.Select(item => item.SemanticModel).ToList());

            results.Add(unitTranslator.TranslateUnit(input, outputDirectory));
        }

        return results;
    }

    private static bool RequiresListRuntime(
        IEnumerable<SemanticUnit> units,
        TypeResolver resolver,
        params string[] runtimeTypeNames)
    {
        foreach (SemanticUnit unit in units)
        {
            var types = new List<string>();
            types.AddRange(unit.Types.Select(type => type.Definition));
            types.AddRange(unit.Variables.Select(variable => variable.Type));
            foreach (SemanticFunction function in unit.Functions)
            {
                types.Add(function.ReturnType);
                types.AddRange(function.Parameters.Select(parameter => parameter.Type));
                types.AddRange(function.LocalVariables.Select(variable => variable.Type));
            }
            foreach (SemanticClass owner in unit.Classes)
            {
                types.Add(owner.BaseType ?? string.Empty);
                foreach (SemanticMember member in owner.Members)
                {
                    types.Add(member.Type);
                    types.AddRange(member.Parameters.Select(parameter => parameter.Type));
                }
            }
            foreach (SemanticInterface owner in unit.Interfaces)
            {
                foreach (SemanticMember member in owner.Members)
                {
                    types.Add(member.Type);
                    types.AddRange(member.Parameters.Select(parameter => parameter.Type));
                }
            }

            foreach (string name in runtimeTypeNames)
            {
                TypeResolution resolution = resolver.Resolve(name, unit.Name);
                if (!resolution.IsResolved || resolution.Chain.Count > 0)
                    continue;

                if (types.Any(type => !string.IsNullOrWhiteSpace(type) &&
                    System.Text.RegularExpressions.Regex.IsMatch(
                        type,
                        @"(?<![\w.])" + name + @"(?!\w)",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase |
                        System.Text.RegularExpressions.RegexOptions.CultureInvariant)))
                    return true;
            }
        }
        return false;
    }
    private static bool RequiresJclStringListRuntime(
        IEnumerable<SemanticUnit> units,
        TypeResolver? resolver)
    {
        if (resolver is null)
            return false;

        foreach (SemanticUnit unit in units)
        {
            // A project declaration with this name takes precedence.
            TypeResolution resolution = resolver.Resolve("IJclStringList", unit.Name);
            if (!resolution.IsResolved || resolution.Chain.Count > 0)
                continue;

            var types = new List<string>();
            types.AddRange(unit.Types.Select(type => type.Definition));
            types.AddRange(unit.Variables.Select(variable => variable.Type));

            foreach (SemanticFunction function in unit.Functions)
            {
                types.Add(function.ReturnType);
                types.AddRange(function.Parameters.Select(parameter => parameter.Type));
                types.AddRange(function.LocalVariables.Select(variable => variable.Type));
            }

            foreach (SemanticClass owner in unit.Classes)
            {
                types.Add(owner.BaseType ?? string.Empty);
                foreach (SemanticMember member in owner.Members)
                {
                    types.Add(member.Type);
                    types.AddRange(member.Parameters.Select(parameter => parameter.Type));
                }
            }

            foreach (SemanticInterface owner in unit.Interfaces)
            {
                foreach (SemanticMember member in owner.Members)
                {
                    types.Add(member.Type);
                    types.AddRange(member.Parameters.Select(parameter => parameter.Type));
                }
            }

            if (types.Any(type => !string.IsNullOrWhiteSpace(type) &&
                System.Text.RegularExpressions.Regex.IsMatch(
                    type,
                    @"(?<![\w.])IJclStringList(?!\w)",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase |
                    System.Text.RegularExpressions.RegexOptions.CultureInvariant)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool RequiresStreamRuntime(
        IEnumerable<SemanticUnit> units,
        TypeResolver? resolver)
    {
        if (resolver is null)
            return false;

        foreach (SemanticUnit unit in units)
        {
            var types = new List<string>();
            types.AddRange(unit.Types.Select(type => type.Definition));
            types.AddRange(unit.Variables.Select(variable => variable.Type));
            foreach (SemanticFunction function in unit.Functions)
            {
                types.Add(function.ReturnType);
                types.AddRange(function.Parameters.Select(parameter => parameter.Type));
                types.AddRange(function.LocalVariables.Select(variable => variable.Type));
            }

            foreach (SemanticClass owner in unit.Classes)
            {
                types.Add(owner.BaseType ?? string.Empty);
                foreach (SemanticMember member in owner.Members)
                {
                    types.Add(member.Type);
                    types.AddRange(member.Parameters.Select(parameter => parameter.Type));
                }
            }

            foreach (SemanticInterface owner in unit.Interfaces)
            {
                foreach (SemanticMember member in owner.Members)
                {
                    types.Add(member.Type);
                    types.AddRange(member.Parameters.Select(parameter => parameter.Type));
                }
            }

            if (types.Where(type => !string.IsNullOrWhiteSpace(type)).Any(type =>
                resolver.IsInheritedTStreamOperation(type, "Read", unit.Name)))
                return true;
        }

        return false;
    }

    private TranslationResult TranslateUnit(
        ProjectTranslationInput input,
        string outputDirectory)
    {
        string rootName = new DirectoryInfo(input.RootPath).Name;
        string relativePath = Path.GetRelativePath(input.RootPath, input.FilePath);
        string outputRelativePath = Path.ChangeExtension(relativePath, ".cs");
        string outputFile = Path.Combine(outputDirectory, rootName, outputRelativePath);

        try
        {
            string? outputParent = Path.GetDirectoryName(outputFile);
            if (!string.IsNullOrWhiteSpace(outputParent))
                Directory.CreateDirectory(outputParent);

            var builder = new StringBuilder();
            builder.AppendLine("// <auto-generated />");
            builder.AppendLine($"// Source: {input.FilePath}");
            builder.AppendLine();
            builder.AppendLine("using System;");
            builder.AppendLine("using System.Collections.Generic;");
            builder.AppendLine("using System.Linq;");
            builder.AppendLine("using System.Runtime.InteropServices;");
            builder.AppendLine("using FireURQ.Runtime;");
            builder.AppendLine("using static global::FireURQ.Runtime.DelphiVariants;");
            if (_usesJclStringListRuntime)
                builder.AppendLine("using static global::FireURQ.Runtime.DelphiJclStringLists;");
            if (_usesJclStringListRuntime || _usesStringListRuntime)
                builder.AppendLine("using static global::FireURQ.Runtime.TDuplicates;");
            if (_usesStreamRuntime)
                builder.AppendLine("using static global::FireURQ.Runtime.DelphiStreamConstants;");
            builder.AppendLine();

            string declarations = _declarationTranslator.Translate(input.SemanticModel);
            if (!string.IsNullOrWhiteSpace(declarations))
                builder.AppendLine(declarations);

            string moduleMembers = _declarationTranslator.TranslateModuleMembers(input.SemanticModel);
            bool hasTopLevelImplementation = input.Bodies.Any(body =>
                body.Body.OwnerKind == BodyTokenAnalyzer.BodyOwnerKind.TopLevel &&
                body.Body.BodyTokens.Count > 0);

            if (!string.IsNullOrWhiteSpace(moduleMembers) || hasTopLevelImplementation)
            {
                builder.AppendLine($"public static class {input.SemanticModel.Name}Module");
                builder.AppendLine("{");

                if (!string.IsNullOrWhiteSpace(moduleMembers))
                {
                    foreach (string line in moduleMembers.Split('\n'))
                        builder.AppendLine("    " + line.TrimEnd('\r'));
                    builder.AppendLine();
                }

                int functionsTranslated = AppendTopLevelImplementations(
                    builder,
                    input);

                builder.AppendLine("}");
                builder.AppendLine();

                int classFunctionsTranslated = AppendClassImplementations(
                    builder,
                    input);

                string outputText = builder.ToString();
                File.WriteAllText(outputFile, outputText, Encoding.UTF8);

                return new TranslationResult(
                    input.FilePath,
                    outputFile,
                    true,
                    functionsTranslated + classFunctionsTranslated,
                    CountTodos(outputText),
                    null);
            }

            int onlyClassFunctionsTranslated = AppendClassImplementations(builder, input);
            string finalOutput = builder.ToString();
            File.WriteAllText(outputFile, finalOutput, Encoding.UTF8);

            return new TranslationResult(
                input.FilePath,
                outputFile,
                true,
                onlyClassFunctionsTranslated,
                CountTodos(finalOutput),
                null);
        }
        catch (Exception ex)
        {
            return new TranslationResult(input.FilePath, outputFile, false, 0, 0, ex.ToString());
        }
    }

    private int AppendTopLevelImplementations(
        StringBuilder builder,
        ProjectTranslationInput input)
    {
        int translatedCount = 0;

        foreach (var body in input.Bodies)
        {
            if (body.Body.OwnerKind != BodyTokenAnalyzer.BodyOwnerKind.TopLevel ||
                body.Body.BodyTokens.Count == 0)
                continue;

            SemanticFunction? function = MatchFunction(input.Unit, body.Body, input.SemanticModel);
            if (function is null)
            {
                builder.AppendLine($"    // TODO: Unmatched Delphi implementation: {body.Body.Name}");
                builder.AppendLine();
                continue;
            }

            string translated = _functionTranslator.TranslateFunction(function, body.Behavior);
            builder.Append(IndentAndModifySignature(translated, "public static ", 1));
            builder.AppendLine();
            translatedCount++;
        }

        return translatedCount;
    }

    private int AppendClassImplementations(
        StringBuilder builder,
        ProjectTranslationInput input)
    {
        int translatedCount = 0;

        var classBodies = input.Bodies
            .Where(body =>
                body.Body.OwnerKind == BodyTokenAnalyzer.BodyOwnerKind.Class &&
                body.Body.BodyTokens.Count > 0)
            .ToList();

        foreach (var group in classBodies.GroupBy(body => GetOwnerName(body.Body.Name)))
        {
            builder.AppendLine($"public partial class {group.Key}");
            builder.AppendLine("{");

            foreach (var body in group)
            {
                SemanticFunction? function = MatchFunction(input.Unit, body.Body, input.SemanticModel);
                if (function is null)
                {
                    builder.AppendLine($"    // TODO: Unmatched Delphi method implementation: {body.Body.Name}");
                    builder.AppendLine();
                    continue;
                }

                function = NormalizeOverrideVisibility(
                    function, group.Key, input.SemanticModel);
                string translated = _functionTranslator.TranslateFunction(
                    function, body.Behavior, manageDestructorLifetime: true);
                builder.Append(IndentAndModifySignature(
                    translated, GetInstanceImplementationPrefix(function), 1));
                builder.AppendLine();
                translatedCount++;
            }

            builder.AppendLine("}");
            builder.AppendLine();
        }

        return translatedCount;
    }

    private SemanticFunction NormalizeOverrideVisibility(
        SemanticFunction function,
        string ownerName,
        SemanticUnit unit)
    {
        if (_resolver is null || function.IsClassMethod ||
            !function.Modifiers.Contains(
                "override", StringComparer.OrdinalIgnoreCase))
            return function;

        SemanticClass? owner = unit.Classes.FirstOrDefault(item =>
            item.Name.Equals(ownerName, StringComparison.OrdinalIgnoreCase));
        string? baseName = owner?.BaseType;
        string context = unit.Name;
        string? visibility = null;
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        while (!string.IsNullOrWhiteSpace(baseName))
        {
            TypeResolution resolution = _resolver.Resolve(baseName, context);
            if (!resolution.IsResolved || resolution.IsPointer)
                break;

            string declaringUnit = resolution.DeclaringUnitName ?? context;
            SemanticUnit? baseUnit = _units.FirstOrDefault(item =>
                item.Name.Equals(declaringUnit, StringComparison.OrdinalIgnoreCase));
            if (baseUnit is null)
                break;

            string resolvedName = resolution.ResolvedName;
            int separator = resolvedName.LastIndexOf('.');
            if (separator >= 0)
                resolvedName = resolvedName[(separator + 1)..];

            SemanticClass? baseClass = baseUnit.Classes.FirstOrDefault(item =>
                item.Name.Equals(resolvedName, StringComparison.OrdinalIgnoreCase));
            if (baseClass is null ||
                !visited.Add(baseUnit.Name + "." + baseClass.Name))
                break;

            var candidates = baseClass.Members.Where(member =>
                member.Kind == SemanticMemberKind.Method &&
                !member.IsClassMethod &&
                member.Name.Equals(function.Name, StringComparison.OrdinalIgnoreCase) &&
                SameParameterSignature(
                    member.Parameters, baseUnit.Name,
                    function.Parameters, unit.Name)).ToList();

            if (candidates.Count > 1)
                break;

            if (candidates.Count == 1)
            {
                SemanticMember member = candidates[0];
                bool Has(string modifier) => member.Modifiers.Contains(
                    modifier, StringComparer.OrdinalIgnoreCase);

                if (!Has("virtual") && !Has("dynamic") &&
                    !Has("abstract") && !Has("override"))
                    break;

                visibility = member.Visibility;
                if (!Has("override"))
                    break;
            }

            baseName = baseClass.BaseType;
            context = baseUnit.Name;
        }

        return visibility is null
            ? function
            : function with { Visibility = visibility };
    }

    private bool SameParameterSignature(
        IReadOnlyList<SemanticParameter> left,
        string leftUnit,
        IReadOnlyList<SemanticParameter> right,
        string rightUnit)
    {
        if (left.Count != right.Count)
            return false;

        for (int index = 0; index < left.Count; index++)
        {
            if (!left[index].Kind.Equals(
                    right[index].Kind, StringComparison.OrdinalIgnoreCase) ||
                !CSharpTypeMapper.Map(left[index].Type, _resolver, leftUnit).Equals(
                    CSharpTypeMapper.Map(right[index].Type, _resolver, rightUnit),
                    StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    private static string GetInstanceImplementationPrefix(SemanticFunction function)
    {
        // Delphi class-method dispatch requires separate handling.
        if (function.IsClassMethod)
            return "public ";

        string visibility = function.Visibility?.ToLowerInvariant() switch
        {
            "private" or "strict private" => "private ",
            "protected" or "strict protected" => "protected ",
            _ => "public "
        };

        bool Has(string modifier) => function.Modifiers.Contains(
            modifier, StringComparer.OrdinalIgnoreCase);

        if (visibility == "private " &&
            (Has("virtual") || Has("dynamic") ||
             Has("override") || Has("abstract")))
            visibility = "protected ";

        if (Has("override"))
            return visibility + "override ";

        if (Has("virtual") || Has("dynamic"))
            return visibility + "virtual ";

        if (Has("reintroduce"))
            return visibility + "new ";

        return visibility;
    }
    private static string IndentAndModifySignature(
        string translated,
        string modifier,
        int indent)
    {
        string[] lines = translated.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        if (lines.Length > 0 && !string.IsNullOrWhiteSpace(lines[0]))
            lines[0] = modifier + lines[0];

        string prefix = new string(' ', indent * 4);
        return string.Join(
            Environment.NewLine,
            lines.Select(line => prefix + line));
    }

    private static string GetOwnerName(string qualifiedName)
    {
        int separator = qualifiedName.LastIndexOf('.');
        return separator > 0 ? qualifiedName[..separator] : qualifiedName;
    }

    private static SemanticFunction? MatchFunction(
        DelphiUnit unit,
        BodyTokenAnalyzer.BodyInfo body,
        SemanticUnit semanticUnit)
    {
        return FunctionImplementationMatcher.Match(unit, body, semanticUnit);
    }

    private static int CountTodos(string text)
    {
        return text.Split('\r', '\n').Count(line =>
            line.Contains("TODO:", StringComparison.OrdinalIgnoreCase));
    }
}

public sealed record ProjectTranslationInput(
    string RootPath,
    string FilePath,
    DelphiUnit Unit,
    SemanticUnit SemanticModel,
    IReadOnlyList<(
        BodyTokenAnalyzer.BodyInfo Body,
        BehaviorAnalyzer.BehaviorInfo Behavior)> Bodies
);
