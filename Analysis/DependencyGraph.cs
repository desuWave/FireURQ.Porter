namespace FireURQ.Porter.Analysis;

public enum DependencyKind
{
    Inherits,
    UsesType,
    UsesInterface,
    MethodResolution,
    UsesUnit,
    ResolvesMethod
}

public sealed record DependencyNode(
    string Name,
    string Kind
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
