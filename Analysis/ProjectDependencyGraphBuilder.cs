namespace FireURQ.Porter.Analysis;

public static class ProjectDependencyGraphBuilder
{
    public static ProjectDependencyGraph Build(
        IReadOnlyList<ProjectAnalysisResult> analyses)
    {
        var units =
            new List<ProjectUnitNode>();

        var edges =
            new List<ProjectDependencyEdge>();

        // --------------------------------------------------------
        // Nodes
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
        // Unit dependencies
        // --------------------------------------------------------

        foreach (var analysis in analyses)
        {
            var unit =
                analysis.SemanticModel;

            foreach (var usedUnit in unit.Uses)
            {
                if (string.IsNullOrWhiteSpace(usedUnit))
                    continue;

                edges.Add(
                    new ProjectDependencyEdge(
                        unit.Name,
                        usedUnit,
                        DependencyKind.UsesUnit));
            }
        }

        return new ProjectDependencyGraph(
            units,
            edges);
    }
}