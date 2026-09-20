namespace FireURQ.Porter.Analysis;

public enum SemanticDependencyKind
{
    Reads,
    Writes,
    Calls,
    AccessesMember,
    Indexes
}

public sealed record SemanticDependencyEdge(
    string From,
    string To,
    SemanticDependencyKind Kind,
    string FilePath,
    string ProcedureName,
    int Line);

public static class SemanticDependencyBuilder
{
    public static IReadOnlyList<SemanticDependencyEdge> Build(
        IReadOnlyList<ExpressionCensus.SemanticReference> references)
    {
        var edges =
            new List<SemanticDependencyEdge>();

        foreach (
            ExpressionCensus.SemanticReference reference
            in references)
        {
            SemanticDependencyKind? kind =
                reference.Kind switch
                {
                    ExpressionCensus.SemanticReferenceKind.Read =>
                        SemanticDependencyKind.Reads,

                    ExpressionCensus.SemanticReferenceKind.Write =>
                        SemanticDependencyKind.Writes,

                    ExpressionCensus.SemanticReferenceKind.Call =>
                        SemanticDependencyKind.Calls,

                    ExpressionCensus.SemanticReferenceKind.MemberAccess =>
                        SemanticDependencyKind.AccessesMember,

                    ExpressionCensus.SemanticReferenceKind.IndexAccess =>
                        SemanticDependencyKind.Indexes,

                    _ =>
                        null
                };

            if (kind is null)
            {
                continue;
            }

            edges.Add(
                new SemanticDependencyEdge(
                    reference.ProcedureName,
                    reference.Text,
                    kind.Value,
                    reference.FilePath,
                    reference.ProcedureName,
                    reference.Line));
        }

        return edges;
    }
}