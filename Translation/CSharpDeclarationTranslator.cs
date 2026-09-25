using System.Text;
using FireURQ.Porter.Analysis;

namespace FireURQ.Porter.Translation;

public sealed class CSharpDeclarationTranslator
{
    public string Translate(
        SemanticUnit unit)
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
                $"{constant.Name} = {constant.Value};");
        }

        if (unit.Constants.Count > 0)
            builder.AppendLine();

        foreach (SemanticType type in unit.Types)
        {
            builder.AppendLine(
                TranslateType(type));
        }

        return builder.ToString();
    }

private static string TranslateType(
    SemanticType type)
{
    if (type.Kind.Equals(
            "Simple",
            StringComparison.OrdinalIgnoreCase))
    {
        return
            $"using {type.Name} = " +
            $"{CSharpTypeMapper.Map(type.Definition)};";
    }

    if (type.Kind.Equals(
            "Enum",
            StringComparison.OrdinalIgnoreCase))
    {
        string[] values =
            type.Definition
                .Split(
                    ',',
                    StringSplitOptions.RemoveEmptyEntries |
                    StringSplitOptions.TrimEntries);

        var builder =
            new StringBuilder();

        builder.AppendLine(
            $"enum {type.Name}");
        builder.AppendLine("{");

        for (int i = 0; i < values.Length; i++)
        {
            string suffix =
                i < values.Length - 1
                    ? ","
                    : "";

            builder.AppendLine(
                $"    {values[i]}{suffix}");
        }

        builder.Append("}");

        return builder.ToString();
    }

if (type.Kind.Equals(
        "Array",
        StringComparison.OrdinalIgnoreCase))
{
    string definition =
        type.Definition.Trim();

    int ofIndex =
        definition.IndexOf(
            "of",
            StringComparison.OrdinalIgnoreCase);

    if (ofIndex >= 0)
    {
        string elementType =
            definition[(ofIndex + 2)..]
                .Trim();

        return
            $"using {type.Name} = " +
            $"{CSharpTypeMapper.Map(elementType)}[];";
    }
}

if (type.Kind.Equals(
        "Record",
        StringComparison.OrdinalIgnoreCase))
{
    var builder =
        new StringBuilder();

    builder.AppendLine(
        $"struct {type.Name}");

    builder.AppendLine("{");

    if (type.Fields is not null)
    {
        foreach (SemanticField field in type.Fields)
        {
            builder.AppendLine(
                $"    {CSharpTypeMapper.Map(field.Type)} " +
                $"{field.Name};");
        }
    }

    builder.Append("}");

    return builder.ToString();
}

    return
        $"// TODO: Delphi type {type.Name} " +
        $"({type.Kind}): {type.Definition}";
    }
}