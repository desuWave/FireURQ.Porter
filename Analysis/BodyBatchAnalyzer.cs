using System.Text;

using FireURQ.Porter.AST;
using FireURQ.Porter.Lexer;
using FireURQ.Porter.Parser;

namespace FireURQ.Porter.Analysis;

public static class BodyBatchAnalyzer
{
    private static void AddLine(
        List<string> lines,
        string text = "")
    {
        lines.Add(text);
    }

    public static void Analyze(
        string rootPath,
        bool showProgress = false)
    {
        if (!Directory.Exists(rootPath))
        {
            Console.WriteLine(
                $"ERROR: Directory not found:\n{rootPath}");

            return;
        }

        var files =
            Directory
                .GetFiles(
                    rootPath,
                    "*.pas",
                    SearchOption.AllDirectories)
                .OrderBy(x => x)
                .ToList();

        Console.WriteLine(
            "================================");

        Console.WriteLine(
            " FireURQ.Porter");

        Console.WriteLine(
            " Body Token Batch Analysis");

        Console.WriteLine(
            "================================");

        Console.WriteLine();

        Console.WriteLine(
            $"Files found: {files.Count}");

        Console.WriteLine();

        int parsed = 0;
        int failed = 0;
        int processed = 0;

        var allBodies =
            new List<BodyTokenAnalyzer.BodyInfo>();

        var behaviors =
            new List<BehaviorAnalyzer.BehaviorInfo>();

        var fileResults =
            new List<string>();

        ConsoleProgressBar? progress =
            showProgress
                ? new ConsoleProgressBar()
                : null;

        foreach (var file in files)
        {
            string fileName =
                Path.GetFileName(file);

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

                var bodies =
                    BodyTokenAnalyzer.Analyze(
                        unit,
                        file);

                allBodies.AddRange(
                    bodies);

                foreach (
                    var body in
                    bodies.Where(
                        x => x.TokenCount > 0))
                {
                    var behavior =
                        BehaviorAnalyzer.Analyze(
                            body.FilePath,
                            body.Name,
                            body.BodyTokens);

                    behaviors.Add(
                        behavior);
                }

                parsed++;

                fileResults.Add(
                    $"{fileName,-32} OK");
            }
            catch (Exception ex)
            {
                failed++;

                fileResults.Add(
                    $"{fileName,-32} FAILED: " +
                    $"{ex.GetType().Name}: {ex.Message}");
            }

            processed++;

            progress?.Report(
                processed,
                files.Count);
        }

        if (progress is not null)
        {
            progress.Complete(
                files.Count,
                "Analysis complete");
        }

        // ========================================================
        // BEHAVIOR TARGETS
        // ========================================================

        var targetBehaviors =
            behaviors
                .OrderBy(x => x.FilePath)
                .ThenBy(x => x.Name)
                .ToList();

        // ========================================================
        // STATISTICS
        // ========================================================

        int withBody =
            allBodies.Count(
                x => x.TokenCount > 0);

        int withoutBody =
            allBodies.Count -
            withBody;

        int zeroTopLevel =
            allBodies.Count(
                x =>
                    x.TokenCount == 0 &&
                    x.OwnerKind ==
                        BodyTokenAnalyzer.BodyOwnerKind.TopLevel);

        int zeroQualifiedTopLevel =
            allBodies.Count(
                x =>
                    x.TokenCount == 0 &&
                    x.OwnerKind ==
                        BodyTokenAnalyzer.BodyOwnerKind.TopLevel &&
                    x.Name.Contains('.'));

        int zeroClass =
            allBodies.Count(
                x =>
                    x.TokenCount == 0 &&
                    x.OwnerKind ==
                        BodyTokenAnalyzer.BodyOwnerKind.Class);

        int zeroInterface =
            allBodies.Count(
                x =>
                    x.TokenCount == 0 &&
                    x.OwnerKind ==
                        BodyTokenAnalyzer.BodyOwnerKind.Interface);

        int totalTokens =
            allBodies.Sum(
                x => x.TokenCount);

        double average =
            withBody == 0
                ? 0
                : (double)totalTokens /
                  withBody;

        // ========================================================
        // PAGED LISTING
        // ========================================================

        var listingLines =
            new List<string>();

        // ========================================================
        // BEHAVIORAL ANALYSIS
        // ========================================================

        AddLine(
            listingLines,
            "================================");

        AddLine(
            listingLines,
            " Behavioral Analysis");

