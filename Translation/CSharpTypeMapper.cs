using FireURQ.Porter.Analysis;

namespace FireURQ.Porter.Translation;

public static class CSharpTypeMapper
{
    private static string? GetRuntimeListTypeName(string type) =>
        type.ToLowerInvariant() switch
        {
            "tlist" => "TList",
            "tobjectlist" => "TObjectList",
            "ijclstringlist" => "IJclStringList",
            "iinterface" => "IInterface",
            "iinterfacelist" => "IInterfaceList",
            "tinterfacelist" => "TInterfaceList",
            "ansistring" => "AnsiString",
            "tstrings" => "TStrings",
            "tstringlist" => "TStringList",
            "tstringlistsortcompare" => "TStringListSortCompare",
            _ => null
        };

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

            // Preserve a project-defined class, interface or composite type.
            if (resolution.DeclaringUnitName is not null &&
                GetRuntimeListTypeName(type) is not null)
                return type;

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

        string? runtimeListType = GetRuntimeListTypeName(type);
        if (runtimeListType is not null)
            return runtimeListType;

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
            "hwnd" or "thandle" or "hmodule" or "lparam" or "lresult" => "IntPtr",
            "wparam" => "UIntPtr",
            "uint" => "uint",
            "hresult" => "int",
            "tguid" => "global::System.Guid",
            _ => type
        };
    }
}
