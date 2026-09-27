using System.Text;

namespace FireURQ.Porter.Analysis;

public static class AnalysisReportWriter
{
    public static void Write(
        string reportPath,
        string title,
        IReadOnlyList<ProjectAnalysisResult> analyses,
        IReadOnlyList<BehaviorAnalyzer.BehaviorInfo> behaviors,
        SymbolIndex symbolIndex,
        SemanticResolver resolver,
        IReadOnlyList<SemanticDependencyEdge> semanticEdges)
    {
        string? directory =
            Path.GetDirectoryName(reportPath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var writer =
            new StreamWriter(
                reportPath,
                false,
                new UTF8Encoding(false));

        writer.WriteLine("================================");
        writer.WriteLine($" {title}");
        writer.WriteLine("================================");
        writer.WriteLine();

        WriteProjectSummary(
            writer,
            analyses);

        WriteSemanticClasses(
            writer,
            analyses);

        WriteSymbolIndex(
            writer,
            symbolIndex);

        WriteTf2Context(
            writer,
            analyses);

        WriteLookups(
            writer,
            symbolIndex);

        WriteResolver(
        writer,
        symbolIndex,
        resolver);

        WriteFailureNote(
            writer);

        writer.WriteLine();
        writer.WriteLine("Behavior Names");
        writer.WriteLine("--------------------------------");

        foreach (
            BehaviorAnalyzer.BehaviorInfo behavior
            in behaviors.Take(30))
        {
            writer.WriteLine(
                $"{behavior.Name,-50} {behavior.FilePath}");
        }

        var expressionCounts =
            ExpressionCensus.Analyze(
                behaviors);

        ExpressionCensus.Print(
            expressionCounts,
            writer);

        var semanticCounts =
            ExpressionCensus.AnalyzeSemantics(
                behaviors);

        ExpressionCensus.PrintSemantic(
            semanticCounts,
            writer);

        writer.WriteLine();
        writer.WriteLine("================================");
        writer.WriteLine(" Semantic Dependency Graph");
        writer.WriteLine("================================");
        writer.WriteLine();

        foreach (
            SemanticDependencyEdge edge
            in semanticEdges.Take(30))
        {
            writer.WriteLine(
                $"{edge.From,-40} " +
                $"--{edge.Kind}--> " +
                $"{edge.To,-50} " +
                $"{edge.FilePath}:{edge.Line}");
        }

        writer.WriteLine();

        writer.WriteLine(
            $"Semantic dependency edges: " +
            $"{semanticEdges.Count}");

        writer.WriteLine();
        writer.WriteLine("================================");
        writer.WriteLine(" Report Complete");
        writer.WriteLine("================================");
    }

    private static void WriteProjectSummary(
        TextWriter writer,
        IReadOnlyList<ProjectAnalysisResult> analyses)
    {
        writer.WriteLine("================================");
        writer.WriteLine(" Project Summary");
        writer.WriteLine("================================");
        writer.WriteLine();

        writer.WriteLine(
            $"Files:             {analyses.Count}");

        writer.WriteLine(
            $"Tokens:            " +
            $"{analyses.Sum(value => value.TokenCount)}");

        writer.WriteLine(
            $"Classes:           " +
            $"{analyses.Sum(value => value.SemanticModel.Classes.Count)}");

        writer.WriteLine(
            $"Interfaces:        " +
            $"{analyses.Sum(value => value.SemanticModel.Interfaces.Count)}");

        writer.WriteLine(
            $"Types:             " +
            $"{analyses.Sum(value => value.SemanticModel.Types.Count)}");

        writer.WriteLine(
            $"Constants:         " +
            $"{analyses.Sum(value => value.SemanticModel.Constants.Count)}");

        writer.WriteLine(
            $"Functions:         " +
            $"{analyses.Sum(value => value.SemanticModel.Functions.Count)}");

        writer.WriteLine();

        writer.WriteLine("Files");
        writer.WriteLine("--------------------------------");

        foreach (
            ProjectAnalysisResult analysis
            in analyses.OrderBy(
                value => value.FilePath,
                StringComparer.OrdinalIgnoreCase))
        {
            writer.WriteLine(
                $"{analysis.FilePath}");
        }

        writer.WriteLine();
    }

    private static void WriteSemanticClasses(
        TextWriter writer,
        IReadOnlyList<ProjectAnalysisResult> analyses)
    {
        writer.WriteLine();
        writer.WriteLine("Semantic Classes");
        writer.WriteLine("--------------------------------");

        foreach (SemanticUnit unit in
            analyses
                .Select(value => value.SemanticModel)
                .OrderBy(
                    value => value.Name,
                    StringComparer.OrdinalIgnoreCase))
        {
            foreach (SemanticClass @class in unit.Classes)
            {
                writer.WriteLine(
                    $"Class: {@class.Name} " +
                    $"[Unit={unit.Name}]");

                foreach (SemanticMember member in @class.Members)
                {
                    writer.WriteLine(
                        $"  {member.Kind,-18} " +
                        $"{member.Name,-35} " +
                        $"Type={member.Type}");
                }
            }
        }

        writer.WriteLine();
    }

    private static void WriteSymbolIndex(
        TextWriter writer,
        SymbolIndex symbolIndex)
    {
        writer.WriteLine();
        writer.WriteLine("================================");
        writer.WriteLine(" Symbol Index");
        writer.WriteLine("================================");
        writer.WriteLine();

        writer.WriteLine(
            $"Symbols: {symbolIndex.Count}");

        writer.WriteLine();
    }

    private static void WriteTf2Context(
        TextWriter writer,
        IReadOnlyList<ProjectAnalysisResult> analyses)
    {
        writer.WriteLine("Tf2Context Members");
        writer.WriteLine("--------------------------------");

        SemanticClass? tf2Context =
            analyses
                .SelectMany(
                    analysis =>
                        analysis.SemanticModel.Classes)
                .FirstOrDefault(
                    value => value.Name.Equals(
                        "Tf2Context",
                        StringComparison.OrdinalIgnoreCase));

        if (tf2Context is null)
        {
            writer.WriteLine(
                "  <class not found>");
        }
        else
        {
            writer.WriteLine(
                $"Members: {tf2Context.Members.Count}");

            foreach (SemanticMember member in tf2Context.Members)
            {
                writer.WriteLine(
                    $"  {member.Kind,-18} " +
                    $"{member.Name,-35} " +
                    $"Type={member.Type}");
            }
        }

        writer.WriteLine();
    }

    private static void WriteLookups(
        TextWriter writer,
        SymbolIndex symbolIndex)
    {
        foreach (string name in new[]
        {
            "gD2DE",
            "Tf2Context",
            "System_Log",
            "WHandle",
            "OutError",
            "VariantToAlign",
            "f_CurrentSceneObj"
        })
        {
            writer.WriteLine(
                $"Lookup: {name}");

            IReadOnlyList<SymbolInfo> symbols =
                symbolIndex.Find(name);

            if (symbols.Count == 0)
            {
                writer.WriteLine(
                    "  <not found>");
            }
            else
            {
                foreach (SymbolInfo symbol in symbols)
                {
                    writer.WriteLine(
                        $"  {symbol.Kind,-12} " +
                        $"{symbol.QualifiedName,-40} " +
                        $"Unit={symbol.UnitName}" +
                        (string.IsNullOrWhiteSpace(
                            symbol.OwnerName)
                            ? string.Empty
                            : $" Owner={symbol.OwnerName}") +
                        (string.IsNullOrWhiteSpace(
                            symbol.Type)
                            ? string.Empty
                            : $" Type={symbol.Type}"));
                }
            }

            writer.WriteLine();
        }
    }

private static void WriteResolver(
    TextWriter writer,
    SymbolIndex symbolIndex,
    SemanticResolver resolver)
    {
IReadOnlyList<SymbolInfo> hmusic =
    symbolIndex.Find("HMUSIC");

writer.WriteLine("Index lookup: HMUSIC");

if (hmusic.Count == 0)
{
    writer.WriteLine("  Result: NOT FOUND");
}
else
{
    foreach (SymbolInfo symbol in hmusic)
    {
        writer.WriteLine(
            $"  Name: {symbol.Name}");

        writer.WriteLine(
            $"  QualifiedName: {symbol.QualifiedName}");

        writer.WriteLine(
            $"  Kind: {symbol.Kind}");

        writer.WriteLine(
            $"  Unit: {symbol.UnitName}");
    }
}

writer.WriteLine();

        foreach (string reference in new[]
        {
            "gD2DE.System_Log",
            "gD2DE.WHandle",
            "gD2DE.Input_GetEvent",
            "f_CurrentSceneObj.ProcessEvent",
            "Dynamic_Bass.HMUSIC"
        })
        {
            ResolvedSymbol? resolved =
                reference == "Dynamic_Bass.HMUSIC"
                    ? resolver.ResolveQualified(reference)
                    : resolver.ResolveMember(reference);

            writer.WriteLine(
                $"Resolve: {reference}");

            if (resolved is null)
            {
                writer.WriteLine(
                    "  Result: NOT RESOLVED");
            }
            else
            {
                writer.WriteLine(
                    $"  Result: {resolved.Symbol.Kind}");

                writer.WriteLine(
                    $"  QualifiedName: {resolved.Symbol.QualifiedName}");

                writer.WriteLine(
                    $"  Unit: {resolved.Symbol.UnitName}");

                writer.WriteLine(
                    $"  Type: {resolved.Symbol.Type ?? "<none>"}");
            }

            writer.WriteLine();
        }
    }
    private static void WriteFailureNote(
        TextWriter writer)
    {
        writer.WriteLine(
            "Failure Categories");

        writer.WriteLine("--------------------------------");

        writer.WriteLine(
            "Failure details are reported by BatchAnalyzer.");

        writer.WriteLine();
    }
}
