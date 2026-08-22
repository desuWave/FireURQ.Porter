namespace FireURQ.Porter.AST;

public abstract record DelphiNode;

public sealed record DelphiUnit(
    string Name,
    IReadOnlyList<string> Uses,
    IReadOnlyList<DelphiNode> Declarations
) : DelphiNode;

public enum DelphiTypeKind
{
    Simple,
    Enum,
    Set,
    Class,
    Record,
    Procedure,
    Unknown
}

public sealed record DelphiType(
    string Name,
    DelphiTypeKind Kind,
    string Definition
) : DelphiNode;

public sealed record DelphiEnum(
    string Name,
    IReadOnlyList<string> Values
) : DelphiNode;

public sealed record DelphiConst(
    string Name,
    string Type,
    string Value
) : DelphiNode;

public sealed record DelphiParameter(
    string Name,
    string Type,
    bool IsConst
) : DelphiNode;

public sealed record DelphiFunction(
    string Name,
    IReadOnlyList<DelphiParameter> Parameters,
    string ReturnType,
    bool IsProcedure
) : DelphiNode;