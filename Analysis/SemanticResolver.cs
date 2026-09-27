namespace FireURQ.Porter.Analysis;

public sealed record ResolvedSymbol(
    string Reference,
    SymbolInfo Symbol);

public sealed class SemanticResolver
{
    private readonly SymbolIndex _index;

    public SemanticResolver(
        SymbolIndex index)
    {
        _index = index;
    }

    public ResolvedSymbol? ResolveMember(
        string reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
            return null;

        int dot =
            reference.IndexOf('.');

        if (dot <= 0 ||
            dot >= reference.Length - 1)
        {
            return null;
        }

        string root =
            reference[..dot];

        string memberName =
            reference[(dot + 1)..];

        IReadOnlyList<SymbolInfo> roots =
            _index.Find(root);

            foreach (SymbolInfo rootSymbol in roots)
            {
            if (rootSymbol.Kind != SymbolKind.Variable &&
                rootSymbol.Kind != SymbolKind.Field)
            {
            continue;
            }

            if (string.IsNullOrWhiteSpace(
                    rootSymbol.Type))
            {
                continue;
            }

            string typeName =
                rootSymbol.Type;

            IReadOnlyList<SymbolInfo> members =
                _index.Find(memberName);

            foreach (SymbolInfo member in members)
            {
                if (member.OwnerName?.Equals(
                        typeName,
                        StringComparison.OrdinalIgnoreCase)
                    == true)
                {
                    return new ResolvedSymbol(
                        reference,
                        member);
                }
            }
        }

        return null;
    }
    public ResolvedSymbol? ResolveQualified(
        string reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
            return null;

        IReadOnlyList<SymbolInfo> symbols =
            _index.FindQualified(reference);

        return symbols.Count == 1
            ? new ResolvedSymbol(
                reference,
                symbols[0])
            : null;
    }
}