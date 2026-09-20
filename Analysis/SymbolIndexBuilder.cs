namespace FireURQ.Porter.Analysis;

public static class SymbolIndexBuilder
{
    public static SymbolIndex Build(
        IReadOnlyList<SemanticUnit> units)
    {
        var index = new SymbolIndex();

        foreach (SemanticUnit unit in units)
        {
            index.Add(
                new SymbolInfo(
                    unit.Name,
                    unit.Name,
                    SymbolKind.Unit,
                    unit.Name));

            foreach (SemanticType type in unit.Types)
            {
                index.Add(
                    new SymbolInfo(
                        type.Name,
                        type.Name,
                        SymbolKind.Type,
                        unit.Name));
            }

            foreach (SemanticClass @class in unit.Classes)
            {
                index.Add(
                    new SymbolInfo(
                        @class.Name,
                        @class.Name,
                        SymbolKind.Class,
                        unit.Name));

                foreach (SemanticMember member in @class.Members)
                {
                    AddMember(
                        index,
                        @class.Name,
                        unit.Name,
                        member);
                }
            }

            foreach (SemanticInterface @interface in unit.Interfaces)
            {
                index.Add(
                    new SymbolInfo(
                        @interface.Name,
                        @interface.Name,
                        SymbolKind.Interface,
                        unit.Name));

                foreach (SemanticMember member in @interface.Members)
                {
                    AddMember(
                        index,
                        @interface.Name,
                        unit.Name,
                        member);
                }
            }

            foreach (SemanticConstant constant in unit.Constants)
            {
                index.Add(
                    new SymbolInfo(
                        constant.Name,
                        constant.Name,
                        SymbolKind.Constant,
                        unit.Name,
                        Type: constant.Type));
            }

            foreach (SemanticVariable variable in unit.Variables)
            {
                index.Add(
                    new SymbolInfo(
                        variable.Name,
                        variable.Name,
                        SymbolKind.Variable,
                        unit.Name,
                        Type: variable.Type));
            }

            foreach (SemanticFunction function in unit.Functions)
            {
                index.Add(
                    new SymbolInfo(
                        function.Name,
                        function.Name,
                        function.IsProcedure
                            ? SymbolKind.Procedure
                            : SymbolKind.Function,
                        unit.Name,
                        Type: function.ReturnType));
            }
        }

        return index;
    }

    private static void AddMember(
        SymbolIndex index,
        string ownerName,
        string unitName,
        SemanticMember member)
    {
        SymbolKind? kind =
            member.Kind switch
            {
                SemanticMemberKind.Field =>
                    SymbolKind.Field,

                SemanticMemberKind.Property =>
                    SymbolKind.Property,

                SemanticMemberKind.Method =>
                    SymbolKind.Method,

                _ =>
                    null
            };

        if (kind is null)
            return;

        index.Add(
            new SymbolInfo(
                member.Name,
                $"{ownerName}.{member.Name}",
                kind.Value,
                unitName,
                ownerName,
                member.Type));
    }
}
