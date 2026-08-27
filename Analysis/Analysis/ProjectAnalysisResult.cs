namespace FireURQ.Porter.Analysis;

public sealed record ProjectAnalysisResult(
    string FilePath,
    SemanticUnit SemanticModel,
    DependencyGraph DependencyGraph,
    int TokenCount
);