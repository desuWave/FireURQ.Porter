namespace FireURQ.Porter.Translation;

public static class CSharpTypeMapper
{
    public static string Map(string? delphiType)
    {
        if (string.IsNullOrWhiteSpace(delphiType))
            return "void";

        string type = delphiType.Trim();

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
