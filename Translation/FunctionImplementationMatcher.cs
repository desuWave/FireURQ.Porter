using FireURQ.Porter.AST;
using FireURQ.Porter.Analysis;

namespace FireURQ.Porter.Translation;

public static class FunctionImplementationMatcher
{
public static SemanticFunction? Match(
    DelphiUnit unit,
    BodyTokenAnalyzer.BodyInfo body,
    SemanticUnit semanticUnit)
{
    DelphiFunction? implementation =
        FindImplementation(
            unit,
            body);

    if (body.OwnerKind ==
        BodyTokenAnalyzer.BodyOwnerKind.TopLevel)
    {
        return semanticUnit.Functions.FirstOrDefault(
            candidate =>
                FunctionMatches(
                    candidate,
                    implementation!));
    }

    int separator =
        implementation?.Name.LastIndexOf('.') ?? -1;

    if (separator < 1)
        return null;

    string ownerName =
        implementation!.Name[..separator];

    string methodName =
        implementation.Name[(separator + 1)..];

    SemanticMember? member =
        FindMethodMember(
            semanticUnit,
            body.OwnerKind,
            ownerName,
            methodName,
            implementation);

    if (member is null)
        return null;

    return CreateSemanticFunction(
        member,
        implementation);
}

    private static DelphiFunction? FindImplementation(
        DelphiUnit unit,
        BodyTokenAnalyzer.BodyInfo body)
    {
        foreach (DelphiNode declaration in unit.Declarations)
        {
            if (declaration is not DelphiFunction function)
                continue;

            if (function.BodyTokens.Count == 0)
                continue;

            if (!function.Name.Equals(
                    body.Name,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (function.IsProcedure != body.IsProcedure)
                continue;

            return function;
        }

        return null;
    }

    private static SemanticMember? FindMethodMember(
        SemanticUnit semanticUnit,
        BodyTokenAnalyzer.BodyOwnerKind ownerKind,
        string ownerName,
        string methodName,
        DelphiFunction implementation)
    {
        IEnumerable<SemanticMember> members;

        if (ownerKind ==
            BodyTokenAnalyzer.BodyOwnerKind.Class)
        {
            SemanticClass? @class =
                semanticUnit.Classes.FirstOrDefault(
                    value =>
                        value.Name.Equals(
                            ownerName,
                            StringComparison.OrdinalIgnoreCase));

            if (@class is null)
                return null;

            members = @class.Members;
        }
        else if (ownerKind ==
                 BodyTokenAnalyzer.BodyOwnerKind.Interface)
        {
            SemanticInterface? @interface =
                semanticUnit.Interfaces.FirstOrDefault(
                    value =>
                        value.Name.Equals(
                            ownerName,
                            StringComparison.OrdinalIgnoreCase));

            if (@interface is null)
                return null;

            members = @interface.Members;
        }
        else
        {
            return null;
        }

        foreach (SemanticMember member in members)
        {
            if (member.Kind !=
                SemanticMemberKind.Method)
            {
                continue;
            }

            if (!member.Name.Equals(
                    methodName,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (member.Parameters.Count !=
                implementation.Parameters.Count)
            {
                continue;
            }

            if (!ParametersMatch(
                    member.Parameters,
                    implementation.Parameters))
            {
                continue;
            }

            bool memberIsProcedure =
                string.IsNullOrWhiteSpace(member.Type);

            if (memberIsProcedure !=
                implementation.IsProcedure)
            {
                continue;
            }

            if (!memberIsProcedure &&
                !member.Type.Equals(
                    implementation.ReturnType,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return member;
        }

        return null;
    }

    private static SemanticFunction? CreateSemanticFunction(
        SemanticMember member,
        DelphiFunction implementation)
    {
        return new SemanticFunction(
            member.Name,
            member.Parameters,
            member.Type,
            implementation.IsProcedure,
            implementation.LocalVariables
                .Select(
                    variable =>
                        new SemanticVariable(
                            variable.Name,
                            variable.Type))
                .ToList());
    }

    private static bool FunctionMatches(
        SemanticFunction candidate,
        DelphiFunction implementation)
    {
        return
            candidate.Name.Equals(
                implementation.Name,
                StringComparison.OrdinalIgnoreCase) &&
            candidate.IsProcedure ==
                implementation.IsProcedure &&
            candidate.Parameters.Count ==
                implementation.Parameters.Count &&
            candidate.ReturnType.Equals(
                implementation.ReturnType,
                StringComparison.OrdinalIgnoreCase) &&
            ParametersMatch(
                candidate.Parameters,
                implementation.Parameters) &&
            candidate.LocalVariables.Count ==
                implementation.LocalVariables.Count &&
            LocalVariablesMatch(
                candidate.LocalVariables,
                implementation.LocalVariables);
    }

    private static bool LocalVariablesMatch(
        IReadOnlyList<SemanticVariable> semanticVariables,
        IReadOnlyList<DelphiVariable> delphiVariables)
    {
        if (semanticVariables.Count !=
            delphiVariables.Count)
        {
            return false;
        }

        for (int i = 0;
             i < semanticVariables.Count;
             i++)
        {
            SemanticVariable semantic =
                semanticVariables[i];

            DelphiVariable delphi =
                delphiVariables[i];

            if (!semantic.Name.Equals(
                    delphi.Name,
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!semantic.Type.Equals(
                    delphi.Type,
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ParametersMatch(
        IReadOnlyList<SemanticParameter> semanticParameters,
        IReadOnlyList<DelphiParameter> delphiParameters)
    {
        if (semanticParameters.Count !=
            delphiParameters.Count)
        {
            return false;
        }

        for (int i = 0;
             i < semanticParameters.Count;
             i++)
        {
            SemanticParameter semantic =
                semanticParameters[i];

            DelphiParameter delphi =
                delphiParameters[i];

            if (!semantic.Name.Equals(
                    delphi.Name,
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!semantic.Type.Equals(
                    delphi.Type,
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!semantic.Kind.Equals(
                    delphi.Kind.ToString(),
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }
}

