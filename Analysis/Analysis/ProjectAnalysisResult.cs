namespace FireURQ.Porter.Analysis;

public sealed record ProjectAnalysisResult(
    string RootPath,
    string FilePath,
    SemanticUnit SemanticModel,
    DependencyGraph DependencyGraph,
    int TokenCount
);
