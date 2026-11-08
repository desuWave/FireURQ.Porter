using System.Text;
using FireURQ.Porter.Analysis;

namespace FireURQ.Porter.Translation;

public sealed class CSharpDeclarationTranslator
{
    private readonly TypeResolver? _resolver;
    private readonly string? _unitName;
    private readonly IReadOnlyList<SemanticUnit> _units;
    private SemanticUnit? _currentUnit;

    public CSharpDeclarationTranslator(
        TypeResolver? resolver = null,
        string? unitName = null,
        IReadOnlyList<SemanticUnit>? units = null)
    {
        _resolver = resolver;
        _unitName = unitName;
        _units = units ?? Array.Empty<SemanticUnit>();
    }

    private string MapType(string? type) =>
        CSharpTypeMapper.Map(type, _resolver, _unitName);

    public string TranslateTypeDeclaration(SemanticType type)
    {
        return TranslateType(type);
    }

    public string Translate(SemanticUnit unit)
    {
        _currentUnit = unit;
        var builder = new StringBuilder();

        foreach (SemanticType type in unit.Types.Where(IsTypeAlias))
            builder.AppendLine(TranslateType(type));

        if (unit.Types.Any(IsTypeAlias))
            builder.AppendLine();

        foreach (SemanticVariable variable in unit.Variables.Where(
            variable => variable.ProcedureType is not null))
        {
            SemanticProcedureType procedure = variable.ProcedureType!;
            string returnType = procedure.IsProcedure
                ? "void"
                : MapType(procedure.ReturnType);

            string parameters = string.Join(", ",
                procedure.Parameters.Select(parameter =>
                    FormatDelegateParameter(parameter, procedure)));

            string? convention = procedure.CallingConvention?
                .Trim().ToLowerInvariant() switch
                {
                    "stdcall" => "StdCall",
                    "cdecl" => "Cdecl",
                    "safecall" => "StdCall",
                    _ => null
                };

            if (!procedure.IsMethodOfObject && convention is not null)
                builder.AppendLine(
                    $"[UnmanagedFunctionPointer(CallingConvention.{convention})]");

            if (!procedure.IsMethodOfObject &&
                convention is not null &&
                !procedure.IsProcedure &&
                IsLongBool(procedure.ReturnType))
                builder.AppendLine(
                    "[return: MarshalAs(UnmanagedType.Bool)]");

            builder.AppendLine(
                $"public delegate {returnType} {variable.Name}Delegate({parameters});");
        }

        if (unit.Variables.Any(variable => variable.ProcedureType is not null))
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
                $"public static {(variable.ProcedureType is null ? MapType(variable.Type) : variable.Name + "Delegate")} {variable.Name};");
        }

        if (unit.Variables.Count > 0)
            builder.AppendLine();

        foreach (SemanticConstant constant in unit.Constants)
        {
            if (TryTranslateBassStreamSentinel(constant, unit, out string sentinel))
            {
                builder.AppendLine(sentinel);
                continue;
            }

            if (TryTranslateBuiltinPiConstant(constant, unit, out string piConstant))
            {
                builder.AppendLine(piConstant);
                continue;
            }

            if (TryTranslateReferencedSetUnion(constant, unit, out string setUnion))
            {
                builder.AppendLine(setUnion);
                continue;
            }

            string sourceType = constant.Type;

            if (string.IsNullOrWhiteSpace(sourceType))
                sourceType = InferReferencedConstantType(
                    constant,
                    unit,
                    new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            SemanticType? setAlias = unit.Types.FirstOrDefault(type =>
                type.Name.Equals(sourceType, StringComparison.OrdinalIgnoreCase) &&
                type.Kind.Equals("Set", StringComparison.OrdinalIgnoreCase));

            if (setAlias is not null)
                sourceType = setAlias.Definition;

            string type = MapType(sourceType);

            if (type == "object" &&
                TryEvaluateLiteralLength(constant.Value.Trim(), out _))
                type = "int";

            SemanticType? recordType = unit.Types.FirstOrDefault(candidate =>
                candidate.Name.Equals(sourceType, StringComparison.OrdinalIgnoreCase) &&
                candidate.Kind.Equals("Record", StringComparison.OrdinalIgnoreCase));
            string value = TranslateConstantValue(
                constant.Value,
                sourceType,
                recordType?.Fields);
            string constantText = constant.Value.Trim();
            bool isRecordInitializer =
                constantText.Length >= 2 &&
                constantText[0] == '(' &&
                constantText[^1] == ')' &&
                constantText.Contains(':');
            bool requiresStaticReadonly =
                System.Text.RegularExpressions.Regex.IsMatch(type, @"\[[,]*\]$") ||
                type.StartsWith("HashSet<", StringComparison.Ordinal) ||
                isRecordInitializer;
            string modifier = requiresStaticReadonly ? "public static readonly" : "public const";
            builder.AppendLine($"{modifier} {type} {constant.Name} = {value};");
        }

        return builder.ToString();
    }

    private bool IsBassStreamCallback(string typeName)
    {
        if (_resolver is null)
            return false;

        TypeResolution resolution = _resolver.Resolve(typeName, _unitName);
        return resolution.IsResolved &&
            !resolution.IsPointer &&
            resolution.ResolvedName.Equals(
                "STREAMPROC", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(
                resolution.DeclaringUnitName, "Dynamic_Bass",
                StringComparison.OrdinalIgnoreCase);
    }

    private bool TryTranslateBassStreamSentinel(
        SemanticConstant constant,
        SemanticUnit unit,
        out string declaration)
    {
        declaration = string.Empty;

        if (!unit.Name.Equals(
                "Dynamic_Bass", StringComparison.OrdinalIgnoreCase) ||
            !IsBassStreamCallback(constant.Type))
            return false;

        SemanticType? callback = unit.Types.FirstOrDefault(type =>
            type.Name.Equals("STREAMPROC", StringComparison.OrdinalIgnoreCase));

        if (callback?.ProcedureType is not { IsMethodOfObject: false } procedure ||
            !string.Equals(
                procedure.CallingConvention?.Trim(), "stdcall",
                StringComparison.OrdinalIgnoreCase))
            return false;

        var match = System.Text.RegularExpressions.Regex.Match(
            constant.Value.Trim(),
            @"^STREAMPROC\s*\(\s*(?<value>0|-\s*1)\s*\)$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (!match.Success)
            return false;

        string value = match.Groups["value"].Value.StartsWith(
            "-", StringComparison.Ordinal)
            ? "new global::System.IntPtr(-1)"
            : "global::System.IntPtr.Zero";

        declaration =
            $"public static readonly global::System.IntPtr {constant.Name} = {value};";
        return true;
    }
    private bool TryTranslateBuiltinPiConstant(
        SemanticConstant constant,
        SemanticUnit unit,
        out string declaration)
    {
        declaration = string.Empty;

        if (_resolver is not null)
        {
            if (_resolver.HasVisibleValueDeclaration("Pi", unit.Name))
                return false;
        }
        else if (
            unit.Constants.Any(value => value.Name.Equals(
                "Pi", StringComparison.OrdinalIgnoreCase)) ||
            unit.Variables.Any(value => value.Name.Equals(
                "Pi", StringComparison.OrdinalIgnoreCase)) ||
            unit.Functions.Any(value => value.Name.Equals(
                "Pi", StringComparison.OrdinalIgnoreCase)) ||
            unit.Types.Any(value => value.Name.Equals(
                "Pi", StringComparison.OrdinalIgnoreCase)) ||
            unit.Classes.Any(value => value.Name.Equals(
                "Pi", StringComparison.OrdinalIgnoreCase)) ||
            unit.Interfaces.Any(value => value.Name.Equals(
                "Pi", StringComparison.OrdinalIgnoreCase)))
            return false;

        if (!string.IsNullOrWhiteSpace(constant.Type) &&
            MapType(constant.Type) != "double")
            return false;

        var tokens = new FireURQ.Porter.Lexer.DelphiLexer(
            constant.Value).Tokenize();
        var parts = new List<string>();
        bool hasPi = false;

        foreach (var token in tokens)
        {
            if (token.Kind == FireURQ.Porter.Lexer.TokenKind.EndOfFile)
                continue;

            if (token.Kind == FireURQ.Porter.Lexer.TokenKind.Identifier &&
                token.Text.Equals("Pi", StringComparison.OrdinalIgnoreCase))
            {
                hasPi = true;
                parts.Add("global::System.Math.PI");
            }
            else if (token.Kind == FireURQ.Porter.Lexer.TokenKind.Number)
            {
                parts.Add(token.Text.StartsWith("$", StringComparison.Ordinal)
                    ? "0x" + token.Text[1..]
                    : token.Text);
            }
            else if (token.Text is "+" or "-" or "*" or "/" or "(" or ")")
            {
                parts.Add(token.Text);
            }
            else
            {
                return false;
            }
        }

        if (!hasPi)
            return false;

        declaration = $"public const double {constant.Name} = " +
            string.Join(" ", parts) + ";";
        return true;
    }
    private bool TryTranslateReferencedSetUnion(
        SemanticConstant constant,
        SemanticUnit unit,
        out string declaration)
    {
        declaration = string.Empty;
        var match = System.Text.RegularExpressions.Regex.Match(
            constant.Value.Trim(),
            @"^(?<name>[A-Za-z_][A-Za-z0-9_]*(?:\s*\.\s*[A-Za-z_][A-Za-z0-9_]*)?)\s*\+\s*(?<set>\[.*\])$",
            System.Text.RegularExpressions.RegexOptions.Singleline);

        if (!match.Success)
            return false;

        string name = match.Groups["name"].Value;
        SemanticConstant? target;
        string? owner;

        if (_resolver is not null)
        {
            if (!_resolver.TryResolveConstant(name, unit.Name, out target, out owner))
                return false;
        }
        else
        {
            var local = unit.Constants.Where(value =>
                value.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (local.Length != 1)
                return false;

            target = local[0];
            owner = unit.Name;
        }

        if (target is null || owner is null ||
            string.IsNullOrWhiteSpace(target.Type))
            return false;

        string type = CSharpTypeMapper.Map(target.Type, _resolver, owner);

        if (!type.StartsWith("HashSet<", StringComparison.Ordinal))
            return false;

        if (!string.IsNullOrWhiteSpace(constant.Type) &&
            CSharpTypeMapper.Map(constant.Type, _resolver, unit.Name) != type)
            return false;

        if (!TryTranslateSetConstant(
                match.Groups["set"].Value, type, out string added))
            return false;

        string reference = $"global::{owner}Module.{target.Name}";
        declaration =
            $"public static readonly {type} {constant.Name} = " +
            $"new {type}(global::System.Linq.Enumerable.Concat({reference}, {added}));";
        return true;
    }
    private string InferReferencedConstantType(
        SemanticConstant constant,
        SemanticUnit unit,
        HashSet<string> visited)
    {
        if (!string.IsNullOrWhiteSpace(constant.Type))
            return constant.Type;

        if (!visited.Add(constant.Name))
            return string.Empty;

        try
        {
            string expression = constant.Value.Trim();

            var cast = System.Text.RegularExpressions.Regex.Match(
                expression,
                @"^(?<type>Int64|UInt64|Integer|Longint|Cardinal|Longword|DWORD|SmallInt|Word|Byte)\s*\(\s*[+-]?\s*(?:\$[0-9A-Fa-f]+|\d+)\s*\)$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (cast.Success)
                return cast.Groups["type"].Value;

            var parts = System.Text.RegularExpressions.Regex.Split(
                expression,
                @"\s+(?:or|and|xor)\s+",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            string? result = null;

            foreach (string part in parts)
            {
                string name = part.Trim();

                if (!System.Text.RegularExpressions.Regex.IsMatch(
                    name, @"^[A-Za-z_][A-Za-z0-9_]*$"))
                    return string.Empty;

                SemanticConstant? target = unit.Constants.FirstOrDefault(
                    candidate => candidate.Name.Equals(
                        name, StringComparison.OrdinalIgnoreCase));

                if (target is null)
                    return string.Empty;

                string targetType = InferReferencedConstantType(
                    target, unit, visited);

                if (string.IsNullOrWhiteSpace(targetType))
                    return string.Empty;

                string mapped = MapType(targetType);

                if (parts.Length > 1 &&
                    mapped is not ("int" or "uint" or "long" or "ulong"))
                    return string.Empty;

                if (result is null)
                    result = targetType;
                else if (MapType(result) != mapped)
                    return string.Empty;
            }

            return result ?? string.Empty;
        }
        finally
        {
            visited.Remove(constant.Name);
        }
    }

    private bool IsTypeAlias(SemanticType type)
    {
        return type.Kind.Equals("Simple", StringComparison.OrdinalIgnoreCase)
            || type.Kind.Equals("Array", StringComparison.OrdinalIgnoreCase)
            || type.Kind.Equals("Pointer", StringComparison.OrdinalIgnoreCase)
            || type.Kind.Equals("Set", StringComparison.OrdinalIgnoreCase)
            || type.Kind.Equals("ClassReference", StringComparison.OrdinalIgnoreCase);
    }

    private string TranslateConstantValue(
        string value,
        string sourceType,
        IReadOnlyList<SemanticField>? recordFields = null)
    {
        string text = value.Trim();
        string mappedType = MapType(sourceType);

        var numericCast = System.Text.RegularExpressions.Regex.Match(
            text,
            @"^(?<type>Int64|UInt64|Integer|Longint|Cardinal|Longword|DWORD|SmallInt|Word|Byte)\s*\(\s*(?<value>[+-]?\s*(?:\$[0-9A-Fa-f]+|\d+))\s*\)$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (numericCast.Success)
        {
            string targetType = numericCast.Groups["type"].Value;
            string operand = TranslateConstantValue(
                numericCast.Groups["value"].Value, targetType);

            return $"unchecked(({MapType(targetType)})({operand}))";
        }

        if (TryEvaluateLiteralLength(text, out int literalLength))
            return literalLength.ToString(
                System.Globalization.CultureInfo.InvariantCulture);
        if (System.Text.RegularExpressions.Regex.IsMatch(mappedType, @"\[[,]*\]$") &&
            TryTranslateArrayConstant(text, mappedType, out string arrayValue))
            return arrayValue;

        if (mappedType.StartsWith("HashSet<", StringComparison.Ordinal) &&
            TryTranslateSetConstant(text, mappedType, out string setValue))
            return setValue;

        if (text.Length >= 2 &&
            text[0] == '(' &&
            text[^1] == ')' &&
            text.Contains(':') &&
            TryTranslateRecordInitializer(text, out string recordInitializer, recordFields))
            return $"new {mappedType} {recordInitializer}";

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

        var tokens = new FireURQ.Porter.Lexer.DelphiLexer(text).Tokenize();
        var translatedTokens = new List<string>();

        foreach (var token in tokens)
        {
            if (token.Kind == FireURQ.Porter.Lexer.TokenKind.EndOfFile)
                continue;

            if (token.Kind == FireURQ.Porter.Lexer.TokenKind.String)
            {
                translatedTokens.Add(TranslateConstantValue(token.Text, "string"));
                continue;
            }

            if (token.Text.StartsWith("$", StringComparison.Ordinal))
            {
                translatedTokens.Add("0x" + token.Text[1..]);
                continue;
            }

            translatedTokens.Add(token.Text.ToLowerInvariant() switch
            {
                "or" => "|",
                "and" => "&",
                "xor" => "^",
                "not" => mappedType == "bool" ? "!" : "~",
                "shl" => "<<",
                "shr" => ">>",
                "div" => "/",
                "mod" => "%",
                "true" => "true",
                "false" => "false",
                _ => token.Text
            });
        }

        return string.Join(" ", translatedTokens);
    }

    private bool TryEvaluateLiteralLength(string text, out int length)
    {
        length = 0;

        var match = System.Text.RegularExpressions.Regex.Match(
            text,
            @"^Length\s*\(\s*'(?<value>(?:[^']|'')*)'\s*\)$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (!match.Success)
            return false;

        length = match.Groups["value"].Value.Replace("''", "'").Length;
        return true;
    }

    private bool TryTranslateArrayConstant(string text, string arrayType, out string result)
    {
        result = string.Empty;
        var match = System.Text.RegularExpressions.Regex.Match(
            arrayType, @"^(?<element>.+)\[(?<commas>,*)\]$");

        if (!match.Success)
            return false;

        string elementType = match.Groups["element"].Value.Trim();
        int rank = match.Groups["commas"].Value.Length + 1;

        if (!TryTranslateArrayLevel(text, elementType, rank, out string initializer))
            return false;

        result = $"new {arrayType} {initializer}";
        return true;
    }

    private bool TryTranslateArrayLevel(
        string text,
        string elementType,
        int rank,
        out string result)
    {
        result = string.Empty;
        text = text.Trim();

        if (text.Length < 2 || text[0] != '(' || text[^1] != ')')
            return false;

        List<string> elements = SplitTopLevel(text[1..^1], ',');
        if (elements.Count == 0)
            return false;

        var translated = new List<string>();

        foreach (string element in elements)
        {
            string item = element.Trim();
            if (item.Length == 0)
                return false;

            if (rank > 1)
            {
                if (!TryTranslateArrayLevel(
                    item, elementType, rank - 1, out string nested))
                    return false;

                translated.Add(nested);
            }
            else if (item.StartsWith("(") &&
                     item.EndsWith(")") &&
                     item.Contains(':'))
            {
                if (!TryTranslateRecordInitializer(item, out string recordValue))
                    return false;

                translated.Add($"new {elementType} {recordValue}");
            }
            else
            {
                translated.Add(TranslateConstantValue(item, elementType));
            }
        }

        result = $"{{ {string.Join(", ", translated)} }}";
        return true;
    }

    private bool TryTranslateRecordInitializer(
        string text,
        out string result,
        IReadOnlyList<SemanticField>? recordFields = null)
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
            string? fieldType = recordFields?
                .FirstOrDefault(field =>
                    field.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                ?.Type;

            string translatedValue = fieldType is not null
                ? TranslateConstantValue(value, fieldType)
                : TranslateUntypedRecordValue(value);

            assignments.Add($"{name} = {translatedValue}");
        }
        result = $"{{ {string.Join(", ", assignments)} }}";
        return assignments.Count > 0;
    }

    private string TranslateUntypedRecordValue(string value)
    {
        string text = value.Trim();

        if (text.StartsWith("'", StringComparison.Ordinal) &&
            text.EndsWith("'", StringComparison.Ordinal) &&
            text.Length >= 2)
        {
            string content = text[1..^1].Replace("''", "'");
            if (content.Length == 1)
                return "'" + EscapeCSharpCharacter(content[0]) + "'";
            return "\"" + EscapeCSharpString(content) + "\"";
        }

        return TranslateConstantValue(text, "int");
    }

    private List<string> SplitTopLevel(string text, char separator)
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
        string remainder = current.ToString().Trim();
        if (remainder.Length > 0)
            parts.Add(remainder);
        return parts;
    }

    private bool TryTranslateSetConstant(string text, string setType, out string result)
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

    private string FormatSetElement(int value, string elementType)
    {
        if (elementType.Equals("byte", StringComparison.OrdinalIgnoreCase))
            return $"(byte){value}";
        if (elementType.Equals("char", StringComparison.OrdinalIgnoreCase))
            return $"(char){value}";
        return value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private bool TryParsePascalSetValue(string text, out int value)
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

    private bool TryTranslateCharacterCodes(string text, string mappedType, out string result)
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

    private string EscapeCSharpCharacter(char value)
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

    private string EscapeCSharpString(string value)
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

    private HashSet<string> GetPendingAbstractMethods(
        SemanticClass owner,
        SemanticUnit? unit,
        HashSet<string> visited)
    {
        var pending = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string context = unit?.Name ?? _unitName ?? string.Empty;
        if (!visited.Add(context + "." + owner.Name))
            return pending;

        if (_resolver is not null &&
            !string.IsNullOrWhiteSpace(owner.BaseType))
        {
            TypeResolution resolution = _resolver.Resolve(owner.BaseType, context);
            if (resolution.IsResolved && !resolution.IsPointer)
            {
                string baseUnitName = resolution.DeclaringUnitName ?? context;
                SemanticUnit? baseUnit = unit is not null &&
                    unit.Name.Equals(baseUnitName, StringComparison.OrdinalIgnoreCase)
                        ? unit
                        : _units.FirstOrDefault(candidate => candidate.Name.Equals(
                            baseUnitName, StringComparison.OrdinalIgnoreCase));

                string baseName = resolution.ResolvedName;
                int separator = baseName.LastIndexOf('.');
                if (separator >= 0)
                    baseName = baseName[(separator + 1)..];

                SemanticClass? baseClass = baseUnit?.Classes.FirstOrDefault(
                    candidate => candidate.Name.Equals(
                        baseName, StringComparison.OrdinalIgnoreCase));

                if (baseClass is not null)
                    pending.UnionWith(GetPendingAbstractMethods(
                        baseClass, baseUnit, visited));
            }
        }

        foreach (SemanticMember member in owner.Members.Where(
            member => member.Kind == SemanticMemberKind.Method &&
                !member.IsClassMethod))
        {
            string signature = GetAbstractSignature(member, context);
            if (member.Modifiers.Contains(
                "abstract", StringComparer.OrdinalIgnoreCase))
                pending.Add(signature);
            else if (member.Modifiers.Contains(
                "override", StringComparer.OrdinalIgnoreCase))
                pending.Remove(signature);
        }

        return pending;
    }

    private string GetAbstractSignature(SemanticMember member, string context)
    {
        string returnType = string.IsNullOrWhiteSpace(member.Type)
            ? "void"
            : CSharpTypeMapper.Map(member.Type, _resolver, context);
        string parameters = string.Join(",", member.Parameters.Select(parameter =>
            parameter.Kind.Trim() + ":" +
            CSharpTypeMapper.Map(parameter.Type, _resolver, context)));
        return member.Name + "(" + parameters + "):" + returnType;
    }

    private string TranslateClass(SemanticClass @class)
    {
        var builder = new StringBuilder();
        string baseType = string.IsNullOrWhiteSpace(@class.BaseType)
            ? " : global::FireURQ.Runtime.TObject"
            : $" : {MapType(@class.BaseType)}";

        bool hasAbstractMethods = GetPendingAbstractMethods(
            @class,
            _currentUnit,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)).Count > 0;
        string abstractModifier = hasAbstractMethods ? "abstract " : string.Empty;
        builder.AppendLine($"public {abstractModifier}partial class {@class.Name}{baseType}");
        builder.AppendLine("{");
        AppendMembers(builder, @class.Members, isInterface: false);
        builder.Append('}');
        return builder.ToString();
    }

    private string TranslateInterface(SemanticInterface @interface)
    {
        var builder = new StringBuilder();
        string baseType = string.IsNullOrWhiteSpace(@interface.BaseType)
            ? (@interface.Name.Equals("IInterface", StringComparison.OrdinalIgnoreCase)
                ? string.Empty
                : " : " + MapType("IInterface"))
            : $" : {MapType(@interface.BaseType)}";

        builder.AppendLine($"public interface {@interface.Name}{baseType}");
        builder.AppendLine("{");
        AppendMembers(builder, @interface.Members, isInterface: true);
        builder.Append('}');
        return builder.ToString();
    }

    private void AppendMembers(StringBuilder builder, IReadOnlyList<SemanticMember> members, bool isInterface)
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
                            $"    {visibility}{MapType(member.Type)} {member.Name};");
                    }
                    break;

                case SemanticMemberKind.Method:
                    // Implementations are emitted into a separate partial class.
                    // Interface methods remain declarations because the interface is a contract.
                    if (isInterface)
                        AppendMethod(builder, member, isInterface: true);
                    else if (member.Modifiers.Contains(
                        "abstract", StringComparer.OrdinalIgnoreCase))
                        AppendAbstractMethod(builder, member);
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

    private void AppendAbstractMethod(StringBuilder builder, SemanticMember member)
    {
        string visibility = MapVisibility(member.Visibility);
        if (visibility == "private ")
            visibility = "protected ";
        string returnType = string.IsNullOrWhiteSpace(member.Type)
            ? "void"
            : MapType(member.Type);
        string parameters = string.Join(", ", member.Parameters.Select(parameter =>
        {
            string modifier = parameter.Kind.Equals(
                "Const", StringComparison.OrdinalIgnoreCase)
                ? "in "
                : MapParameterKind(parameter.Kind);
            return $"{modifier}{MapType(parameter.Type)} {parameter.Name}";
        }));
        builder.AppendLine(
            $"    {visibility}abstract {returnType} {member.Name}({parameters});");
    }

    private void AppendMethod(StringBuilder builder, SemanticMember member, bool isInterface)
    {
        string visibility = isInterface ? string.Empty : MapVisibility(member.Visibility);
        string returnType = MapType(member.Type);
        string parameters = string.Join(", ", member.Parameters.Select(parameter =>
            $"{MapParameterKind(parameter.Kind)}{MapType(parameter.Type)} {parameter.Name}"));
        builder.AppendLine($"    {visibility}{returnType} {member.Name}({parameters});");
    }

    private void AppendProperty(StringBuilder builder, SemanticMember member, IReadOnlyList<SemanticMember> members, bool isInterface)
    {
        string visibility = isInterface ? string.Empty : MapVisibility(member.Visibility);
        string propertyType = MapType(member.Type);

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

    private string BuildPropertyAccessor(string? accessor, IReadOnlyList<SemanticMember> members, bool isInterface, bool isGetter)
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

    private string MapVisibility(string? visibility)
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

    private string MapParameterKind(string? kind)
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

    private bool IsLongBool(string typeName)
    {
        string resolved = _resolver is null
            ? typeName.Trim()
            : _resolver.Resolve(typeName, _unitName).ResolvedName;

        return resolved.Equals(
            "LongBool", StringComparison.OrdinalIgnoreCase);
    }

    private string FormatDelegateParameter(
        SemanticParameter parameter,
        SemanticProcedureType procedure)
    {
        string attribute =
            !procedure.IsMethodOfObject &&
            !string.IsNullOrWhiteSpace(procedure.CallingConvention) &&
            IsLongBool(parameter.Type)
                ? "[MarshalAs(UnmanagedType.Bool)] "
                : string.Empty;

        string parameterType =
            !procedure.IsMethodOfObject &&
            string.Equals(
                procedure.CallingConvention?.Trim(), "stdcall",
                StringComparison.OrdinalIgnoreCase) &&
            IsBassStreamCallback(parameter.Type)
                ? "global::System.IntPtr"
                : MapType(parameter.Type);

        return attribute + MapParameterKind(parameter.Kind) +
            parameterType + " @" + parameter.Name;
    }

    private string MapAliasType(string definition)
    {
        string mapped = MapType(definition);

        mapped = System.Text.RegularExpressions.Regex.Replace(
            mapped,
            @"(?<![\w.:])IntPtr\b",
            "global::System.IntPtr");

        mapped = System.Text.RegularExpressions.Regex.Replace(
            mapped,
            @"(?<![\w.:])HashSet<",
            "global::System.Collections.Generic.HashSet<");

        return mapped;
    }

    private string TranslateType(SemanticType type)
    {
        if (type.ProcedureType is not null)
        {
            SemanticProcedureType procedure = type.ProcedureType;

            string returnType = procedure.IsProcedure
                ? "void"
                : MapType(procedure.ReturnType);

            string parameters = string.Join(", ",
                procedure.Parameters.Select(parameter =>
                    FormatDelegateParameter(parameter, procedure)));

            string? convention = procedure.CallingConvention?
                .Trim().ToLowerInvariant() switch
                {
                    "stdcall" => "StdCall",
                    "cdecl" => "Cdecl",
                    "safecall" => "StdCall",
                    _ => null
                };

            string attribute =
                !procedure.IsMethodOfObject && convention is not null
                    ? $"[UnmanagedFunctionPointer(CallingConvention.{convention})]" +
                      Environment.NewLine
                    : string.Empty;

            if (!procedure.IsMethodOfObject &&
                convention is not null &&
                !procedure.IsProcedure &&
                IsLongBool(procedure.ReturnType))
                attribute += "[return: MarshalAs(UnmanagedType.Bool)]" +
                    Environment.NewLine;

            return attribute +
                $"public delegate {returnType} {type.Name}({parameters});";
        }

        if (type.Kind.Equals("Simple", StringComparison.OrdinalIgnoreCase))
            return $"using {type.Name} = {MapAliasType(type.Definition)};";

        if (type.Kind.Equals("Enum", StringComparison.OrdinalIgnoreCase))
        {
            string[] values = type.Definition.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var builder = new StringBuilder();
            builder.AppendLine($"public enum {type.Name}");
            builder.AppendLine("{");
            for (int i = 0; i < values.Length; i++)
                builder.AppendLine($"    {values[i]}{(i < values.Length - 1 ? "," : string.Empty)}");
            builder.Append('}');
            return builder.ToString();
        }

        if (type.Kind.Equals("Array", StringComparison.OrdinalIgnoreCase))
            return $"using {type.Name} = {MapAliasType(type.Definition)};";

        if (type.Kind.Equals("Pointer", StringComparison.OrdinalIgnoreCase))
            return $"using {type.Name} = global::System.IntPtr;";

        if (type.Kind.Equals("Set", StringComparison.OrdinalIgnoreCase))
            return $"using {type.Name} = {MapAliasType(type.Definition)};";

        if (type.Kind.Equals("ClassReference", StringComparison.OrdinalIgnoreCase))
            return $"using {type.Name} = global::System.Type;";

        if (type.Kind.Equals("Record", StringComparison.OrdinalIgnoreCase))
        {
            if (type.VariantPart is not null)
                return TranslateVariantRecord(type);

            var builder = new StringBuilder();
            builder.AppendLine($"public struct {type.Name}");
            builder.AppendLine("{");
            if (type.Fields is not null)
            {
                foreach (SemanticField field in type.Fields)
                {
                    if (IsLongBool(field.Type))
                        builder.AppendLine(
                            "    [MarshalAs(UnmanagedType.Bool)]");

                    builder.AppendLine(
                        $"    public {MapType(field.Type)} {field.Name};");
                }
            }
            builder.Append('}');
            return builder.ToString();
        }

        return $"// TODO: Delphi type {type.Name} ({type.Kind}): {type.Definition}";
    }

    private string TranslateVariantRecord(SemanticType type)
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

            string mappedType = MapType(fieldType);

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

    private bool IsExplicitLayoutScalar(string mappedType)
    {
        return mappedType is
            "bool" or "byte" or "sbyte" or
            "short" or "ushort" or
            "int" or "uint" or
            "long" or "ulong" or
            "char" or "float" or "double";
    }

    private bool TryGetFixedBuffer(
        string delphiType,
        out string elementType,
        out int length)
    {
        elementType = string.Empty;
        length = 0;

        var match = System.Text.RegularExpressions.Regex.Match(
            delphiType,
            @"^array\s*\[\s*0\s*\.\.\s*(\d+)\s*\]\s*of\s+([A-Za-z_][A-Za-z0-9_]*)$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (!match.Success ||
            !int.TryParse(match.Groups[1].Value, out int upperBound) ||
            upperBound < 0 ||
            upperBound >= 65536)
            return false;

        string mappedElementType =
            MapType(match.Groups[2].Value);

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
