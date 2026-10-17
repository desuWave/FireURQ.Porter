namespace FireURQ.Porter.Analysis;

public sealed record SemanticUnit(
    string Name,
    IReadOnlyList<string> Uses,
    IReadOnlyList<SemanticClass> Classes,
    IReadOnlyList<SemanticInterface> Interfaces,
    IReadOnlyList<SemanticType> Types,
    IReadOnlyList<SemanticConstant> Constants,
    IReadOnlyList<SemanticVariable> Variables,
    IReadOnlyList<SemanticFunction> Functions
);

public sealed record SemanticClass(
    string Name,
    string? BaseType,
    IReadOnlyList<SemanticMember> Members
);

public sealed record SemanticInterface(
    string Name,
    string? BaseType,
    IReadOnlyList<SemanticMember> Members
);

public enum SemanticMemberKind
{
    Field,
    Method,
    Property,
    MethodResolution
}

public sealed record SemanticMember(
    string Name,
    SemanticMemberKind Kind,
    string Type,
    string? Visibility,
    IReadOnlyList<SemanticParameter> Parameters,
    bool IsDefault,
    string? ReadAccessor = null,
    string? WriteAccessor = null
);

public sealed record SemanticField(
    string Name,
    string Type
);

public sealed record SemanticVariant(
    IReadOnlyList<string> Labels,
    IReadOnlyList<SemanticField> Fields
);

public sealed record SemanticVariantPart(
    string? TagFieldName,
    string TagType,
    IReadOnlyList<SemanticVariant> Variants
);

public sealed record SemanticType(
    string Name,
    string Kind,
    string Definition,
    IReadOnlyList<SemanticField>? Fields = null,
    string? UnitName = null,
    SemanticVariantPart? VariantPart = null
);

public sealed record SemanticConstant(
    string Name,
    string Type,
    string Value
);

public sealed record SemanticVariable(
    string Name,
    string Type,
    SemanticProcedureType? ProcedureType = null
);

public sealed record SemanticProcedureType(
    IReadOnlyList<SemanticParameter> Parameters,
    string ReturnType,
    bool IsProcedure,
    bool IsMethodOfObject,
    string? CallingConvention
);

public sealed record SemanticFunction(
    string Name,
    IReadOnlyList<SemanticParameter> Parameters,
    string ReturnType,
    bool IsProcedure,
    IReadOnlyList<SemanticVariable> LocalVariables
);

public sealed record SemanticParameter(
    string Name,
    string Type,
    string Kind
);
