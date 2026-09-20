using System.Text;

using FireURQ.Porter.AST;
using FireURQ.Porter.Lexer;
using FireURQ.Porter.Parser;

namespace FireURQ.Porter.Analysis;

public static class BatchAnalyzer
{
    public static ProjectDependencyGraph? Analyze(
        string rootPath)
    {
        Console.WriteLine();
        Console.WriteLine("================================");
        Console.WriteLine(" FireURQ.Porter");
        Console.WriteLine(" Batch Analysis");
        Console.WriteLine("================================");
        Console.WriteLine();

        if (!Directory.Exists(rootPath))
        {
            Console.WriteLine("ERROR: Directory not found:");
            Console.WriteLine(rootPath);
            Console.WriteLine();
            return null;
        }

        Encoding.RegisterProvider(
            CodePagesEncodingProvider.Instance);

        var files =
            Directory
                .GetFiles(
                    rootPath,
                    "*.pas",
                    SearchOption.AllDirectories)
                .OrderBy(path => path)
                .ToList();

        Console.WriteLine(
            $"Delphi files found: {files.Count}");

        Console.WriteLine();

        int success = 0;
        int failed = 0;

        var progress =
            new ConsoleProgressBar();

        int totalTokens = 0;
        int totalClasses = 0;
        int totalInterfaces = 0;
        int totalTypes = 0;
        int totalConstants = 0;
        int totalFunctions = 0;
        int totalGraphNodes = 0;
        int totalGraphEdges = 0;

        var errors =
            new Dictionary<string, int>();

        var analyses =
            new List<ProjectAnalysisResult>();

        var behaviors =
            new List<BehaviorAnalyzer.BehaviorInfo>();

        foreach (var file in files)
        {
            string relativePath =
                Path.GetRelativePath(
                    rootPath,
                    file);

            try
            {
                string source =
                    File.ReadAllText(
                        file,
                        Encoding.GetEncoding(1251));

                var lexer =
                    new DelphiLexer(source);

                var tokens =
                    lexer.Tokenize();

                var parser =
                    new DelphiParser(tokens);

                DelphiUnit unit =
                    parser.ParseUnit();

                var semanticModel =
                    SemanticModelBuilder.Build(
                        unit);

                var dependencyGraph =
                    DependencyGraphBuilder.Build(
                        semanticModel);

                analyses.Add(
                    new ProjectAnalysisResult(
                        relativePath,
                        semanticModel,
                        dependencyGraph,
                        tokens.Count));

                /*
                 * Build behavior information from the same
                 * already parsed DelphiUnit.
                 *
                 * This does not create another lexer/parser pass.
                 */
                IReadOnlyList<BodyTokenAnalyzer.BodyInfo> bodies =
                    BodyTokenAnalyzer.Analyze(
                        unit,
                        file);

                foreach (
                    BodyTokenAnalyzer.BodyInfo body
                    in bodies)
                {
                    if (body.BodyTokens.Count == 0)
                        continue;

                    BehaviorAnalyzer.BehaviorInfo behavior =
                        BehaviorAnalyzer.Analyze(
                            body.FilePath,
                            body.Name,
                            body.BodyTokens);

                    behaviors.Add(behavior);
                }

                int classes =
                    semanticModel.Classes.Count;

                int interfaces =
                    semanticModel.Interfaces.Count;

                int types =
                    semanticModel.Types.Count;

                int constants =
                    semanticModel.Constants.Count;

                int functions =
                    semanticModel.Functions.Count;

                int graphNodes =
                    dependencyGraph.Nodes.Count;

                int graphEdges =
                    dependencyGraph.Edges.Count;

                totalTokens +=
                    tokens.Count;

                totalClasses +=
                    classes;

                totalInterfaces +=
                    interfaces;

                totalTypes +=
                    types;

                totalConstants +=
                    constants;

                totalFunctions +=
                    functions;

                totalGraphNodes +=
                    graphNodes;

                totalGraphEdges +=
                    graphEdges;

                success++;
            }
            catch (Exception ex)
            {
                failed++;

                string error =
                    ex.Message;

                if (errors.ContainsKey(error))
                {
                    errors[error]++;
                }
                else
                {
                    errors[error] = 1;
                }
            }

            progress.Report(
                success + failed,
                files.Count);
        }

        progress.Complete(
            files.Count,
            "Analysis complete");

        var semanticUnits =
            analyses
                .Select(
                    analysis => analysis.SemanticModel)
                .ToList();

        var symbolIndex =
            SymbolIndexBuilder.Build(
                semanticUnits);

        var resolver =
            new SemanticResolver(
                symbolIndex);

        Console.WriteLine();
        Console.WriteLine("================================");
        Console.WriteLine(" Symbol Index");
        Console.WriteLine("================================");
        Console.WriteLine();

        Console.WriteLine(
            $"Symbols: {symbolIndex.Count}");

        Console.WriteLine();
        Console.WriteLine("Tf2Context Members");
        Console.WriteLine("--------------------------------");

        SemanticClass? tf2Context =
            semanticUnits
                .SelectMany(unit => unit.Classes)
                .FirstOrDefault(
                    value => value.Name.Equals(
                        "Tf2Context",
                        StringComparison.OrdinalIgnoreCase));

        if (tf2Context is null)
        {
            Console.WriteLine("  <class not found>");
        }
        else
        {
            Console.WriteLine(
                $"Members: {tf2Context.Members.Count}");

            foreach (SemanticMember member in tf2Context.Members)
            {
                Console.WriteLine(
                    $"  {member.Kind,-18} " +
                    $"{member.Name,-35} " +
                    $"Type={member.Type}");
            }
        }

        Console.WriteLine();

        foreach (string name in new[]
        {
    "gD2DE",
    "Tf2Context",
    "System_Log",
    "WHandle",
    "OutError",
    "VariantToAlign"
})
        {
            Console.WriteLine(
                $"Lookup: {name}");

            IReadOnlyList<SymbolInfo> symbols =
                symbolIndex.Find(name);

            if (symbols.Count == 0)
            {
                Console.WriteLine(
                    "  <not found>");
            }
            else
            {
                foreach (SymbolInfo symbol in symbols)
                {
                    Console.WriteLine(
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

            Console.WriteLine();
        }

        Console.WriteLine();
        Console.WriteLine("Semantic Resolver");
        Console.WriteLine("--------------------------------");

        foreach (string reference in new[]
        {
    "gD2DE.System_Log",
    "gD2DE.WHandle",
    "gD2DE.Input_GetEvent"
})
        {
            ResolvedSymbol? resolved =
                resolver.ResolveMember(
                    reference);

            Console.WriteLine(
                $"Resolve: {reference}");

            if (resolved is null)
            {
                Console.WriteLine(
                    "  <not resolved>");

                continue;
            }

            Console.WriteLine(
                $"  {resolved.Symbol.Kind,-12} " +
                $"{resolved.Symbol.QualifiedName,-35} " +
                $"Unit={resolved.Symbol.UnitName} " +
                $"Owner={resolved.Symbol.OwnerName}");
        }

        Console.WriteLine();

        Console.WriteLine("================================");
        Console.WriteLine(" FireURQ Project Summary");
        Console.WriteLine("================================");

        Console.WriteLine(
            $"Files found:      {files.Count}");

        Console.WriteLine(
            $"Parsed:           {success}");

        Console.WriteLine(
            $"Failed:           {failed}");

        Console.WriteLine();

        Console.WriteLine(
            $"Total tokens:     {totalTokens}");

        Console.WriteLine(
            $"Total classes:    {totalClasses}");

        Console.WriteLine(
            $"Total interfaces: {totalInterfaces}");

        Console.WriteLine(
            $"Total types:      {totalTypes}");

        Console.WriteLine(
            $"Total constants:  {totalConstants}");

        Console.WriteLine(
            $"Total functions:  {totalFunctions}");

        Console.WriteLine();

        Console.WriteLine(
            $"Graph nodes:      {totalGraphNodes}");

        Console.WriteLine(
            $"Graph edges:      {totalGraphEdges}");

        if (errors.Count > 0)
        {
            Console.WriteLine();

            Console.WriteLine("================================");
            Console.WriteLine(" Failure Categories");
            Console.WriteLine("================================");

            foreach (
                var item in
                errors
                    .OrderByDescending(
                        x => x.Value)
                    .ThenBy(x => x.Key))
            {
                Console.WriteLine(
                    $"  {item.Value,3}x  {item.Key}");
            }
        }

        /*
         * Expression census.
         *
         * The BehaviorInfo objects were collected during
         * the same file-analysis pass above.
         */

        Console.WriteLine();
        Console.WriteLine("Behavior Names");
        Console.WriteLine("--------------------------------");

        foreach (BehaviorAnalyzer.BehaviorInfo behavior in behaviors.Take(30))
        {
            Console.WriteLine(
                $"{behavior.Name,-50} {behavior.FilePath}");
        }

        Console.WriteLine();

        var expressionCounts =
            ExpressionCensus.Analyze(
                behaviors);

        ExpressionCensus.Print(
            expressionCounts);

        var semanticCounts =
            ExpressionCensus.AnalyzeSemantics(
                behaviors);

        ExpressionCensus.PrintSemantic(
            semanticCounts);

        var semanticEdges =
            SemanticDependencyBuilder.Build(
                semanticCounts.References);

        Console.WriteLine();
        Console.WriteLine("================================");
        Console.WriteLine(" Semantic Dependency Graph");
        Console.WriteLine("================================");
        Console.WriteLine();

        foreach (
            SemanticDependencyEdge edge
            in semanticEdges.Take(30))
        {
            Console.WriteLine(
                $"{edge.From,-40} " +
                $"--{edge.Kind}--> " +
                $"{edge.To,-50} " +
                $"{edge.FilePath}:{edge.Line}");
        }

        Console.WriteLine();
        Console.WriteLine(
            $"Semantic dependency edges: {semanticEdges.Count}");
        Console.WriteLine();

        Console.WriteLine();

        // ====================================================
        // PROJECT DEPENDENCY GRAPH
        // ====================================================

        var projectGraph =
            ProjectDependencyGraphBuilder.Build(
                analyses);

        Console.WriteLine("================================");
        Console.WriteLine(" Project Dependency Graph");
        Console.WriteLine("================================");

        Console.WriteLine();

        Console.WriteLine(
            $"Units: {projectGraph.Units.Count}");

        Console.WriteLine(
            $"Unit edges: {projectGraph.Edges.Count}");

        Console.WriteLine();

        Console.WriteLine(
            "Exporting project graph...");

        string projectGraphPath =
            Path.Combine(
                Directory.GetCurrentDirectory(),
                "fireurq.project.graph.json");

        DependencyGraphJsonExporter.Export(
            projectGraph,
            projectGraphPath);

        Console.WriteLine(
            $"Project graph: {projectGraphPath}");

        Console.WriteLine();

        Console.WriteLine("================================");
        Console.WriteLine(" Batch Analysis Complete");
        Console.WriteLine("================================");

        Console.WriteLine();

        return projectGraph;
    }
}
