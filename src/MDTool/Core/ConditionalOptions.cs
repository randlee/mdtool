namespace MDTool.Core;

/// <summary>
/// Options for controlling conditional evaluation behavior.
/// </summary>
/// <param name="Strict">If true, unknown variables in expressions cause errors; default is false (unknown vars evaluate as false)</param>
/// <param name="CaseSensitiveStrings">If true, string comparisons are case-sensitive; default is false (case-insensitive)</param>
/// <param name="MaxNesting">Maximum allowed nesting depth for conditional blocks; default is 10</param>
public record ConditionalOptions(
    bool Strict = false,
    bool CaseSensitiveStrings = false,
    int MaxNesting = 10
);
