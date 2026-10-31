namespace FireURQ.Porter.Analysis;

/// <summary>
/// Describes the result of resolving a Delphi type name through declared
/// aliases and pointer declarations.
/// </summary>
public sealed record TypeResolution(
    string DeclaredName,
    string ResolvedName,
    int PointerDepth,
    IReadOnlyList<string> Chain,
    bool HasCycle)
{
    public bool IsPointer => PointerDepth > 0;
    public bool IsResolved => !HasCycle;
}

/// <summary>
/// Resolves Delphi type aliases and preserves pointer indirection.
/// This resolver deliberately does not choose a C# representation; that is
/// a separate translation decision.
/// </summary>
public sealed class TypeResolver
{
    private readonly Dictionary<string, SemanticType> _types;

    public TypeResolver(IEnumerable<SemanticUnit> units)
    {
        ArgumentNullException.ThrowIfNull(units);

        _types = units
            .SelectMany(unit => unit.Types)
            .GroupBy(type => type.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.First(),
                StringComparer.OrdinalIgnoreCase);
    }

    public TypeResolution Resolve(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
        {
            return new TypeResolution(
                typeName ?? string.Empty,
                typeName ?? string.Empty,
                0,
                Array.Empty<string>(),
                false);
        }

        string current = typeName.Trim();
        int pointerDepth = 0;
        var chain = new List<string>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        while (_types.TryGetValue(current, out SemanticType? declaration))
        {
            if (!visited.Add(current))
            {
                chain.Add(current);
                return new TypeResolution(
                    typeName,
                    current,
                    pointerDepth,
                    chain,
                    true);
            }

            chain.Add(current);

            if (declaration.Kind.Equals(
                    "Pointer",
                    StringComparison.OrdinalIgnoreCase))
            {
                pointerDepth++;
                current = declaration.Definition.Trim();
                if (current.Length == 0)
                    break;

                continue;
            }

            if (declaration.Kind.Equals(
                    "Simple",
                    StringComparison.OrdinalIgnoreCase))
            {
                string target = declaration.Definition.Trim();
                if (target.Length == 0)
                    break;

                current = target;
                continue;
            }

            // Arrays, records, enums, sets, classes and other concrete
            // declarations are terminal types for alias resolution.
            break;
        }

        return new TypeResolution(
            typeName,
            current,
            pointerDepth,
            chain,
            false);
    }

    public bool TryResolvePointer(
        string typeName,
        out TypeResolution resolution)
    {
        resolution = Resolve(typeName);
        return resolution.IsResolved && resolution.IsPointer;
    }
}
