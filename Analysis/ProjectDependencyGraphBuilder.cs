namespace FireURQ.Porter.Analysis;

public static class ProjectDependencyGraphBuilder
{
    public static ProjectDependencyGraph Build(
        IReadOnlyList<ProjectAnalysisResult> analyses)
    {
        var units =
            new List<ProjectUnitNode>();

        var nodes =
            new List<DependencyNode>();

        var edges =
            new List<ProjectDependencyEdge>();

        // --------------------------------------------------------
        // Unit nodes
        // --------------------------------------------------------

        foreach (var analysis in analyses)
        {
            var semanticModel =
                analysis.SemanticModel;

            units.Add(
                new ProjectUnitNode(
                    semanticModel.Name,
                    analysis.FilePath,
                    semanticModel.Classes.Count,
                    semanticModel.Interfaces.Count,
                    semanticModel.Types.Count,
                    semanticModel.Constants.Count,
                    semanticModel.Functions.Count));
        }

        // --------------------------------------------------------
        // Full dependency graph
        // --------------------------------------------------------

        foreach (var analysis in analyses)
        {
            var dependencyGraph =
                analysis.DependencyGraph;

            // Nodes
            foreach (var node in dependencyGraph.Nodes)
            {
                AddNode(
                    nodes,
                    node);
            }

            // Edges
            foreach (var edge in dependencyGraph.Edges)
            {
                AddEdge(
                    edges,
                    new ProjectDependencyEdge(
                        edge.From,
                        edge.To,
                        edge.Kind));
            }
        }

        return new ProjectDependencyGraph(
            units,
            nodes,
            edges);
    }

    private static void AddNode(
        List<DependencyNode> nodes,
        DependencyNode node)
    {
        if (string.IsNullOrWhiteSpace(node.Name))
            return;

        bool exists =
            nodes.Any(
                existing =>
                    existing.Id.Equals(
                        node.Id,
                        StringComparison.OrdinalIgnoreCase) &&
                    existing.Kind.Equals(
                        node.Kind,
                        StringComparison.OrdinalIgnoreCase) &&
                    existing.UnitName.Equals(
                        node.UnitName,
                        StringComparison.OrdinalIgnoreCase));

        if (exists)
            return;

        nodes.Add(node);
    }

    private static void AddEdge(
        List<ProjectDependencyEdge> edges,
        ProjectDependencyEdge edge)
    {
        bool exists =
            edges.Any(
                existing =>
                    existing.From.Equals(
                        edge.From,
                        StringComparison.OrdinalIgnoreCase) &&
                    existing.To.Equals(
                        edge.To,
                        StringComparison.OrdinalIgnoreCase) &&
                    existing.Kind == edge.Kind);

        if (exists)
            return;

        edges.Add(edge);
    }
}