using FireURQ.Porter.Analysis;

namespace FireURQ.Porter.Translation;

public static class CSharpTypeMapper
{
    public static string Map(string? delphiType) =>
        Map(delphiType, null, null);

    public static string Map(
        string? delphiType,
        TypeResolver? resolver,
        string? unitName)
    {
        if (string.IsNullOrWhiteSpace(delphiType))
            return "object";

        string type = delphiType.Trim();

        if (resolver is not null)
        {
            TypeResolution resolution = resolver.Resolve(type, unitName);

            if (resolution.HasCycle || resolution.IsAmbiguous)
            {
                string reason = resolution.HasCycle
                    ? "cyclic alias"
                    : "ambiguous declaration";

                throw new InvalidOperationException(
                    $"Cannot map Delphi type '{type}' in unit " +
                    $"'{unitName ?? "<unspecified>"}': {reason}.");
            }

            if (resolution.IsPointer)
                return "IntPtr";

            type = resolution.ResolvedName;

            if (resolver.TryGetCompositeDefinition(
                type,
                resolution.DeclaringUnitName ?? unitName,
                out string definition,
                out string? declaringUnit))
            {
                type = definition;
                unitName = declaringUnit;
            }
        }

        if (type.StartsWith("set of ", StringComparison.OrdinalIgnoreCase))
            return $"HashSet<{Map(type[7..], resolver, unitName)}>";

        var arrayMatch = System.Text.RegularExpressions.Regex.Match(
            type,
            @"^array\s*(?:\[[^\]]*\]\s*)?of\s+(.+)$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (arrayMatch.Success)
        {
            string bounds = type.Contains('[')
                ? type[(type.IndexOf('[') + 1)..type.IndexOf(']')]
                : string.Empty;

            int rank = 1;
            int depth = 0;

            foreach (char character in bounds)
            {
                if (character == '(') depth++;
                else if (character == ')') depth--;
                else if (character == ',' && depth == 0) rank++;
            }

            string suffix = "[" + new string(',', rank - 1) + "]";
            return Map(arrayMatch.Groups[1].Value, resolver, unitName) + suffix;
        }

        // The context-free API also supports standard Delphi pointers.
        if (resolver is null &&
            type.ToLowerInvariant() is
                "pointer" or "pchar" or "pansichar" or "pwidechar" or
                "pbyte" or "plongword" or "pguid")
            return "IntPtr";

        return type.ToLowerInvariant() switch
        {
            "integer" or "int" => "int",
            "boolean" or "bool" or "longbool" => "bool",
            "string" => "string",
            "real" or "double" or "tdatetime" => "double",
            "single" => "float",
            "char" => "char",
            "byte" => "byte",
            "word" => "ushort",
            "cardinal" or "longword" or "dword" => "uint",
            "smallint" => "short",
            "int64" => "long",
            "uint64" => "ulong",
            "longint" => "int",
            "shortint" => "sbyte",
            "hwnd" or "thandle" => "IntPtr",
            "hresult" => "int",
            "tguid" => "global::System.Guid",
            _ => type
        };
    }
}
