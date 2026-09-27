using FireURQ.Porter.AST;

namespace FireURQ.Porter.Analysis;

public static class SemanticModelBuilder
{
    public static SemanticUnit Build(DelphiUnit unit)
    {
        var classes = new List<SemanticClass>();
        var interfaces = new List<SemanticInterface>();
        var types = new List<SemanticType>();
        var constants = new List<SemanticConstant>();
        var variables = new List<SemanticVariable>();
        var functions = new List<SemanticFunction>();

var fullClassNames =
    unit.Declarations
        .OfType<DelphiClass>()
        .Where(value => value.Members.Count > 0)
        .Select(value => value.Name)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

foreach (var declaration in unit.Declarations)
{
    switch (declaration)
    {
        case DelphiClass value:
            if (value.Members.Count == 0 &&
                fullClassNames.Contains(value.Name))
            {
                break;
            }

            classes.Add(BuildClass(value));
            break;

                case DelphiInterface value:
                    interfaces.Add(BuildInterface(value));
                    break;

                case DelphiType value:
                    types.Add(
                        new SemanticType(
                            value.Name,
                            value.Kind.ToString(),
                            value.Definition));
                    break;

                case DelphiConst value:
                    constants.Add(
                        new SemanticConstant(
                            value.Name,
                            value.Type,
                            value.Value));
                    break;

                case DelphiVariable value:
                    variables.Add(
                        new SemanticVariable(
                            value.Name,
                            value.Type));
                    break;

                case DelphiFunction value:
                    functions.Add(BuildFunction(value));
                    break;
            }
        }

        return new SemanticUnit(
            unit.Name,
            unit.Uses,
            classes,
            interfaces,
            types,
            constants,
            variables,
            functions);
    }

    private static SemanticClass BuildClass(
        DelphiClass value)
    {
        var members =
            value.Members
                .Select(BuildMember)
                .ToList();

        return new SemanticClass(
            value.Name,
            value.BaseType,
            members);
    }
    private static SemanticInterface BuildInterface(
        DelphiInterface value)
    {
        var members =
            value.Members
                .Select(BuildMember)
                .ToList();

        return new SemanticInterface(
            value.Name,
            value.BaseType,
            members);
    }

    private static SemanticMember BuildMember(
        DelphiClassMember member)
    {
        return member.Node switch
        {
            DelphiField field =>
                new SemanticMember(
                    field.Name,
                    SemanticMemberKind.Field,
                    field.Type,
                    member.Visibility.ToString(),
                    Array.Empty<SemanticParameter>(),
                    false),

            DelphiFunction function =>
                new SemanticMember(
                    function.Name,
                    SemanticMemberKind.Method,
                    function.IsProcedure
                        ? string.Empty
                        : function.ReturnType,
                    member.Visibility.ToString(),
                    function.Parameters
                        .Select(BuildParameter)
                        .ToList(),
                        false),

            DelphiProperty property =>
                new SemanticMember(
                    property.Name,
                    SemanticMemberKind.Property,
                    property.Type,
                    member.Visibility.ToString(),
                    property.Parameters
                        .Select(BuildParameter)
                        .ToList(),
                    property.IsDefault),

            DelphiMethodResolution resolution =>
                new SemanticMember(
                    resolution.InterfaceMethod,
                    SemanticMemberKind.MethodResolution,
                    resolution.ImplementationMethod,
                    member.Visibility.ToString(),
                    Array.Empty<SemanticParameter>(),
                    false),

            _ =>
                throw new InvalidOperationException(
                    $"Unsupported Delphi class member: " +
                    member.Node.GetType().Name)
        };
    }

private static SemanticFunction BuildFunction(
    DelphiFunction function)
{
    return new SemanticFunction(
        function.Name,
        function.Parameters
            .Select(BuildParameter)
            .ToList(),
        function.ReturnType,
        function.IsProcedure,
        function.LocalVariables
            .Select(
                variable =>
                    new SemanticVariable(
                        variable.Name,
                        variable.Type))
            .ToList());
}

    private static SemanticParameter BuildParameter(
        DelphiParameter parameter)
    {
        return new SemanticParameter(
            parameter.Name,
            parameter.Type,
            parameter.Kind.ToString());
    }
}


