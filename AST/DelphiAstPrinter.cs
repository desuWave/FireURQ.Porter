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
            case DelphiClass c:
                Console.WriteLine(
                    $"{indent}CLASS {c.Name}");

                if (!string.IsNullOrWhiteSpace(c.BaseType))
                {
                    Console.WriteLine(
                        $"{indent}  BASE: {c.BaseType}");
                }

                DelphiVisibility? currentVisibility = null;

                foreach (var member in c.Members)
                {
                    if (currentVisibility != member.Visibility)
                    {
                        currentVisibility = member.Visibility;

                        Console.WriteLine();
                        Console.WriteLine(
                            $"{indent}  {member.Visibility.ToString().ToUpperInvariant()}");
                    }

                    switch (member.Node)
                    {
                        case DelphiField field:
                            Console.WriteLine(
                                $"{indent}    FIELD {field.Name}: {field.Type}");
                            break;

                        case DelphiFunction function:
                            Console.WriteLine(
                                $"{indent}    {(function.IsProcedure ? "PROCEDURE" : "FUNCTION")} {function.Name}");

                            foreach (var parameter in function.Parameters)
                            {
                                Console.WriteLine(
                                    $"{indent}      PARAM {parameter.Name}: {parameter.Type}");
                            }

                            if (!function.IsProcedure &&
                                !string.IsNullOrWhiteSpace(function.ReturnType))
                            {
                                Console.WriteLine(
                                    $"{indent}      RETURN: {function.ReturnType}");
                            }

                            break;

                        case DelphiProperty property:
                            Console.WriteLine(
                                $"{indent}    PROPERTY {property.Name}: {property.Type}");

                            if (!string.IsNullOrWhiteSpace(property.ReadAccessor))
                            {
                                Console.WriteLine(
                                    $"{indent}      READ: {property.ReadAccessor}");
                            }

                            if (!string.IsNullOrWhiteSpace(property.WriteAccessor))
                            {
                                Console.WriteLine(
                                    $"{indent}      WRITE: {property.WriteAccessor}");
                            }

                            if (property.IsDefault)
                            {
                                Console.WriteLine(
                                    $"{indent}      DEFAULT");
                            }

                            break;

                        case DelphiMethodResolution resolution:
                            Console.WriteLine(
                                $"{indent}    METHOD RESOLUTION {resolution.InterfaceMethod} = {resolution.ImplementationMethod}");
                            break;

                        default:
                            Console.WriteLine(
                                $"{indent}    [{member.Kind}] {member.Node.GetType().Name}");
                            break;
                    }
                }

                break;

            case DelphiInterface value:
                Console.WriteLine(
                    $"{indent}INTERFACE {value.Name}");

                if (!string.IsNullOrEmpty(value.BaseType))
                {
                    Console.WriteLine(
                        $"{indent}  BASE: {value.BaseType}");
                }

                foreach (var member in value.Members)
                {
                    PrintInterfaceMember(member);
                }

                Console.WriteLine();

                break;

            case DelphiConst value:
                Console.WriteLine(
                    $"{indent}CONST {value.Name}");

                Console.WriteLine(
                    $"{indent}  VALUE: {value.Value}");

                break;

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

                foreach (var enumValue in e.Values)
                {
                    Console.WriteLine(
                        $"{indent}  {enumValue}");
                }

                break;

            case DelphiField f:
                Console.WriteLine(
                    $"{indent}FIELD {f.Name}: {f.Type}");

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

            case DelphiProperty p:
                Console.WriteLine(
                    $"{indent}PROPERTY {p.Name}: {p.Type}");

                if (!string.IsNullOrWhiteSpace(p.ReadAccessor))
                {
                    Console.WriteLine(
                        $"{indent}  READ: {p.ReadAccessor}");
                }

                if (!string.IsNullOrWhiteSpace(p.WriteAccessor))
                {
                    Console.WriteLine(
                        $"{indent}  WRITE: {p.WriteAccessor}");
                }

                if (p.IsDefault)
                {
                    Console.WriteLine(
                        $"{indent}  DEFAULT");
                }

                break;

            case DelphiMethodResolution resolution:
                Console.WriteLine(
                    $"{indent}METHOD RESOLUTION {resolution.InterfaceMethod} = {resolution.ImplementationMethod}");

                break;

            default:
                Console.WriteLine(
                    $"{indent}[UNKNOWN NODE] {node.GetType().Name}");

                break;
        }
    }

    private static void PrintInterfaceMember(
        DelphiClassMember member)
    {
        switch (member.Node)
        {
            case DelphiFunction function:
                Console.WriteLine(
                    $"    {(function.IsProcedure ? "PROCEDURE" : "FUNCTION")} {function.Name}");

                foreach (var parameter in function.Parameters)
                {
                    Console.WriteLine(
                        $"      PARAM {parameter.Name}: {parameter.Type}");
                }

                if (!function.IsProcedure)
                {
                    Console.WriteLine(
                        $"      RETURN: {function.ReturnType}");
                }

                break;

            case DelphiProperty property:
                Console.WriteLine(
                    $"    PROPERTY {property.Name}: {property.Type}");

                if (!string.IsNullOrWhiteSpace(property.ReadAccessor))
                {
                    Console.WriteLine(
                        $"      READ: {property.ReadAccessor}");
                }

                if (!string.IsNullOrWhiteSpace(property.WriteAccessor))
                {
                    Console.WriteLine(
                        $"      WRITE: {property.WriteAccessor}");
                }

                if (property.IsDefault)
                {
                    Console.WriteLine(
                        $"      DEFAULT");
                }

                break;

            case DelphiMethodResolution resolution:
                Console.WriteLine(
                    $"    METHOD RESOLUTION {resolution.InterfaceMethod} = {resolution.ImplementationMethod}");

                break;

            default:
                Console.WriteLine(
                    $"    [{member.Kind}] {member.Node.GetType().Name}");

                break;
        }
    }
}