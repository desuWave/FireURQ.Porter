using System.Text;

using FireURQ.Porter.AST;
using FireURQ.Porter.Lexer;
using FireURQ.Porter.Parser;
using FireURQ.Porter.Translation;

namespace FireURQ.Porter.Analysis;

public static class BatchAnalyzer
{
    public static ProjectDependencyGraph? Analyze(
        string rootPath)
    {
        return Analyze(
            new[] { rootPath });
    }

    public static ProjectDependencyGraph? Analyze(
        IReadOnlyList<string> rootPaths)
    {
        Console.WriteLine();
        Console.WriteLine("================================");
        Console.WriteLine(" FireURQ.Porter");
        Console.WriteLine(" Batch Analysis");
        Console.WriteLine("================================");
        Console.WriteLine();

        if (rootPaths.Count == 0)
        {
            Console.WriteLine(
                "ERROR: No analysis directories specified.");

            Console.WriteLine();

            return null;
        }

        foreach (string rootPath in rootPaths)
        {
            if (!Directory.Exists(rootPath))
            {
                Console.WriteLine(
                    "ERROR: Directory not found:");

                Console.WriteLine(rootPath);
                Console.WriteLine();

                return null;
            }
        }

        Encoding.RegisterProvider(
            CodePagesEncodingProvider.Instance);

        var files =
            rootPaths
                .SelectMany(
                    rootPath =>
                        Directory
                            .GetFiles(
                                rootPath,
                                "*.pas",
                                SearchOption.AllDirectories)
                            .Select(
                                file => new
                                {
                                    RootPath = rootPath,
                                    FilePath = file
                                }))
                .OrderBy(
                    item => item.RootPath,
                    StringComparer.OrdinalIgnoreCase)
                .ThenBy(
                    item => item.FilePath,
                    StringComparer.OrdinalIgnoreCase)
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

        var translationInputs =
            new List<ProjectTranslationInput>();

        foreach (var item in files)
        {
            string file =
                item.FilePath;

            string relativePath =
                Path.GetRelativePath(
                    item.RootPath,
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
                        item.RootPath,
                        relativePath,
                        semanticModel,
                        dependencyGraph,
                        tokens.Count));

                IReadOnlyList<BodyTokenAnalyzer.BodyInfo> bodies =
                    BodyTokenAnalyzer.Analyze(
                        unit,
                        file);

                var translationBodies =
                    new List<(
                        BodyTokenAnalyzer.BodyInfo Body,
                        BehaviorAnalyzer.BehaviorInfo Behavior)>();

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

                    behaviors.Add(
                        behavior);

                    translationBodies.Add(
                        (body, behavior));
                }

                translationInputs.Add(
                    new ProjectTranslationInput(
                        item.RootPath,
                        file,
                        unit,
                        semanticModel,
                        translationBodies));

                totalTokens +=
                    tokens.Count;

                totalClasses +=
                    semanticModel.Classes.Count;

                totalInterfaces +=
                    semanticModel.Interfaces.Count;

                totalTypes +=
                    semanticModel.Types.Count;

                totalConstants +=
                    semanticModel.Constants.Count;

                totalFunctions +=
                    semanticModel.Functions.Count;

                totalGraphNodes +=
                    dependencyGraph.Nodes.Count;

                totalGraphEdges +=
                    dependencyGraph.Edges.Count;

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

        Console.WriteLine();

        /*
         * Everything below this point works with the already
         * parsed semantic model and behavior information.
         *
         * No second lexer/parser pass is performed.
         */

        var semanticUnits =
            analyses
                .Select(
                    analysis => analysis.SemanticModel)
                .ToList();

        /*
         * One common symbol index for all roots.
         *
         * This is important for cross-tree resolution:
         *
         * F2 + D2D
         *      v
         * common SymbolIndex
         *      v
         * common SemanticResolver
         */
        var symbolIndex =
            SymbolIndexBuilder.Build(
                semanticUnits);

        var resolver =
            new SemanticResolver(
                symbolIndex);

        var expressionCounts =
            ExpressionCensus.Analyze(
                behaviors);

        var semanticCounts =
            ExpressionCensus.AnalyzeSemantics(
                behaviors);

        var semanticEdges =
            SemanticDependencyBuilder.Build(
                semanticCounts.References);

        /*
         * Build the complete project graph from all parsed units.
         */
        var projectGraph =
            ProjectDependencyGraphBuilder.Build(
                analyses);

        /*
         * Reports
         */
        string reportsDirectory =
            Path.Combine(
                Directory.GetCurrentDirectory(),
                "reports");

        Directory.CreateDirectory(
            reportsDirectory);

