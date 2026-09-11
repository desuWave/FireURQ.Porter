namespace FireURQ.Porter.Analysis;

public sealed record ProjectUnitNode(
    string Name,
    string FilePath,
    int Classes,
    int Interfaces,
    int Types,
    int Constants,
    int Functions
);

public sealed record ProjectDependencyEdge(
    string From,
    string To,
    DependencyKind Kind
);

public sealed record ProjectDependencyGraph(
    IReadOnlyList<ProjectUnitNode> Units,
    IReadOnlyList<DependencyNode> Nodes,
    IReadOnlyList<ProjectDependencyEdge> Edges
);