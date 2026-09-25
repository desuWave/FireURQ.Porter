using FireURQ.Porter.AST;

namespace FireURQ.Porter.Analysis;

public static class SemanticAnalyzer
{
    public static SemanticUnit Analyze(DelphiUnit unit)
    {
        var classes = new List<SemanticClass>();
        var interfaces = new List<SemanticInterface>();
        var types = new List<SemanticType>();
        var constants = new List<SemanticConstant>();
        var variables = new List<SemanticVariable>();
        var functions = new List<SemanticFunction>();

        foreach (var declaration in unit.Declarations)
        {
            switch (declaration)
            {
                case DelphiClass value:
                    classes.Add(AnalyzeClass(value));
                    break;

                case DelphiInterface value:
                    interfaces.Add(AnalyzeInterface(value));
                    break;

case DelphiType value:
    types.Add(
        new SemanticType(
            value.Name,
            value.Kind.ToString(),
            value.Definition,
            value.Fields?
                .Select(
                    field =>
                        new SemanticField(
                            field.Name,
                            field.Type))
                .ToList()));
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
                    functions.Add(
                        AnalyzeFunction(value));
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

    private static SemanticClass AnalyzeClass(
        DelphiClass value)
    {
        var members =
            value.Members
                .Select(AnalyzeMember)
                .ToList();

        return new SemanticClass(
            value.Name,
            value.BaseType,
            members);
    }

    private static SemanticInterface AnalyzeInterface(
        DelphiInterface value)
    {
        var members =
            value.Members
                .Select(AnalyzeMember)
                .ToList();

        return new SemanticInterface(
            value.Name,
            value.BaseType,
            members);
    }

    private static SemanticMember AnalyzeMember(
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
                        .Select(parameter =>
                            new SemanticParameter(
                                parameter.Name,
                                parameter.Type,
                                parameter.Kind.ToString()))
                        .ToList(),
                        false),

            DelphiProperty property =>
                new SemanticMember(
                    property.Name,
                    SemanticMemberKind.Property,
                    property.Type,
                    member.Visibility.ToString(),
                    property.Parameters
                        .Select(parameter =>
                            new SemanticParameter(
                                parameter.Name,
                                parameter.Type,
                                parameter.Kind.ToString()))
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

    private static SemanticFunction AnalyzeFunction(
        DelphiFunction function)
    {
        var parameters =
            function.Parameters
                .Select(parameter =>
                    new SemanticParameter(
                        parameter.Name,
                        parameter.Type,
                        parameter.Kind.ToString()))
                .ToList();

var localVariables =
    function.LocalVariables
        .Select(variable =>
            new SemanticVariable(
                variable.Name,
                variable.Type))
        .ToList();

return new SemanticFunction(
    function.Name,
    parameters,
    function.ReturnType,
    function.IsProcedure,
    localVariables);
    }
}
