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
            unit.Name,
            "Unit",
            unit.Name);

        foreach (var type in unit.Types)
        {
            AddNode(
                nodes,
                type.Name,
                type.Name,
                $"Type:{type.Kind}",
                unit.Name);
        }

        foreach (var value in unit.Classes)
        {
            AddNode(
                nodes,
                value.Name,
                value.Name,
                "Class",
                unit.Name);
        }

        foreach (var value in unit.Interfaces)
        {
            AddNode(
                nodes,
                value.Name,
                value.Name,
                "Interface",
                unit.Name);
        }

        foreach (var value in unit.Constants)
        {
            AddNode(
                nodes,
                value.Name,
                value.Name,
                "Constant",
                unit.Name);
        }

        foreach (var value in unit.Functions)
        {
            AddNode(
                nodes,
                value.Name,
                value.Name,
                value.IsProcedure
                    ? "Procedure"
                    : "Function",
                unit.Name);
        }

        // Classes
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
                AddMemberNode(
                    nodes,
                    value.Name,
                    member,
                    unit.Name);

                if (member.Kind !=
                    SemanticMemberKind.MethodResolution)
                {
                    edges.Add(
                        new DependencyEdge(
                            value.Name,
                            $"{value.Name}.{member.Name}",
                            DependencyKind.Contains));
                }

                AddMemberDependencies(
                    nodes,
                    edges,
                    value.Name,
                    member,
                    unit.Name);
            }
        }

        // Interfaces
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
                AddMemberNode(
                    nodes,
                    value.Name,
                    member,
                    unit.Name);

                if (member.Kind !=
                    SemanticMemberKind.MethodResolution)
                {
                    edges.Add(
                        new DependencyEdge(
                            value.Name,
                            $"{value.Name}.{member.Name}",
                            DependencyKind.Contains));
                }

                AddMemberDependencies(
                    nodes,
                    edges,
                    value.Name,
                    member,
                    unit.Name);
            }
        }

        // Global functions / procedures
        foreach (var function in unit.Functions)
        {
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

        // Uses clause
        foreach (var usedUnit in unit.Uses)
        {
            if (string.IsNullOrWhiteSpace(
                    usedUnit))
            {
                continue;
            }

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

    private static void AddMemberNode(
        List<DependencyNode> nodes,
        string owner,
        SemanticMember member,
        string unitName)
    {
        string? kind =
            member.Kind switch
            {
                SemanticMemberKind.Field =>
                    "Field",

                SemanticMemberKind.Property =>
                    "Property",

                SemanticMemberKind.Method =>
                    "Method",

                _ =>
                    null
            };

        if (kind is null)
        {
            return;
        }

        string id =
            $"{owner}.{member.Name}";

        AddNode(
            nodes,
            id,
            member.Name,
            kind,
            unitName);
    }

    private static void AddMemberDependencies(
        List<DependencyNode> nodes,
        List<DependencyEdge> edges,
        string owner,
        SemanticMember member,
        string unitName)
    {
        switch (member.Kind)
        {
            case SemanticMemberKind.MethodResolution:

                AddNode(
                    nodes,
                    member.Name,
                    member.Name,
                    "InterfaceMethod",
                    unitName);

                AddNode(
                    nodes,
                    member.Type,
                    member.Type,
                    "ImplementationMethod",
                    unitName);

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
        string id,
        string name,
        string kind,
        string unitName)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        if (nodes.Any(
                node => node.Id.Equals(
                    id,
                    StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        nodes.Add(
            new DependencyNode(
                id,
                name,
                kind,
                unitName));
    }
}

