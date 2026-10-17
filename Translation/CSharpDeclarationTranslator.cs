using System.Text;
using FireURQ.Porter.Analysis;

namespace FireURQ.Porter.Translation;

public sealed class CSharpDeclarationTranslator
{
    public string Translate(SemanticUnit unit)
    {
        var builder = new StringBuilder();

        foreach (SemanticType type in unit.Types.Where(IsTypeAlias))
            builder.AppendLine(TranslateType(type));

        if (unit.Types.Any(IsTypeAlias))
            builder.AppendLine();

        foreach (SemanticType type in unit.Types.Where(type => !IsTypeAlias(type)))
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

    public string TranslateModuleMembers(SemanticUnit unit)
    {
        var builder = new StringBuilder();

        foreach (SemanticVariable variable in unit.Variables)
        {
            builder.AppendLine(
                $"public static {CSharpTypeMapper.Map(variable.Type)} {variable.Name};");
        }

        if (unit.Variables.Count > 0)
            builder.AppendLine();

        foreach (SemanticConstant constant in unit.Constants)
        {
            string sourceType = constant.Type;
            SemanticType? setAlias = unit.Types.FirstOrDefault(type =>
                type.Name.Equals(sourceType, StringComparison.OrdinalIgnoreCase) &&
                type.Kind.Equals("Set", StringComparison.OrdinalIgnoreCase));

            if (setAlias is not null)
                sourceType = setAlias.Definition;

            string type = CSharpTypeMapper.Map(sourceType);
            string value = TranslateConstantValue(constant.Value, sourceType);
            bool requiresStaticReadonly =
                type.EndsWith("[]", StringComparison.Ordinal) ||
                type.StartsWith("HashSet<", StringComparison.Ordinal);
            string modifier = requiresStaticReadonly ? "public static readonly" : "public const";
            builder.AppendLine($"{modifier} {type} {constant.Name} = {value};");
        }

        return builder.ToString();
    }

    private static bool IsTypeAlias(SemanticType type)
    {
        return type.Kind.Equals("Simple", StringComparison.OrdinalIgnoreCase)
            || type.Kind.Equals("Array", StringComparison.OrdinalIgnoreCase)
            || type.Kind.Equals("Pointer", StringComparison.OrdinalIgnoreCase)
            || type.Kind.Equals("Set", StringComparison.OrdinalIgnoreCase)
            || type.Kind.Equals("ClassReference", StringComparison.OrdinalIgnoreCase);
    }

    private static string TranslateConstantValue(string value, string sourceType)
    {
        string text = value.Trim();
        string mappedType = CSharpTypeMapper.Map(sourceType);
        if (mappedType.EndsWith("[]", StringComparison.Ordinal) &&
            TryTranslateArrayConstant(text, mappedType, out string arrayValue))
            return arrayValue;

        if (mappedType.StartsWith("HashSet<", StringComparison.Ordinal) &&
            TryTranslateSetConstant(text, mappedType, out string setValue))
            return setValue;

        if (text.StartsWith("$", StringComparison.Ordinal))
        {
            string hexadecimal = text[1..];
            if (hexadecimal.Length > 0 && hexadecimal.All(Uri.IsHexDigit))
                return "0x" + hexadecimal;
        }

        if (TryTranslateCharacterCodes(text, mappedType, out string characterCodeValue))
            return characterCodeValue;

        if (text.StartsWith("'") && text.EndsWith("'") && text.Length >= 2)
        {
            string content = text[1..^1].Replace("''", "'");
            if (mappedType == "char" && content.Length == 1)
                return "'" + EscapeCSharpCharacter(content[0]) + "'";
            return "\"" + EscapeCSharpString(content) + "\"";
        }

        if (text.Equals("true", StringComparison.OrdinalIgnoreCase) ||
            text.Equals("false", StringComparison.OrdinalIgnoreCase))
            return text.ToLowerInvariant();
        if (text.Equals("nil", StringComparison.OrdinalIgnoreCase))
            return "null";
        return text;
    }

    private static bool TryTranslateArrayConstant(string text, string arrayType, out string result)
    {
        result = string.Empty;
        if (text.Length < 2 || text[0] != '(' || text[^1] != ')') return false;
        string elementType = arrayType[..^2].Trim();
        List<string> elements = SplitTopLevel(text[1..^1], ',');
        if (elements.Count == 0) return false;

        var translated = new List<string>();
        foreach (string element in elements)
        {
            string item = element.Trim();
            if (item.Length == 0) return false;
            if (item.StartsWith("(") && item.EndsWith(")") && item.Contains(':'))
            {
                if (!TryTranslateRecordInitializer(item, out string recordValue)) return false;
                translated.Add($"new {elementType} {recordValue}");
            }
            else
                translated.Add(TranslateConstantValue(item, elementType));
        }
        result = $"new {arrayType} {{ {string.Join(", ", translated)} }}";
        return true;
    }

    private static bool TryTranslateRecordInitializer(string text, out string result)
    {
        result = string.Empty;
        if (text.Length < 2 || text[0] != '(' || text[^1] != ')') return false;
        var assignments = new List<string>();
        foreach (string field in SplitTopLevel(text[1..^1], ';'))
        {
            List<string> pair = SplitTopLevel(field, ':');
            if (pair.Count < 2) return false;
            string name = pair[0].Trim();
            string value = string.Join(":", pair.Skip(1)).Trim();
            if (name.Length == 0 || value.Length == 0) return false;
            assignments.Add($"{name} = {TranslateConstantValue(value, "string")}");
        }
        result = $"{{ {string.Join(", ", assignments)} }}";
        return assignments.Count > 0;
    }

    private static List<string> SplitTopLevel(string text, char separator)
    {
        var parts = new List<string>();
        int roundDepth = 0, squareDepth = 0;
        bool inString = false;
        var current = new StringBuilder();
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '\'')
            {
                current.Append(c);
                if (inString && i + 1 < text.Length && text[i + 1] == '\'')
                {
                    current.Append(text[++i]);
                    continue;
                }
                inString = !inString;
                continue;
            }
            if (!inString)
            {
                if (c == '(') roundDepth++;
                else if (c == ')') roundDepth--;
                else if (c == '[') squareDepth++;
                else if (c == ']') squareDepth--;
                if (c == separator && roundDepth == 0 && squareDepth == 0)
                {
                    parts.Add(current.ToString().Trim());
                    current.Clear();
                    continue;
                }
            }
            current.Append(c);
        }
        if (current.Length > 0) parts.Add(current.ToString().Trim());
        return parts;
    }

    private static bool TryTranslateSetConstant(string text, string setType, out string result)
    {
        result = string.Empty;
        if (text.Length < 2 || text[0] != '[' || text[^1] != ']')
            return false;

        int genericStart = setType.IndexOf('<');
        int genericEnd = setType.LastIndexOf('>');
        if (genericStart < 0 || genericEnd <= genericStart)
            return false;

        string elementType = setType[(genericStart + 1)..genericEnd].Trim();
        List<string> elements = SplitTopLevel(text[1..^1], ',');
        var values = new List<int>();

        var translatedElements = new List<string>();

        foreach (string rawElement in elements)
        {
            string element = rawElement.Trim();
            if (element.Length == 0)
                return false;

            List<string> range = SplitTopLevel(element, '.');
            if (range.Count == 3 && range[1].Length == 0)
            {
                if (!TryParsePascalSetValue(range[0], out int first) ||
                    !TryParsePascalSetValue(range[2], out int last) ||
                    last < first || last - first > 4096)
                    return false;

                for (int value = first; value <= last; value++)
                    translatedElements.Add(FormatSetElement(value, elementType));

                continue;
            }

            if (TryParsePascalSetValue(element, out int scalar))
            {
                translatedElements.Add(FormatSetElement(scalar, elementType));
                continue;
            }

            // Pascal set literals may refer to previously declared character constants,
            // e.g. [' ', chTAB, #0]. Preserve a simple identifier as a C# expression.
            if (System.Text.RegularExpressions.Regex.IsMatch(element, @"^[A-Za-z_][A-Za-z0-9_]*$"))
            {
                translatedElements.Add(element);
                continue;
            }

            return false;
        }

        result = $"new {setType} {{ {string.Join(", ", translatedElements)} }}";
        return true;
    }

    private static string FormatSetElement(int value, string elementType)
    {
        if (elementType.Equals("byte", StringComparison.OrdinalIgnoreCase))
            return $"(byte){value}";
        if (elementType.Equals("char", StringComparison.OrdinalIgnoreCase))
            return $"(char){value}";
        return value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private static bool TryParsePascalSetValue(string text, out int value)
    {
        string token = text.Trim();
        value = 0;

        if (token.Length >= 3 && token[0] == '\'' && token[^1] == '\'')
        {
            string character = token[1..^1].Replace("''", "'");
            if (character.Length != 1)
                return false;

            value = character[0];
            return true;
        }

        if (token.StartsWith("#", StringComparison.Ordinal))
            return int.TryParse(token[1..], System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out value);

        if (token.StartsWith("$", StringComparison.Ordinal))
            return int.TryParse(token[1..], System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture, out value);

        return int.TryParse(token, System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture, out value);
    }

    private static bool TryTranslateCharacterCodes(string text, string mappedType, out string result)
    {
        result = string.Empty;
        if (text.Length == 0 || text[0] != '#')
            return false;

        var characters = new List<char>();
        int position = 0;

        while (position < text.Length)
        {
            while (position < text.Length && char.IsWhiteSpace(text[position]))
                position++;

            if (position >= text.Length || text[position] != '#')
                return false;

            position++;
            int digitStart = position;

            while (position < text.Length && char.IsDigit(text[position]))
                position++;

            if (digitStart == position ||
                !int.TryParse(text[digitStart..position], out int code) ||
                code < 0 || code > char.MaxValue)
                return false;

            characters.Add((char)code);
        }

        if (characters.Count == 0)
            return false;

        if (mappedType == "char")
        {
            if (characters.Count != 1)
                return false;
            result = "'" + EscapeCSharpCharacter(characters[0]) + "'";
            return true;
        }

        result = "\"" + EscapeCSharpString(new string(characters.ToArray())) + "\"";
        return true;
    }

    private static string EscapeCSharpCharacter(char value)
    {
        return value switch
        {
            '\\' => "\\\\",
            '\'' => "\\'",
            '\0' => "\\0",
            '\a' => "\\a",
            '\b' => "\\b",
            '\f' => "\\f",
            '\n' => "\\n",
            '\r' => "\\r",
            '\t' => "\\t",
            '\v' => "\\v",
            _ when char.IsControl(value) => "\\u" + ((int)value).ToString("X4"),
            _ => value.ToString()
        };
    }

    private static string EscapeCSharpString(string value)
    {
        var builder = new StringBuilder();
        foreach (char character in value)
        {
            builder.Append(character switch
            {
                '\\' => "\\\\",
                '"' => "\\\"",
                '\0' => "\\0",
                '\a' => "\\a",
                '\b' => "\\b",
                '\f' => "\\f",
                '\n' => "\\n",
                '\r' => "\\r",
                '\t' => "\\t",
                '\v' => "\\v",
                _ when char.IsControl(character) => "\\u" + ((int)character).ToString("X4"),
                _ => character.ToString()
            });
        }
        return builder.ToString();
    }

    private static string TranslateClass(SemanticClass @class)
    {
        var builder = new StringBuilder();
        string baseType = string.IsNullOrWhiteSpace(@class.BaseType)
            ? string.Empty
            : $" : {CSharpTypeMapper.Map(@class.BaseType)}";

        builder.AppendLine($"public partial class {@class.Name}{baseType}");
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

    private static void AppendMembers(StringBuilder builder, IReadOnlyList<SemanticMember> members, bool isInterface)
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
                            $"    {visibility}{CSharpTypeMapper.Map(member.Type)} {member.Name};");
                    }
                    break;

                case SemanticMemberKind.Method:
                    // Implementations are emitted into a separate partial class.
                    // Interface methods remain declarations because the interface is a contract.
                    if (isInterface)
                        AppendMethod(builder, member, isInterface: true);
                    break;

                case SemanticMemberKind.Property:
                    AppendProperty(builder, member, members, isInterface);
                    break;

                case SemanticMemberKind.MethodResolution:
                    builder.AppendLine($"    // TODO: method resolution {member.Name}");
                    break;
            }
        }
    }

    private static void AppendMethod(StringBuilder builder, SemanticMember member, bool isInterface)
    {
        string visibility = isInterface ? string.Empty : MapVisibility(member.Visibility);
        string returnType = CSharpTypeMapper.Map(member.Type);
        string parameters = string.Join(", ", member.Parameters.Select(parameter =>
            $"{MapParameterKind(parameter.Kind)}{CSharpTypeMapper.Map(parameter.Type)} {parameter.Name}"));
        builder.AppendLine($"    {visibility}{returnType} {member.Name}({parameters});");
    }

    private static void AppendProperty(StringBuilder builder, SemanticMember member, IReadOnlyList<SemanticMember> members, bool isInterface)
    {
        string visibility = isInterface ? string.Empty : MapVisibility(member.Visibility);
        string propertyType = CSharpTypeMapper.Map(member.Type);

        string getter = BuildPropertyAccessor(member.ReadAccessor, members, isInterface, true);
        string setter = BuildPropertyAccessor(member.WriteAccessor, members, isInterface, false);
        string accessors = string.Join(" ", new[] { getter, setter }.Where(x => !string.IsNullOrWhiteSpace(x)));

        if (string.IsNullOrWhiteSpace(accessors))
        {
            builder.AppendLine($"    // TODO: property {member.Name} has no read/write accessor");
            return;
        }

        builder.AppendLine($"    {visibility}{propertyType} {member.Name} {{ {accessors} }}");
    }

    private static string BuildPropertyAccessor(string? accessor, IReadOnlyList<SemanticMember> members, bool isInterface, bool isGetter)
    {
        if (string.IsNullOrWhiteSpace(accessor))
            return string.Empty;

        string name = accessor.Trim();
        SemanticMember? target = members.FirstOrDefault(candidate =>
            candidate.Name.Equals(name, StringComparison.OrdinalIgnoreCase) &&
            candidate.Kind is SemanticMemberKind.Field or SemanticMemberKind.Method);

        if (isInterface)
            return isGetter ? "get;" : "set;";

        if (target is null)
            return isGetter
                ? $"get => throw new NotImplementedException(/* {name} */);"
                : $"set => throw new NotImplementedException(/* {name} */);";

        if (target.Kind == SemanticMemberKind.Field)
            return isGetter ? $"get => {name};" : $"set => {name} = value;";

        string parameters = isGetter
            ? string.Join(", ", target.Parameters.Select(parameter => parameter.Name))
            : string.Join(", ", target.Parameters.Select(parameter => parameter.Name).Append("value"));

        return isGetter ? $"get => {name}({parameters});" : $"set => {name}({parameters});";
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
        if (type.Kind.Equals("Simple", StringComparison.OrdinalIgnoreCase))
            return $"using {type.Name} = {CSharpTypeMapper.Map(type.Definition)};";

        if (type.Kind.Equals("Enum", StringComparison.OrdinalIgnoreCase))
        {
            string[] values = type.Definition.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var builder = new StringBuilder();
            builder.AppendLine($"enum {type.Name}");
            builder.AppendLine("{");
            for (int i = 0; i < values.Length; i++)
                builder.AppendLine($"    {values[i]}{(i < values.Length - 1 ? "," : string.Empty)}");
            builder.Append('}');
            return builder.ToString();
        }

        if (type.Kind.Equals("Array", StringComparison.OrdinalIgnoreCase))
        {
            string definition = type.Definition.Trim();
            int ofIndex = definition.IndexOf("of", StringComparison.OrdinalIgnoreCase);
            if (ofIndex >= 0)
            {
                string elementType = definition[(ofIndex + 2)..].Trim();
                return $"using {type.Name} = {CSharpTypeMapper.Map(elementType)}[];";
            }
        }

        if (type.Kind.Equals("Pointer", StringComparison.OrdinalIgnoreCase))
            return $"using {type.Name} = IntPtr;";

        if (type.Kind.Equals("Set", StringComparison.OrdinalIgnoreCase))
            return $"using {type.Name} = {CSharpTypeMapper.Map(type.Definition)};";

        if (type.Kind.Equals("ClassReference", StringComparison.OrdinalIgnoreCase))
            return $"using {type.Name} = Type;";

        if (type.Kind.Equals("Record", StringComparison.OrdinalIgnoreCase))
        {
            if (type.VariantPart is not null)
                return TranslateVariantRecord(type);

            var builder = new StringBuilder();
            builder.AppendLine($"struct {type.Name}");
            builder.AppendLine("{");
            if (type.Fields is not null)
            {
                foreach (SemanticField field in type.Fields)
                    builder.AppendLine($"    public {CSharpTypeMapper.Map(field.Type)} {field.Name};");
            }
            builder.Append('}');
            return builder.ToString();
        }

        return $"// TODO: Delphi type {type.Name} ({type.Kind}): {type.Definition}";
    }

    private static string TranslateVariantRecord(SemanticType type)
    {
        SemanticVariantPart variantPart = type.VariantPart!;

        // Variant fields overlay each other at the start of a Pascal record.
        // This initial implementation deliberately supports records whose
        // entire storage is the variant part; mixed common/variant fields
        // need target-specific field-offset calculation.
        if (type.Fields is { Count: > 0 })
            return $"// TODO: Variant record {type.Name} has common fields; explicit layout is not yet supported.";

        var fields = variantPart.Variants
            .SelectMany(variant => variant.Fields)
            .ToList();

        if (fields.Count == 0)
            return $"// TODO: Variant record {type.Name} has no variant fields.";

        var duplicateNames = fields
            .GroupBy(field => field.Name, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        if (duplicateNames.Count > 0)
            return $"// TODO: Variant record {type.Name} repeats field names across alternatives: {string.Join(", ", duplicateNames)}.";

        var declarations = new List<string>();

        foreach (SemanticField field in fields)
        {
            string fieldType = field.Type.Trim();

            if (TryGetFixedBuffer(fieldType, out string elementType, out int length))
            {
                declarations.Add(
                    $"    [FieldOffset(0)] public fixed {elementType} {field.Name}[{length}];");
                continue;
            }

            string mappedType = CSharpTypeMapper.Map(fieldType);

            if (!IsExplicitLayoutScalar(mappedType))
            {
                return $"// TODO: Variant record {type.Name} contains unsupported overlay field type '{field.Type}' ({field.Name}).";
            }

            declarations.Add(
                $"    [FieldOffset(0)] public {mappedType} {field.Name};");
        }

        var builder = new StringBuilder();
        builder.AppendLine("[StructLayout(LayoutKind.Explicit)]");
        builder.AppendLine($"public unsafe struct {type.Name}");
        builder.AppendLine("{");

        foreach (string declaration in declarations)
            builder.AppendLine(declaration);

        builder.Append('}');
        return builder.ToString();
    }

    private static bool IsExplicitLayoutScalar(string mappedType)
    {
        return mappedType is
            "bool" or "byte" or "sbyte" or
            "short" or "ushort" or
            "int" or "uint" or
            "long" or "ulong" or
            "char" or "float" or "double";
    }

    private static bool TryGetFixedBuffer(
        string delphiType,
        out string elementType,
        out int length)
    {
        elementType = string.Empty;
        length = 0;

        var match = System.Text.RegularExpressions.Regex.Match(
            delphiType,
            @"^array\\s*\\[\\s*0\\s*\\.\\.\\s*(\\d+)\\s*\\]\\s*of\\s+([A-Za-z_][A-Za-z0-9_]*)$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (!match.Success ||
            !int.TryParse(match.Groups[1].Value, out int upperBound) ||
            upperBound < 0 ||
            upperBound >= 65536)
            return false;

        string mappedElementType =
            CSharpTypeMapper.Map(match.Groups[2].Value);

        if (!IsExplicitLayoutScalar(mappedElementType) ||
            mappedElementType == "bool" ||
            mappedElementType == "float" ||
            mappedElementType == "double")
            return false;

        elementType = mappedElementType;
        length = upperBound + 1;
        return true;
    }
}
