namespace FireURQ.Porter.Analysis;

public enum DependencyKind
{
    Inherits,
    UsesType,
    UsesInterface,
    UsesUnit,
    ResolvesMethod
}

public sealed record DependencyNode(
    string Id,
    string Name,
    string Kind,
    string UnitName
);

public sealed record DependencyEdge(
    string From,
    string To,
    DependencyKind Kind
);

public sealed record DependencyGraph(
    IReadOnlyList<DependencyNode> Nodes,
    IReadOnlyList<DependencyEdge> Edges
);
