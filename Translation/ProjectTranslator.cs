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

        if (RequiresListRuntime(
            inputs.Select(input => input.SemanticModel), resolver,
            "TPoint", "TRect"))
        {
            File.WriteAllText(
                Path.Combine(runtimeDirectory, "Geometry.cs"),
                CSharpGeometryRuntime.Source,
                Encoding.UTF8);
        }

        if (RequiresListRuntime(
            inputs.Select(input => input.SemanticModel), resolver,
            "TD3DXImageInfo"))
        {
            File.WriteAllText(
                Path.Combine(runtimeDirectory, "D3DX8.cs"),
                CSharpD3DX8Runtime.Source,
                Encoding.UTF8);
        }

        if (inputs.Any(input => input.SemanticModel.Name.Equals(
                "d2dCore", StringComparison.OrdinalIgnoreCase)))
        {
            File.WriteAllText(
                Path.Combine(runtimeDirectory, "D2dLogFormat.cs"),
                CSharpD2dLogFormatRuntime.Source,
                Encoding.UTF8);
        }

        if (RequiresListRuntime(
            inputs.Select(input => input.SemanticModel), resolver,
            "D3DADAPTER_IDENTIFIER8"))
        {
            File.WriteAllText(
                Path.Combine(runtimeDirectory, "D3D8Adapter.cs"),
                CSharpD3D8AdapterRuntime.Source,
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

    private static bool UsesManagedStandaloneClass(string unitName, string className) =>
        (unitName.Equals("d2dGIF", StringComparison.OrdinalIgnoreCase) &&
         className.Equals("Td2dGIFSprite", StringComparison.OrdinalIgnoreCase)) ||
        (unitName.Equals("SimpleXML", StringComparison.OrdinalIgnoreCase) &&
         className.ToUpperInvariant() is
             "TBINXMLREADER" or "TSTMXMLREADER" or "TSTRXMLREADER" or
             "TBINXMLWRITER" or "TSTMXMLWRITER" or "TSTRXMLWRITER");

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
            if (input.SemanticModel.Name.Equals("f2FontGen", StringComparison.OrdinalIgnoreCase))
            {
                builder.AppendLine("using static global::FireURQ.Runtime.GdiPlusFontBitmaps;");
                builder.AppendLine("using IStream = global::System.Runtime.InteropServices.ComTypes.IStream;");
                builder.AppendLine("using TStatus = global::System.Int32;");
            }
            if (_usesJclStringListRuntime)
                builder.AppendLine("using static global::FireURQ.Runtime.DelphiJclStringLists;");
            if (_usesJclStringListRuntime || _usesStringListRuntime)
                builder.AppendLine("using static global::FireURQ.Runtime.TDuplicates;");
            if (_usesStreamRuntime)
                builder.AppendLine("using static global::FireURQ.Runtime.DelphiStreamConstants;");
            builder.AppendLine();

            var declarationModel = input.SemanticModel with
            {
                Classes = input.SemanticModel.Classes.Where(owner =>
                    !UsesManagedStandaloneClass(input.SemanticModel.Name, owner.Name)).ToArray()
            };
            string declarations = _declarationTranslator.Translate(declarationModel);
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

                string outputText = AdaptD2dRenderTargets(input.SemanticModel.Name, builder.ToString());
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
            string finalOutput = AdaptD2dRenderTargets(input.SemanticModel.Name, builder.ToString());
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


    private static string AdaptD2dRenderTargets(string unitName, string source)
    {
        // Render targets contain COM references and are used by identity in D2D.
        if (unitName.Equals("d2dTypes", StringComparison.OrdinalIgnoreCase))
        {
            return source
                .Replace("using Pd2dRenderTarget = global::System.IntPtr;", "")
                .Replace("public struct Td2dRenderTarget", "public sealed class Td2dRenderTarget");
        }

        if (!unitName.Equals("d2dCore", StringComparison.OrdinalIgnoreCase))
            return source;

        source = global::System.Text.RegularExpressions.Regex.Replace(
            source, @"\bIntPtr(?=\s+(?:aTarget|l_Target|f_CurTarget|Target_Create)\b)", "Td2dRenderTarget");
        source = global::System.Text.RegularExpressions.Regex.Replace(
            source, @"\b(aTarget|l_Target)\s*!=\s*(?:global::System\.)?IntPtr\.Zero\b", "$1 != null");

        return source
            .Replace("TList f_Targets;", "List<Td2dRenderTarget> f_Targets;")
            .Replace("f_Targets = TList.Create();", "f_Targets = new List<Td2dRenderTarget>();")
            .Replace("f_Targets.Free();", "ClearTargets();")
            .Replace("f_Targets.Pack();", "f_CurTarget = null;")
            .Replace("Pd2dRenderTarget(f_Targets.Items[I])", "f_Targets[I]")
            .Replace("Dispose(f_Targets[I]);", "ReleaseTargetResources(f_Targets[I]);")
            .Replace("Dispose(Pd2dRenderTarget(f_Targets.Items[l_Idx]));",
                "ReleaseTargetResources(f_Targets[l_Idx]);")
            .Replace("f_Targets.Delete(l_Idx);", "f_Targets.RemoveAt(l_Idx);")
            .Replace("if (aTarget != f_CurTarget)",
                "if (!ReferenceEquals(aTarget, f_CurTarget) || (aTarget != null && aTarget.Tex == null))")
            .Replace("aTarget.Tex.GetSurfaceLevel(0, out l_Surf);",
                "if (aTarget.Tex == null || aTarget.Tex.GetSurfaceLevel(0, out l_Surf) < 0) return false;")
            .Replace("f_D3DDevice.SetRenderState(D3DRS_ZENABLE, D3DZB_TRUE);",
                "global::FireURQ.Runtime.D3D8.SetRenderState(f_D3DDevice, global::FireURQ.Runtime.D3D8.D3DRS_ZENABLE, global::FireURQ.Runtime.D3D8.D3DZB_TRUE);")
            .Replace("f_D3DDevice.SetRenderState(D3DRS_ZENABLE, D3DZB_FALSE);",
                "global::FireURQ.Runtime.D3D8.SetRenderState(f_D3DDevice, global::FireURQ.Runtime.D3D8.D3DRS_ZENABLE, global::FireURQ.Runtime.D3D8.D3DZB_FALSE);")
            .Replace("TD3DMatrix l_Temp;", "")
            .Replace("D3DXMatrixScaling(f_matProj, 1.0, ( - 1.0), 1.0);", """
f_matProj = new TD3DMatrix
        {
            _11 = 2f / aWidth,
            _22 = -2f / aHeight,
            _33 = 1f,
            _41 = -1f - 1f / aWidth,
            _42 = 1f + 1f / aHeight,
            _44 = 1f
        };
""")
            .Replace("D3DXMatrixTranslation(l_Temp, ( - 0.5), (aHeight + 0.5), 0.0);", "")
            .Replace("D3DXMatrixMultiply(f_matProj, f_matProj, l_Temp);", "")
            .Replace("D3DXMatrixOrthoOffCenterLH(l_Temp, 0, aWidth, 0, aHeight, 0, 1.0);", "")
            .Replace("D3DXMatrixIdentity(f_matView);",
                "f_matView = new TD3DMatrix { _11 = 1f, _22 = 1f, _33 = 1f, _44 = 1f };")
            .Replace("f_D3DDevice.SetTransform(D3DTS_PROJECTION, f_matProj);",
                "f_D3DDevice.SetTransform(3, ref f_matProj);")
            .Replace("f_D3DDevice.SetTransform(D3DTS_VIEW, f_matView);",
                "f_D3DDevice.SetTransform(2, ref f_matView);")
            .Replace("New(l_Target);", "l_Target = new Td2dRenderTarget();")
            .Replace("Dispose(l_Target);", "ReleaseTargetResources(l_Target);")
            .Replace("if (~(f_CurTarget != null))", "if (f_CurTarget == null)")
            .Replace("if (~(f_D3DDevice != null))", "if (f_D3DDevice == null)")
            .Replace("f_D3DDevice.TestCooperativeLevel == D3DERR_DEVICELOST",
                "f_D3DDevice.TestCooperativeLevel() == unchecked((int)0x88760868)")
            .Replace("f_D3DDevice.Reset(ref f_D3DPP);",
                "if (f_D3DDevice.Reset(ref f_D3DPP) < 0) return;")
            .Replace("Failed(D3DXCreateTexture(f_D3DDevice, aWidth, aHeight, 1, D3DUSAGE_RENDERTARGET, f_D3DPP.BackBufferFormat, D3DPOOL_DEFAULT, l_Target.Tex))",
                "f_D3DDevice.CreateTexture(unchecked((uint)aWidth), unchecked((uint)aHeight), 1, 1u, f_D3DPP.BackBufferFormat, 0, out l_Target.Tex) < 0")
            .Replace("D3DXCreateTexture(f_D3DDevice, l_Target.Width, l_Target.Height, 1, D3DUSAGE_RENDERTARGET, f_D3DPP.BackBufferFormat, D3DPOOL_DEFAULT, l_Target.Tex);",
                "f_D3DDevice.CreateTexture(unchecked((uint)l_Target.Width), unchecked((uint)l_Target.Height), 1, 1u, f_D3DPP.BackBufferFormat, 0, out l_Target.Tex);")
            .Replace("l_Target.Tex.GetLevelDesc(0, l_Desc);",
                "if (l_Target.Tex.GetLevelDesc(0, out l_Desc) < 0) { ReleaseTargetResources(l_Target); return null; }")
            .Replace("l_Target.Width = l_Desc.Width;", "l_Target.Width = unchecked((int)l_Desc.Width);")
            .Replace("l_Target.Height = l_Desc.Height;", "l_Target.Height = unchecked((int)l_Desc.Height);")
            .Replace("private void ClearTargets()", """
private static void ReleaseTargetResources(Td2dRenderTarget target)
    {
        target.Depth = null;
        target.Tex = null;
    }

    private void ClearTargets()
""");
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
            string modifier = input.Unit.Name.Equals("f2LinkParser", StringComparison.OrdinalIgnoreCase) &&
                function.Name.Equals("GetLinkRegEx", StringComparison.OrdinalIgnoreCase)
                ? "private static "
                : "public static ";
            builder.Append(IndentAndModifySignature(translated, modifier, 1));
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
            if (UsesManagedStandaloneClass(input.SemanticModel.Name, group.Key))
                continue;

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

                // Route base-typed Windows context output to the text buffer.
                if (input.SemanticModel.Name.Equals("furqContext", StringComparison.OrdinalIgnoreCase) &&
                    group.Key.Equals("TFURQWinContext", StringComparison.OrdinalIgnoreCase) &&
                    function.Name.Equals("OutText", StringComparison.OrdinalIgnoreCase) &&
                    function.IsProcedure && !function.IsClassMethod &&
                    function.Parameters.Count == 1 &&
                    function.Parameters[0].Kind.Equals("const", StringComparison.OrdinalIgnoreCase) &&
                    function.Parameters[0].Type.Equals("string", StringComparison.OrdinalIgnoreCase) &&
                    !function.Modifiers.Contains("override", StringComparer.OrdinalIgnoreCase))
                {
                    function = function with
                    {
                        Modifiers = function.Modifiers.Append("override").ToArray()
                    };
                }

                function = NormalizeOverrideVisibility(
                    function, group.Key, input.SemanticModel);
                string translated = _functionTranslator.TranslateFunction(
                    function, body.Behavior, manageDestructorLifetime: true);
                if (input.SemanticModel.Name.Equals("furqContext", StringComparison.OrdinalIgnoreCase) &&
                    group.Key.Equals("TFURQCode", StringComparison.OrdinalIgnoreCase) &&
                    function.Name.Equals("Load", StringComparison.OrdinalIgnoreCase) &&
                    function.Parameters.Count == 2 &&
                    function.Parameters[1].Type.Equals("TFURQCodeLoadProc", StringComparison.OrdinalIgnoreCase))
                {
                    translated = AdaptQuestLoader(translated);
                }

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

    private static string AdaptQuestLoader(string source)
    {
        // Delphi string offsets are one-based, and Delete mutates its argument.
        return source
            .Replace("uint l_OpenPos;", "")
            .Replace("uint l_ClosePos;", "")
            .Replace("uint l_Pos;", "int l_Pos;")
            .Replace("uint l_Pos2;", "int l_Pos2;")
            .Replace("l_Pos = Pos(\"*/\", l_Str);", "l_Pos = l_Str.IndexOf(\"*/\", StringComparison.Ordinal) + 1;")
            .Replace("l_Pos = Pos(\"/*\", l_Str);", "l_Pos = l_Str.IndexOf(\"/*\", StringComparison.Ordinal) + 1;")
            .Replace("l_Pos = Pos(\";\", aList[I]);", "l_Pos = aList[I].IndexOf(';') + 1;")
            .Replace("PosEx(\"*/\", l_Str, (l_Pos + 2))", "(l_Str.IndexOf(\"*/\", l_Pos + 1, StringComparison.Ordinal) + 1)")
            .Replace("Delete(l_Str, 1, (l_Pos + 1));", "l_Str = l_Str[(l_Pos + 1)..];")
            .Replace("Delete(l_Str, l_Pos, ((l_Pos2 - l_Pos) + 2));", "l_Str = l_Str.Remove(l_Pos - 1, l_Pos2 - l_Pos + 2);")
            .Replace("Delete(l_Str, l_Pos, MaxInt);", "l_Str = l_Str[..(l_Pos - 1)];")
            .Replace("Copy(aList[I], 1, (l_Pos - 1))", "aList[I][..(l_Pos - 1)]")
            .Replace("Pred(aList.Count)", "(aList.Count - 1)")
            .Replace("Pred(f_Source.Count)", "(f_Source.Count - 1)")
            .Replace("Pred(l_Buf.Count)", "(l_Buf.Count - 1)")
            .Replace("Pred(l_Includes.Count)", "(l_Includes.Count - 1)")
            .Replace("AnsiReplaceStr(f_Source[I], '\\t', \"    \")", "f_Source[I].Replace(\"\\t\", \"    \")")
            .Replace("Length(l_Str)", "l_Str.Length")
            .Replace("for (J = 1; J <= l_Len; J++)", "var chars = l_Str.ToCharArray();\n        for (J = 0; J < l_Len; J++)")
            .Replace("l_Str[J]", "chars[J]")
            .Replace("TrimLeft(l_Str)", "new string(chars).TrimStart()")
            .Replace("AnsiStartsStr(\":\", l_Str)", "l_Str.StartsWith(\":\", StringComparison.Ordinal)")
            .Replace("Delete(l_Str, 1, 1);", "l_Str = l_Str[1..];")
            .Replace("Trim(l_Str)", "l_Str.Trim()")
            .Replace("Trim(l_Buf[I])", "l_Buf[I].Trim()")
            .Replace("AnsiStartsText(c_sUsePrefix, l_Label)", "l_Label.StartsWith(c_sUsePrefix, StringComparison.OrdinalIgnoreCase)")
            .Replace("AnsiStartsText(c_sInclude, l_Str)", "l_Str.StartsWith(c_sInclude, StringComparison.OrdinalIgnoreCase)")
            .Replace("Copy(l_Label, 5, MaxInt)", "l_Label[Math.Min(4, l_Label.Length)..]")
            .Replace("Trim(Copy(l_Str, 10, MaxInt))", "l_Str[Math.Min(9, l_Str.Length)..].Trim()")
            .Replace("aLoadProc(aOneFileName)", "aLoadProc(aOneFilename)")
            .Replace("ExtractFilePath(aFilename)", "System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(aFilename))!")
            .Replace("ExtractFilePath(aOneFilename)", "System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(aOneFilename))!")
            .Replace("ExtractRelativePath(l_BasePath, aOneFilename)", "System.IO.Path.GetRelativePath(l_BasePath, aOneFilename)")
            .Replace("(l_Path + l_Includes[I])", "System.IO.Path.Combine(l_Path, l_Includes[I])");
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
