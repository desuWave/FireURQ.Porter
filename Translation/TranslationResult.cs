namespace FireURQ.Porter.Translation;

public sealed record TranslationResult(
    string SourceFile,
    string OutputFile,
    bool Success,
    int FunctionsTranslated,
    int TodoCount,
    string? Error
);
