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
    Array,
    Pointer,
    Unknown
}

public sealed record DelphiField(
    string Name,
    string Type
) : DelphiNode;

public enum DelphiClassMemberKind
{
    Field,
    Method,
    Property
}

public sealed record DelphiClassMember(
    DelphiClassMemberKind Kind,
    DelphiNode Node
) : DelphiNode;

public sealed record DelphiClass(
    string Name,
    string? BaseType,
    IReadOnlyList<DelphiClassMember> Members
) : DelphiNode;

public enum DelphiVisibility
{
    Private,
    Protected,
    Public,
    Published,
    Automated
}

public sealed record DelphiType(
    string Name,
    DelphiTypeKind Kind,
    string Definition,
    IReadOnlyList<DelphiField>? Fields = null
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

public enum DelphiParameterKind
{
    Value,
    Const,
    Var,
    Out
}

public sealed record DelphiParameter(
    string Name,
    string Type,
    DelphiParameterKind Kind
) : DelphiNode;

public sealed record DelphiFunction(
    string Name,
    IReadOnlyList<DelphiParameter> Parameters,
    string ReturnType,
    bool IsProcedure
) : DelphiNode;
