namespace FireURQ.Porter.Analysis;

public sealed record SemanticUnit(
    string Name,
    IReadOnlyList<string> Uses,
    IReadOnlyList<SemanticClass> Classes,
    IReadOnlyList<SemanticInterface> Interfaces,
    IReadOnlyList<SemanticType> Types,
    IReadOnlyList<SemanticConstant> Constants,
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
    bool IsDefault
);

public sealed record SemanticType(
    string Name,
    string Kind,
    string Definition
);

public sealed record SemanticConstant(
    string Name,
    string Type,
    string Value
);

public sealed record SemanticFunction(
    string Name,
    IReadOnlyList<SemanticParameter> Parameters,
    string ReturnType,
    bool IsProcedure
);

public sealed record SemanticParameter(
    string Name,
    string Type,
    string Kind
);
