namespace FireURQ.Porter.AST;

public static class DelphiAstPrinter
{
    public static void Print(DelphiUnit unit)
    {
        Console.WriteLine();
        Console.WriteLine("================================");
        Console.WriteLine(" Delphi AST");
        Console.WriteLine("================================");

        Console.WriteLine($"UNIT: {unit.Name}");

        if (unit.Uses.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("USES:");

            foreach (var item in unit.Uses)
                Console.WriteLine($"  {item}");
        }

        Console.WriteLine();

        foreach (var declaration in unit.Declarations)
        {
            PrintDeclaration(
                declaration,
                "  ");
        }

        Console.WriteLine();
    }

    private static void PrintDeclaration(
        DelphiNode node,
        string indent)
    {
        switch (node)
        {
            case DelphiType t:
                Console.WriteLine(
                    $"{indent}TYPE {t.Name}");

                Console.WriteLine(
                    $"{indent}  KIND: {t.Kind}");

                Console.WriteLine(
                    $"{indent}  DEFINITION: {t.Definition}");

                break;

            case DelphiEnum e:
                Console.WriteLine(
                    $"{indent}ENUM {e.Name}");

                foreach (var value in e.Values)
                {
                    Console.WriteLine(
                        $"{indent}  {value}");
                }

                break;

            case DelphiConst c:
                if (string.IsNullOrWhiteSpace(c.Type))
                {
                    Console.WriteLine(
                        $"{indent}CONST {c.Name} = {c.Value}");
                }
                else
                {
                    Console.WriteLine(
                        $"{indent}CONST {c.Name}: {c.Type} = {c.Value}");
                }

                break;

            case DelphiFunction f:
                Console.WriteLine(
                    $"{indent}{(f.IsProcedure ? "PROCEDURE" : "FUNCTION")} {f.Name}");

                foreach (var parameter in f.Parameters)
                {
                    Console.WriteLine(
                        $"{indent}  PARAM {parameter.Name}: {parameter.Type}");
                }

                if (!string.IsNullOrWhiteSpace(f.ReturnType))
                {
                    Console.WriteLine(
                        $"{indent}  RETURN: {f.ReturnType}");
                }

                break;

            default:
                Console.WriteLine(
                    $"{indent}[UNKNOWN NODE] {node.GetType().Name}");

                break;
        }
    }
}