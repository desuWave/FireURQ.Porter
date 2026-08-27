namespace FireURQ.Porter.Analysis;

public static class SemanticPrinter
{
    public static void Print(SemanticUnit unit)
    {
        Console.WriteLine();
        Console.WriteLine("================================");
        Console.WriteLine(" Semantic Model");
        Console.WriteLine("================================");

        Console.WriteLine(
            $"UNIT: {unit.Name}");

        Console.WriteLine();
        Console.WriteLine(
            $"Classes:    {unit.Classes.Count}");

        Console.WriteLine(
            $"Interfaces: {unit.Interfaces.Count}");

        Console.WriteLine(
            $"Types:      {unit.Types.Count}");

        Console.WriteLine(
            $"Constants:  {unit.Constants.Count}");

        Console.WriteLine(
            $"Functions:  {unit.Functions.Count}");

        foreach (var value in unit.Classes)
        {
            Console.WriteLine();
            Console.WriteLine(
                $"CLASS {value.Name}");

            if (!string.IsNullOrWhiteSpace(value.BaseType))
            {
                Console.WriteLine(
                    $"  BASE: {value.BaseType}");
            }

            PrintMembers(value.Members);
        }

        foreach (var value in unit.Interfaces)
        {
            Console.WriteLine();
            Console.WriteLine(
                $"INTERFACE {value.Name}");

            if (!string.IsNullOrWhiteSpace(value.BaseType))
            {
                Console.WriteLine(
                    $"  BASE: {value.BaseType}");
            }

            PrintMembers(value.Members);
        }

        if (unit.Types.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("TYPES");

            foreach (var type in unit.Types)
            {
                Console.WriteLine(
                    $"  {type.Name}: {type.Kind}");

                if (!string.IsNullOrWhiteSpace(type.Definition))
                {
                    Console.WriteLine(
                        $"    {type.Definition}");
                }
            }
        }

        if (unit.Constants.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("CONSTANTS");

            foreach (var constant in unit.Constants)
            {
                Console.WriteLine(
                    $"  {constant.Name}: {constant.Type} = {constant.Value}");
            }
        }

        if (unit.Functions.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("FUNCTIONS");

            foreach (var function in unit.Functions)
            {
                PrintFunction(
                    function,
                    "  ");
            }
        }

        Console.WriteLine();
    }

    private static void PrintMembers(
        IReadOnlyList<SemanticMember> members)
    {
        foreach (var member in members)
        {
            Console.WriteLine(
                $"  {member.Visibility?.ToUpperInvariant()} " +
                $"{member.Kind.ToString().ToUpperInvariant()} " +
                $"{member.Name}");

            if (member.Kind == SemanticMemberKind.Property &&
                    member.IsDefault)
            {
                Console.WriteLine(
                    "    DEFAULT: true");
            }

            if (!string.IsNullOrWhiteSpace(member.Type))
            {
                Console.WriteLine(
                    $"    TYPE: {member.Type}");
            }

            foreach (var parameter in member.Parameters)
            {
                Console.WriteLine(
                    $"    PARAM " +
                    $"{parameter.Kind.ToUpperInvariant()} " +
                    $"{parameter.Name}: {parameter.Type}");
            }
        }
    }


    private static void PrintFunction(
        SemanticFunction function,
        string indent)
    {
        Console.WriteLine(
            $"{indent}{(function.IsProcedure ? "PROCEDURE" : "FUNCTION")} " +
            $"{function.Name}");

        foreach (var parameter in function.Parameters)
        {
            Console.WriteLine(
                $"{indent}  PARAM " +
                $"{parameter.Kind.ToUpperInvariant()} " +
                $"{parameter.Name}: {parameter.Type}");
        }

        if (!function.IsProcedure &&
            !string.IsNullOrWhiteSpace(function.ReturnType))
        {
            Console.WriteLine(
                $"{indent}  RETURN: {function.ReturnType}");
        }
    }
}