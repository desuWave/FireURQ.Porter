namespace FireURQ.Porter.Analysis;

public static class DependencyGraphMerger
{
    public static DependencyGraph Merge(
        IEnumerable<DependencyGraph> graphs)
    {
        var nodes =
            new List<DependencyNode>();

        var edges =
            new List<DependencyEdge>();

        foreach (var graph in graphs)
        {
            foreach (var node in graph.Nodes)
            {
                AddNode(
                    nodes,
                    node);
            }

            foreach (var edge in graph.Edges)
            {
                AddEdge(
                    edges,
                    edge);
            }
        }

        return new DependencyGraph(
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
                    existing.Name.Equals(
                        node.Name,
                        StringComparison.OrdinalIgnoreCase) &&
                    existing.Kind.Equals(
                        node.Kind,
                        StringComparison.OrdinalIgnoreCase));

        if (exists)
            return;

        nodes.Add(node);
    }

    private static void AddEdge(
        List<DependencyEdge> edges,
        DependencyEdge edge)
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
