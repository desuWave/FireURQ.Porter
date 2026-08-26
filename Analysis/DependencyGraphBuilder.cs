namespace FireURQ.Porter.Analysis;

public static class DependencyGraphBuilder
{
    public static DependencyGraph Build(
        SemanticUnit unit)
    {
        var nodes =
            new List<DependencyNode>();

        var edges =
            new List<DependencyEdge>();

        AddNode(
            nodes,
            unit.Name,
            "Unit");

        foreach (var type in unit.Types)
        {
            AddNode(
                nodes,
                type.Name,
                $"Type:{type.Kind}");
        }

        foreach (var value in unit.Classes)
        {
            AddNode(
                nodes,
                value.Name,
                "Class");
        }

        foreach (var value in unit.Interfaces)
        {
            AddNode(
                nodes,
                value.Name,
                "Interface");
        }

        foreach (var value in unit.Constants)
        {
            AddNode(
                nodes,
                value.Name,
                "Constant");
        }

        foreach (var value in unit.Functions)
        {
            AddNode(
                nodes,
                value.Name,
                value.IsProcedure
                    ? "Procedure"
                    : "Function");
        }

        foreach (var value in unit.Classes)
        {
            if (!string.IsNullOrWhiteSpace(
                    value.BaseType))
            {
                edges.Add(
                    new DependencyEdge(
                        value.Name,
                        value.BaseType,
                        DependencyKind.Inherits));
            }

            foreach (var member in value.Members)
            {
                AddMemberDependencies(
                    nodes,
                    edges,
                    value.Name,
                    member);
            }
        }

        foreach (var value in unit.Interfaces)
        {
            if (!string.IsNullOrWhiteSpace(
                    value.BaseType))
            {
                edges.Add(
                    new DependencyEdge(
                        value.Name,
                        value.BaseType,
                        DependencyKind.Inherits));
            }

            foreach (var member in value.Members)
            {
                AddMemberDependencies(
                    nodes,
                    edges,
                    value.Name,
                    member);
            }
        }

        foreach (var function in unit.Functions)
        {
            AddNode(
                nodes,
                function.Name,
                function.IsProcedure
                    ? "Procedure"
                    : "Function");

            foreach (var parameter in function.Parameters)
            {
                if (!string.IsNullOrWhiteSpace(
                        parameter.Type))
                {
                    edges.Add(
                        new DependencyEdge(
                            function.Name,
                            parameter.Type,
                            DependencyKind.UsesType));
                }
            }

            if (!string.IsNullOrWhiteSpace(
                    function.ReturnType))
            {
                edges.Add(
                    new DependencyEdge(
                        function.Name,
                        function.ReturnType,
                        DependencyKind.UsesType));
            }
        }

        foreach (var usedUnit in unit.Uses)
        {
            edges.Add(
                new DependencyEdge(
                    unit.Name,
                    usedUnit,
                    DependencyKind.UsesUnit));
        }

        return new DependencyGraph(
            nodes,
            edges);
    }

    private static void AddMemberDependencies(
        List<DependencyNode> nodes,
        List<DependencyEdge> edges,
        string owner,
        SemanticMember member)
    {
        switch (member.Kind)
        {
            case SemanticMemberKind.MethodResolution:

                AddNode(
                    nodes,
                    member.Name,
                    "InterfaceMethod");

                AddNode(
                    nodes,
                    member.Type,
                    "ImplementationMethod");

                if (!string.IsNullOrWhiteSpace(
                        member.Type))
                {
                    edges.Add(
                        new DependencyEdge(
                            owner,
                            member.Name,
                            DependencyKind.ResolvesMethod));

                    edges.Add(
                        new DependencyEdge(
                            member.Name,
                            member.Type,
                            DependencyKind.ResolvesMethod));
                }

                break;

            default:

                if (!string.IsNullOrWhiteSpace(
                        member.Type))
                {
                    edges.Add(
                        new DependencyEdge(
                            owner,
                            member.Type,
                            DependencyKind.UsesType));
                }

                break;
        }
    }

    private static void AddNode(
        List<DependencyNode> nodes,
        string name,
        string kind)
    {
        if (string.IsNullOrWhiteSpace(name))
            return;

        if (nodes.Any(
                node => node.Name.Equals(
                    name,
                    StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        nodes.Add(
            new DependencyNode(
                name,
                kind));
    }
}