        AddLine(
            listingLines,
            "================================");

        AddLine(
            listingLines);

        AddLine(
            listingLines,
            $"Total behaviors: {targetBehaviors.Count}");

        AddLine(
            listingLines);

        foreach (var behavior in targetBehaviors)
        {
            var behaviorLines =
                new List<string>();

            BehaviorAnalyzer.Print(
                behavior,
                behaviorLines);

            foreach (var line in behaviorLines)
            {
                AddLine(
                    listingLines,
                    line);
            }
        }

        AddLine(
            listingLines);

        // ========================================================
        // FILE RESULTS
        // ========================================================

        AddLine(
            listingLines,
            "================================");

        AddLine(
            listingLines,
            " Files");

        AddLine(
            listingLines,
            "================================");

        foreach (string result in fileResults)
        {
            AddLine(
                listingLines,
                result);
        }

        AddLine(
            listingLines);

        // ========================================================
        // SUMMARY
        // ========================================================

        AddLine(
            listingLines,
            "================================");

        AddLine(
            listingLines,
            " FireURQ.Porter");

        AddLine(
            listingLines,
            " Body Token Analysis Result");

        AddLine(
            listingLines);

        AddLine(
            listingLines,
            $"Files found:                   {files.Count}");

        AddLine(
            listingLines,
            $"Parsed:                        {parsed}");

        AddLine(
            listingLines,
            $"Failed:                        {failed}");

        AddLine(
            listingLines);

        AddLine(
            listingLines,
            $"Callable declarations:         {allBodies.Count}");

        AddLine(
            listingLines,
            $"Bodies with tokens:            {withBody}");

        AddLine(
            listingLines,
            $"Bodies without tokens:         {withoutBody}");

        AddLine(
            listingLines,
            $"Total body tokens:             {totalTokens}");

        AddLine(
            listingLines,
            $"Average body tokens:           {average:F1}");

        AddLine(
            listingLines);

        // ========================================================
        // ZERO BODY AUDIT
        // ========================================================

        AddLine(
            listingLines,
            "Zero-body audit");

        AddLine(
            listingLines,
            "--------------------------------");

        AddLine(
            listingLines,
            $"Zero-body TopLevel:             {zeroTopLevel}");

        AddLine(
            listingLines,
            $"Zero-body qualified TopLevel:   {zeroQualifiedTopLevel}");

        AddLine(
            listingLines,
            $"Zero-body Class:                {zeroClass}");

        AddLine(
            listingLines,
            $"Zero-body Interface:            {zeroInterface}");

        AddLine(
            listingLines);

        // ========================================================
        // ALL BODIES
        // ========================================================

        AddLine(
            listingLines,
            "All callable declarations");

        AddLine(
            listingLines,
            "--------------------------------");

        foreach (
            var body in
            allBodies
                .OrderBy(x => x.FilePath)
                .ThenBy(x => x.Name)
                .ThenBy(x => x.OwnerKind))
        {
            string kind =
                body.IsProcedure
                    ? "procedure"
                    : "function";

            string owner =
                body.OwnerKind.ToString();

            AddLine(
                listingLines,
                $"{body.TokenCount,6}  " +
                $"{kind,-9} " +
                $"{body.Name}");

            AddLine(
                listingLines,
                $"        owner: {owner}");

            AddLine(
                listingLines,
                $"        file:  {Path.GetFileName(body.FilePath)}");
        }

        AddLine(
            listingLines);

        // ========================================================
        // LARGEST BODIES
        // ========================================================

        AddLine(
            listingLines,
            "================================");

        AddLine(
            listingLines,
            " Largest bodies");

        AddLine(
            listingLines,
            "--------------------------------");

        foreach (
            var body in
            allBodies
                .OrderByDescending(
                    x => x.TokenCount)
                .Take(20))
        {
            string kind =
                body.IsProcedure
                    ? "procedure"
                    : "function";

            AddLine(
                listingLines,
                $"{body.TokenCount,6}  " +
                $"{kind,-9} " +
                $"{body.Name}");
        }

        // ========================================================
        // VIEWER
        // ========================================================

        int pageSize =
            Math.Max(
                1,
                Console.WindowHeight - 7);

        var listing =
            new PagedListing(
                listingLines,
                pageSize);

        PagedListingViewer.Show(
            listing,
            "FireURQ.Porter - Body Token Analysis");
    }
}
