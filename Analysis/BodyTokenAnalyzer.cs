using FireURQ.Porter.AST;

namespace FireURQ.Porter.Analysis;

public static class BodyTokenAnalyzer
{
    public enum BodyOwnerKind
    {
        TopLevel,
        Class,
        Interface
    }

    public sealed record BodyInfo(
        string FilePath,
        string Name,
        bool IsProcedure,
        int TokenCount,
        BodyOwnerKind OwnerKind);

    public static IReadOnlyList<BodyInfo> Analyze(
        DelphiUnit unit,
        string filePath)
    {
        var bodies =
            new List<BodyInfo>();

        foreach (var declaration in unit.Declarations)
        {
            CollectFunction(
                declaration,
                filePath,
                bodies);
        }

        return bodies;
    }

    private static void CollectFunction(
        DelphiNode declaration,
        string filePath,
        List<BodyInfo> bodies)
    {
        if (declaration is DelphiFunction function)
        {
            bodies.Add(
                new BodyInfo(
                    filePath,
                    function.Name,
                    function.IsProcedure,
                    function.BodyTokens.Count,
                    BodyOwnerKind.TopLevel));

            return;
        }

        if (declaration is DelphiClass delphiClass)
        {
            foreach (var member in delphiClass.Members)
            {
                if (member.Node is DelphiFunction classFunction)
                {
                    bodies.Add(
                        new BodyInfo(
                            filePath,
                            $"{delphiClass.Name}.{classFunction.Name}",
                            classFunction.IsProcedure,
                            classFunction.BodyTokens.Count,
                            BodyOwnerKind.Class));
                }
            }

            return;
        }

        if (declaration is DelphiInterface delphiInterface)
        {
            foreach (var member in delphiInterface.Members)
            {
                if (member.Node is DelphiFunction interfaceFunction)
                {
                    bodies.Add(
                        new BodyInfo(
                            filePath,
                            $"{delphiInterface.Name}.{interfaceFunction.Name}",
                            interfaceFunction.IsProcedure,
                            interfaceFunction.BodyTokens.Count,
                            BodyOwnerKind.Interface));
                }
            }
        }
    }

    public static void PrintZeroBodyAudit(
        IReadOnlyList<BodyInfo> bodies)
    {
        Console.WriteLine();
        Console.WriteLine(
            "================================");
        Console.WriteLine(
            " Zero-Body Audit");
        Console.WriteLine(
            "================================");
        Console.WriteLine();

        var zeroBodies =
            bodies
                .Where(x => x.TokenCount == 0)
                .OrderBy(x => x.FilePath)
                .ThenBy(x => x.OwnerKind)
                .ThenBy(x => x.Name)
                .ToList();

        Console.WriteLine(
            $"Zero-body callables: {zeroBodies.Count}");

        Console.WriteLine();

        foreach (var group in
                 zeroBodies.GroupBy(x => x.FilePath))
        {
            Console.WriteLine(
                $"FILE: {group.Key}");

            foreach (var body in group)
            {
                string kind =
                    body.IsProcedure
                        ? "procedure"
                        : "function";

                Console.WriteLine(
                    $"  {body.OwnerKind,-9} " +
                    $"{kind,-9} " +
                    $"{body.Name}");
            }

            Console.WriteLine();
        }
    }

    public static void PrintTopLevelImplementationAudit(
        IReadOnlyList<BodyInfo> bodies)
    {
        Console.WriteLine();
        Console.WriteLine(
            "================================");
        Console.WriteLine(
            " Top-Level Implementation Audit");
        Console.WriteLine(
            "================================");
        Console.WriteLine();

        var topLevel =
            bodies
                .Where(x =>
                    x.OwnerKind ==
                    BodyOwnerKind.TopLevel)
                .ToList();

        var implementations =
            topLevel
                .Where(x => x.TokenCount > 0)
                .GroupBy(x => x.Name)
                .ToDictionary(
                    x => x.Key,
                    x => x.Count());

        var zeroBodies =
            topLevel
                .Where(x => x.TokenCount == 0)
                .OrderBy(x => x.FilePath)
                .ThenBy(x => x.Name)
                .ToList();

        int matched = 0;
        int unmatched = 0;

        foreach (var body in zeroBodies)
        {
            if (implementations.ContainsKey(body.Name))
            {
                matched++;
            }
            else
            {
                unmatched++;
            }
        }

        Console.WriteLine(
            $"TopLevel zero-body:              {zeroBodies.Count}");

        Console.WriteLine(
            $"Matched by implementation:       {matched}");

        Console.WriteLine(
            $"Unmatched TopLevel zero-body:    {unmatched}");

        Console.WriteLine();

        if (unmatched > 0)
        {
            Console.WriteLine(
                "Suspicious TopLevel declarations");
            Console.WriteLine(
                "--------------------------------");

            foreach (var body in zeroBodies)
            {
                if (implementations.ContainsKey(body.Name))
                {
                    continue;
                }

                string kind =
                    body.IsProcedure
                        ? "procedure"
                        : "function";

                Console.WriteLine(
                    $"  {kind,-9} {body.Name}");

                Console.WriteLine(
                    $"             {body.FilePath}");
            }
        }
    }

}