        foreach (string rootPath in rootPaths)
        {
            IReadOnlyList<ProjectAnalysisResult> rootAnalyses =
                analyses
                    .Where(
                        analysis =>
                            analysis.RootPath.Equals(
                                rootPath,
                                StringComparison.OrdinalIgnoreCase))
                    .ToList();

            IReadOnlyList<BehaviorAnalyzer.BehaviorInfo> rootBehaviors =
                behaviors
                    .Where(
                        behavior =>
                            behavior.FilePath.StartsWith(
                                rootPath,
                                StringComparison.OrdinalIgnoreCase))
                    .ToList();

            var rootSemanticEdges =
                semanticEdges
                    .Where(
                        edge =>
                            edge.FilePath.StartsWith(
                                rootPath,
                                StringComparison.OrdinalIgnoreCase))
                    .ToList();

            string reportFileName =
                GetReportFileName(
                    rootPath);

            string reportPath =
                Path.Combine(
                    reportsDirectory,
                    reportFileName);

            AnalysisReportWriter.Write(
                reportPath,
                GetReportTitle(rootPath),
                rootAnalyses,
                rootBehaviors,
                symbolIndex,
                resolver,
                rootSemanticEdges);

            Console.WriteLine(
                $"Report: {reportPath}");
        }

        Console.WriteLine();

        /*
         * Compact console summary.
         */
        Console.WriteLine("================================");
        Console.WriteLine(" FireURQ Project Summary");
        Console.WriteLine("================================");
        Console.WriteLine();

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

        Console.WriteLine();

        Console.WriteLine(
            $"Symbols:          {symbolIndex.Count}");

        Console.WriteLine(
            $"Semantic refs:    {semanticCounts.References.Count}");

        Console.WriteLine(
            $"Semantic edges:   {semanticEdges.Count}");

        Console.WriteLine();

        Console.WriteLine("Project Dependency Graph");
        Console.WriteLine("--------------------------------");

        Console.WriteLine(
            $"Units:             {projectGraph.Units.Count}");

        Console.WriteLine(
            $"Unit edges:        {projectGraph.Edges.Count}");

        if (errors.Count > 0)
        {
            Console.WriteLine();

            Console.WriteLine(
                $"Failures:          {failed}");

            Console.WriteLine(
                "See analysis reports for details.");
        }

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
        Console.WriteLine(" C# Translation");
        Console.WriteLine("================================");
        Console.WriteLine();

        string translationDirectory =
            Path.Combine(
                Directory.GetCurrentDirectory(),
                "translation-output");

        var translator =
            new ProjectTranslator();

        IReadOnlyList<TranslationResult> translationResults =
            translator.Translate(
                translationInputs,
                translationDirectory);

        int translationSuccess =
            translationResults.Count(
                result => result.Success);

        int translationFailed =
            translationResults.Count(
                result => !result.Success);

        int functionsTranslated =
            translationResults.Sum(
                result => result.FunctionsTranslated);

        int todoCount =
            translationResults.Sum(
                result => result.TodoCount);

        Console.WriteLine(
            $"Translation files:      {translationResults.Count}");

        Console.WriteLine(
            $"Translation succeeded:  {translationSuccess}");

        Console.WriteLine(
            $"Translation failed:     {translationFailed}");

        Console.WriteLine(
            $"Functions translated:   {functionsTranslated}");

        Console.WriteLine(
            $"TODO markers:           {todoCount}");

        Console.WriteLine(
            $"Output:                 {translationDirectory}");

        Console.WriteLine();

        Console.WriteLine("================================");
        Console.WriteLine(" Batch Analysis Complete");
        Console.WriteLine("================================");
        Console.WriteLine();

        return projectGraph;
    }

    private static string GetReportFileName(
        string rootPath)
    {
        string directoryName =
            new DirectoryInfo(rootPath).Name;

        if (directoryName.Equals(
                "fireurq-master",
                StringComparison.OrdinalIgnoreCase))
        {
            return "fireurq.analysis.txt";
        }

        if (directoryName.Equals(
                "d2dge-furq_2_2_4",
                StringComparison.OrdinalIgnoreCase))
        {
            return "d2d.analysis.txt";
        }

        string safeName =
            new string(
                directoryName
                    .Select(
                        character =>
                            char.IsLetterOrDigit(character)
                                ? character
                                : '_')
                    .ToArray());

        return $"{safeName}.analysis.txt";
    }

    private static string GetReportTitle(
        string rootPath)
    {
        string directoryName =
            new DirectoryInfo(rootPath).Name;

        if (directoryName.Equals(
                "fireurq-master",
                StringComparison.OrdinalIgnoreCase))
        {
            return "FireURQ Analysis Report";
        }

        if (directoryName.Equals(
                "d2dge-furq_2_2_4",
                StringComparison.OrdinalIgnoreCase))
        {
            return "D2D Analysis Report";
        }

        return $"{directoryName} Analysis Report";
    }
}




