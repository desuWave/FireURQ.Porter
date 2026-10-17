using FireURQ.Porter.Lexer;

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
    Interface,
    Procedure,
    Array,
    Pointer,
    ClassReference,
    Unknown
}

public sealed record DelphiField(
    string Name,
    string Type
) : DelphiNode;

public sealed record DelphiVariant(
    IReadOnlyList<string> Labels,
    IReadOnlyList<DelphiField> Fields
) : DelphiNode;

public sealed record DelphiVariantPart(
    string? TagFieldName,
    string TagType,
    IReadOnlyList<DelphiVariant> Variants
) : DelphiNode;

public sealed record DelphiVariable(
    string Name,
    string Type,
    DelphiProcedureType? ProcedureType = null
) : DelphiNode;

public sealed record DelphiProperty(
    string Name,
    string Type,
    IReadOnlyList<DelphiParameter> Parameters,
    string? ReadAccessor,
    string? WriteAccessor,
    bool IsDefault
) : DelphiNode;

public enum DelphiClassMemberKind
{
    Field,
    Method,
    Property,
    MethodResolution
}

public sealed record DelphiClassMember(
    DelphiVisibility Visibility,
    DelphiClassMemberKind Kind,
    DelphiNode Node
) : DelphiNode;

public sealed record DelphiClass(
    string Name,
    string? BaseType,
    IReadOnlyList<DelphiClassMember> Members
) : DelphiNode;

public sealed record DelphiInterface(
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
    IReadOnlyList<DelphiField>? Fields = null,
    DelphiVariantPart? VariantPart = null
) : DelphiNode;

public sealed record DelphiProcedureType(
    string Name,
    IReadOnlyList<DelphiParameter> Parameters,
    string ReturnType,
    bool IsProcedure,
    bool IsMethodOfObject,
    string? CallingConvention
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
    DelphiParameterKind Kind,
    string? DefaultValue
) : DelphiNode;

public sealed record DelphiFunction(
    string Name,
    IReadOnlyList<DelphiParameter> Parameters,
    string ReturnType,
    bool IsProcedure,
    IReadOnlyList<DelphiVariable> LocalVariables,
    IReadOnlyList<LexerToken> BodyTokens
) : DelphiNode;

public sealed record DelphiMethodResolution(
    string InterfaceMethod,
    string ImplementationMethod
) : DelphiNode;