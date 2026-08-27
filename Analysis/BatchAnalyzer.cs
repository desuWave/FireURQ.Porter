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

        foreach (var file in files)
        {
            string relativePath =
                Path.GetRelativePath(
                    rootPath,
                    file);

            Console.WriteLine("--------------------------------");
            Console.WriteLine(relativePath);
            Console.WriteLine("--------------------------------");

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

                Console.WriteLine(
                    $"  Uses:       {unit.Uses.Count}");

                foreach (var usedUnit in unit.Uses)
                {
                    Console.WriteLine(
                        $"    -> {usedUnit}");
                }

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

                Console.WriteLine(
                    $"  Unit:       {unit.Name}");

                Console.WriteLine(
                    $"  Tokens:     {tokens.Count}");

                Console.WriteLine(
                    $"  Classes:    {classes}");

                Console.WriteLine(
                    $"  Interfaces: {interfaces}");

                Console.WriteLine(
                    $"  Types:      {types}");

                Console.WriteLine(
                    $"  Constants:  {constants}");

                Console.WriteLine(
                    $"  Functions:  {functions}");

                Console.WriteLine(
                    $"  Graph nodes:{graphNodes}");

                Console.WriteLine(
                    $"  Graph edges:{graphEdges}");

                Console.WriteLine(
                    "  Status:     OK");

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
                Console.WriteLine(
                    "  Status:     FAIL");

                Console.WriteLine(
                    $"  Error:      {ex.Message}");

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

            Console.WriteLine();
        }

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

            foreach (var item in
                     errors
                         .OrderByDescending(
                             x => x.Value)
                         .ThenBy(x => x.Key))
            {
                Console.WriteLine(
                    $"  {item.Value,3}x  {item.Key}");
            }
        }

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
