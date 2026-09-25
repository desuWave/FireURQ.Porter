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

    if (implementation is null)
        return null;

    return semanticUnit.Functions.FirstOrDefault(
        candidate =>
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
                implementation.LocalVariables));
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
        for (int i = 0; i < semanticParameters.Count; i++)
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