namespace FireURQ.Porter.Translation;

public static class CSharpTypeMapper
{
public static string Map(string? delphiType)
{
    if (string.IsNullOrWhiteSpace(delphiType))
    return "object";

    string type = delphiType.Trim();

    // Delphi array declarations:
    // array [0 .. MAX_PATH] of Char -> char[]
    // array [1 .. 2] of TPlaceArea -> TPlaceArea[]
    // array of Integer -> int[]
    if (type.StartsWith("array", StringComparison.OrdinalIgnoreCase))
    {
        int ofIndex = type.IndexOf(
            "of",
            StringComparison.OrdinalIgnoreCase);

        if (ofIndex >= 0)
        {
            string elementType = type[(ofIndex + 2)..].Trim();

            return $"{Map(elementType)}[]";
        }
    }

    return type.ToLowerInvariant() switch
    {
        "integer" => "int",
        "int" => "int",
        "boolean" => "bool",
        "bool" => "bool",
        "string" => "string",
        "real" => "double",
        "double" => "double",
        "single" => "float",
        "char" => "char",
        "byte" => "byte",
        "word" => "ushort",
        "cardinal" => "uint",
        "longword" => "uint",
        "int64" => "long",
        "longint" => "int",
        "shortint" => "sbyte",
        _ => type
    };
}
}
