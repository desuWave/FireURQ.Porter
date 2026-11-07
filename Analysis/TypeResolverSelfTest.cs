namespace FireURQ.Porter.Analysis;

public static class TypeResolverSelfTest
{
    public static int Run()
    {
        var types = new[]
        {
            new SemanticType("TNumber", "Simple", "Integer"),
            new SemanticType("PInteger", "Pointer", "Integer"),
            new SemanticType("TIntegerPointer", "Simple", "PInteger"),
            new SemanticType("PPInteger", "Pointer", "PInteger"),
            new SemanticType("TA", "Simple", "TB"),
            new SemanticType("TB", "Simple", "TA")
        };

        var unit = new SemanticUnit(
            "TypeResolverSelfTest",
            Array.Empty<string>(),
            Array.Empty<SemanticClass>(),
            Array.Empty<SemanticInterface>(),
            types,
            Array.Empty<SemanticConstant>(),
            Array.Empty<SemanticVariable>(),
            Array.Empty<SemanticFunction>());

        var resolver = new TypeResolver(new[] { unit });
        int failed = 0;

        var simple = resolver.Resolve("TNumber");
        Check(
            "Simple alias",
            simple.ResolvedName == "Integer" &&
            simple.PointerDepth == 0 &&
            !simple.HasCycle &&
            simple.Chain.SequenceEqual(new[] { "TNumber" }),
            ref failed);

        var pointer = resolver.Resolve("PInteger");
        Check(
            "Pointer",
            pointer.ResolvedName == "Integer" &&
            pointer.PointerDepth == 1 &&
            pointer.IsPointer,
            ref failed);

        var aliasPointer = resolver.Resolve("TIntegerPointer");
        Check(
            "Alias to pointer",
            aliasPointer.ResolvedName == "Integer" &&
            aliasPointer.PointerDepth == 1 &&
            aliasPointer.Chain.SequenceEqual(
                new[] { "TIntegerPointer", "PInteger" }),
            ref failed);

        var nestedPointer = resolver.Resolve("PPInteger");
        Check(
            "Nested pointer",
            nestedPointer.ResolvedName == "Integer" &&
            nestedPointer.PointerDepth == 2,
            ref failed);

        Check(
            "Case-insensitive lookup",
            resolver.Resolve("tnumber").ResolvedName == "Integer",
            ref failed);

        Check(
            "TryResolvePointer accepts alias",
            resolver.TryResolvePointer(
                "TIntegerPointer",
                out var resolvedPointer) &&
            resolvedPointer.PointerDepth == 1,
            ref failed);

        Check(
            "TryResolvePointer rejects non-pointer",
            !resolver.TryResolvePointer("TNumber", out _),
            ref failed);

        var cycle = resolver.Resolve("TA");
        Check(
            "Alias cycle detection",
            cycle.HasCycle &&
            !cycle.IsResolved &&
            cycle.Chain.SequenceEqual(new[] { "TA", "TB", "TA" }),
            ref failed);

        var unknown = resolver.Resolve("ExternalType");
        Check(
            "Unknown type is preserved",
            unknown.ResolvedName == "ExternalType" &&
            unknown.PointerDepth == 0 &&
            !unknown.HasCycle &&
            unknown.Chain.Count == 0 &&
            unknown.IsResolved,
            ref failed);

        Console.WriteLine();
        Console.WriteLine(
            $"TypeResolver self-test: {9 - failed}/9 passed.");

        return failed == 0 ? 0 : 1;
    }

    private static void Check(
        string name,
        bool condition,
        ref int failed)
    {
        if (condition)
        {
            Console.WriteLine($"[PASS] {name}");
            return;
        }

        Console.WriteLine($"[FAIL] {name}");
        failed++;
    }
}
