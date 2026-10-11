using System.Text;
using FireURQ.Porter.Analysis;

namespace FireURQ.Porter.Translation;

public sealed class CSharpDeclarationTranslator
{
public string Translate(SemanticUnit unit)
{
var builder = new StringBuilder();
    foreach (SemanticVariable variable in unit.Variables)
    {
        builder.AppendLine(
            $"{CSharpTypeMapper.Map(variable.Type)} {variable.Name};");
    }

    if (unit.Variables.Count > 0)
        builder.AppendLine();

    foreach (SemanticConstant constant in unit.Constants)
    {
        builder.AppendLine(
            $"const {CSharpTypeMapper.Map(constant.Type)} " +
            $"{constant.Name} = {TranslateConstantValue(constant.Value)};");
    }

    if (unit.Constants.Count > 0)
        builder.AppendLine();

    foreach (SemanticType type in unit.Types)
    {
        builder.AppendLine(TranslateType(type));
        builder.AppendLine();
    }

    foreach (SemanticClass @class in unit.Classes)
    {
        builder.AppendLine(TranslateClass(@class));
        builder.AppendLine();
    }

    foreach (SemanticInterface @interface in unit.Interfaces)
    {
        builder.AppendLine(TranslateInterface(@interface));
        builder.AppendLine();
    }

    return builder.ToString();
}

private static string TranslateConstantValue(string value)
{
    string text = value.Trim();

    if (text.StartsWith("'") && text.EndsWith("'") && text.Length >= 2)
    {
        string content = text[1..^1].Replace("''", "'");

        content = content
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n")
            .Replace("\t", "\\t");

        return $"\"{content}\"";
    }

    if (text.Equals("true", StringComparison.OrdinalIgnoreCase) ||
        text.Equals("false", StringComparison.OrdinalIgnoreCase))
    {
        return text.ToLowerInvariant();
    }

    if (text.Equals("nil", StringComparison.OrdinalIgnoreCase))
    {
        return "null";
    }

    return text;
}

private static string TranslateClass(SemanticClass @class)
{
    var builder = new StringBuilder();

    string baseType = string.IsNullOrWhiteSpace(@class.BaseType)
        ? string.Empty
        : $" : {CSharpTypeMapper.Map(@class.BaseType)}";

    builder.AppendLine($"public class {@class.Name}{baseType}");
    builder.AppendLine("{");

    AppendMembers(builder, @class.Members, isInterface: false);

    builder.Append('}');
    return builder.ToString();
}

private static string TranslateInterface(SemanticInterface @interface)
{
    var builder = new StringBuilder();

    string baseType = string.IsNullOrWhiteSpace(@interface.BaseType)
        ? string.Empty
        : $" : {CSharpTypeMapper.Map(@interface.BaseType)}";

    builder.AppendLine($"public interface {@interface.Name}{baseType}");
    builder.AppendLine("{");

    AppendMembers(builder, @interface.Members, isInterface: true);

    builder.Append('}');
    return builder.ToString();
}

private static void AppendMembers(
    StringBuilder builder,
    IReadOnlyList<SemanticMember> members,
    bool isInterface)
{
    foreach (SemanticMember member in members)
    {
        switch (member.Kind)
        {
            case SemanticMemberKind.Field:
                if (!isInterface)
                {
                    string visibility = MapVisibility(member.Visibility);
                    builder.AppendLine(
                        $"    {visibility}{CSharpTypeMapper.Map(member.Type)} " +
                        $"{member.Name};");
                }
                break;

            case SemanticMemberKind.Method:
                AppendMethod(builder, member, isInterface);
                break;

            case SemanticMemberKind.Property:
                AppendProperty(builder, member, members, isInterface);
                break;

            case SemanticMemberKind.MethodResolution:
                builder.AppendLine(
                    $"    // TODO: method resolution {member.Name}");
                break;
        }
    }
}

private static void AppendMethod(
    StringBuilder builder,
    SemanticMember member,
    bool isInterface)
{
    string visibility = isInterface
        ? string.Empty
        : MapVisibility(member.Visibility);

    string returnType = CSharpTypeMapper.Map(member.Type);
    string parameters = string.Join(
        ", ",
        member.Parameters.Select(parameter =>
            $"{MapParameterKind(parameter.Kind)}" +
            $"{CSharpTypeMapper.Map(parameter.Type)} {parameter.Name}"));

    builder.AppendLine(
        $"    {visibility}{returnType} {member.Name}({parameters});");
}

private static void AppendProperty(
    StringBuilder builder,
    SemanticMember member,
    IReadOnlyList<SemanticMember> members,
    bool isInterface)
{
    string visibility = isInterface
        ? string.Empty
        : MapVisibility(member.Visibility);

    string propertyType = CSharpTypeMapper.Map(member.Type);

    string? readAccessor = member.ReadAccessor;
    string? writeAccessor = member.WriteAccessor;

    string getter = BuildPropertyAccessor(
        readAccessor,
        members,
        isInterface,
        isGetter: true);

    string setter = BuildPropertyAccessor(
        writeAccessor,
        members,
        isInterface,
        isGetter: false);

    string accessors = string.Join(
        " ",
        new[] { getter, setter }
            .Where(accessor => !string.IsNullOrWhiteSpace(accessor)));

    if (string.IsNullOrWhiteSpace(accessors))
    {
        builder.AppendLine(
            $"    // TODO: property {member.Name} has no read/write accessor");
        return;
    }

    builder.AppendLine(
        $"    {visibility}{propertyType} {member.Name} {{ {accessors} }}");
}

private static string BuildPropertyAccessor(
    string? accessor,
    IReadOnlyList<SemanticMember> members,
    bool isInterface,
    bool isGetter)
{
    if (string.IsNullOrWhiteSpace(accessor))
        return string.Empty;

    string name = accessor.Trim();

    SemanticMember? target = members.FirstOrDefault(
        candidate =>
            candidate.Name.Equals(
                name,
                StringComparison.OrdinalIgnoreCase) &&
            candidate.Kind is SemanticMemberKind.Field
                or SemanticMemberKind.Method);

    if (isInterface)
    {
        // Интерфейс задаёт контракт, а не реализацию аксессоров.
        return isGetter ? "get;" : "set;";
    }

    if (target is null)
    {
        return isGetter
            ? $"get => throw new NotImplementedException(/* {name} */);"
            : $"set => throw new NotImplementedException(/* {name} */);";
    }

    if (target.Kind == SemanticMemberKind.Field)
    {
        return isGetter
            ? $"get => {name};"
            : $"set => {name} = value;";
    }

    string parameters = isGetter
        ? string.Join(
            ", ",
            target.Parameters.Select(parameter => parameter.Name))
        : string.Join(
            ", ",
            target.Parameters.Select(parameter => parameter.Name)
                .Append("value"));

    return isGetter
        ? $"get => {name}({parameters});"
        : $"set => {name}({parameters});";
}

private static string MapVisibility(string? visibility)
{
    if (string.IsNullOrWhiteSpace(visibility))
        return "public ";

    return visibility.Trim().ToLowerInvariant() switch
    {
        "private" => "private ",
        "protected" => "protected ",
        "public" => "public ",
        "published" => "public ",
        "strict private" => "private ",
        "strict protected" => "protected ",
        _ => "public "
    };
}

private static string MapParameterKind(string? kind)
{
    if (string.IsNullOrWhiteSpace(kind))
        return string.Empty;

    return kind.Trim().ToLowerInvariant() switch
    {
        "var" => "ref ",
        "out" => "out ",
        "const" => string.Empty,
        "value" => string.Empty,
        _ => string.Empty
    };
}

private static string TranslateType(SemanticType type)
{
    if (type.Kind.Equals(
            "Simple",
            StringComparison.OrdinalIgnoreCase))
    {
        return $"using {type.Name} = {CSharpTypeMapper.Map(type.Definition)};";
    }

    if (type.Kind.Equals(
            "Enum",
            StringComparison.OrdinalIgnoreCase))
    {
        string[] values = type.Definition.Split(
            ',',
            StringSplitOptions.RemoveEmptyEntries |
            StringSplitOptions.TrimEntries);

        var builder = new StringBuilder();
        builder.AppendLine($"enum {type.Name}");
        builder.AppendLine("{");

        for (int i = 0; i < values.Length; i++)
        {
            string suffix = i < values.Length - 1 ? "," : string.Empty;
            builder.AppendLine($"    {values[i]}{suffix}");
        }

        builder.Append('}');
        return builder.ToString();
    }

    if (type.Kind.Equals(
            "Array",
            StringComparison.OrdinalIgnoreCase))
    {
        string definition = type.Definition.Trim();

        int ofIndex = definition.IndexOf(
            "of",
            StringComparison.OrdinalIgnoreCase);

        if (ofIndex >= 0)
        {
            string elementType = definition[(ofIndex + 2)..].Trim();

            return $"using {type.Name} = " +
                   $"{CSharpTypeMapper.Map(elementType)}[];";
        }
    }

    if (type.Kind.Equals(
            "Pointer",
            StringComparison.OrdinalIgnoreCase))
    {
        return $"using {type.Name} = IntPtr;";
    }

    if (type.Kind.Equals(
            "Set",
            StringComparison.OrdinalIgnoreCase))
    {
        return $"using {type.Name} = HashSet<int>;";
    }

    if (type.Kind.Equals(
            "ClassReference",
            StringComparison.OrdinalIgnoreCase))
    {
        return $"using {type.Name} = Type;";
    }

    if (type.Kind.Equals(
            "Record",
            StringComparison.OrdinalIgnoreCase))
    {
        var builder = new StringBuilder();
        builder.AppendLine($"struct {type.Name}");
        builder.AppendLine("{");

        if (type.Fields is not null)
        {
            foreach (SemanticField field in type.Fields)
            {
                builder.AppendLine(
                    $"    {CSharpTypeMapper.Map(field.Type)} {field.Name};");
            }
        }

        builder.Append('}');
        return builder.ToString();
    }

    return $"// TODO: Delphi type {type.Name} ({type.Kind}): {type.Definition}";
}
}
