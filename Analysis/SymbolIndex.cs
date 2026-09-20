namespace FireURQ.Porter.Analysis;

public enum SymbolKind
{
    Unit,
    Type,
    Class,
    Interface,
    Constant,
    Variable,
    Function,
    Procedure,
    Field,
    Property,
    Method
}

public sealed record SymbolInfo(
    string Name,
    string QualifiedName,
    SymbolKind Kind,
    string UnitName,
    string? OwnerName = null,
    string? Type = null);

public sealed class SymbolIndex
{
    private readonly Dictionary<string, List<SymbolInfo>> _symbols =
        new(StringComparer.OrdinalIgnoreCase);

    public void Add(SymbolInfo symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol.Name))
            return;

        if (!_symbols.TryGetValue(symbol.Name, out List<SymbolInfo>? list))
        {
            list = new List<SymbolInfo>();
            _symbols[symbol.Name] = list;
        }

        if (list.Any(existing =>
                existing.QualifiedName.Equals(
                    symbol.QualifiedName,
                    StringComparison.OrdinalIgnoreCase) &&
                existing.UnitName.Equals(
                    symbol.UnitName,
                    StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        list.Add(symbol);
    }

    public IReadOnlyList<SymbolInfo> Find(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Array.Empty<SymbolInfo>();

        return _symbols.TryGetValue(
            name,
            out List<SymbolInfo>? symbols)
            ? symbols
            : Array.Empty<SymbolInfo>();
    }

    public IReadOnlyList<SymbolInfo> FindQualified(
        string qualifiedName)
    {
        if (string.IsNullOrWhiteSpace(qualifiedName))
            return Array.Empty<SymbolInfo>();

        return _symbols.Values
            .SelectMany(list => list)
            .Where(symbol =>
                symbol.QualifiedName.Equals(
                    qualifiedName,
                    StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public int Count =>
        _symbols.Values.Sum(list => list.Count);
}
