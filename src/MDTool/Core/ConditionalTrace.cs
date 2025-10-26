namespace MDTool.Core;

/// <summary>
/// Trace of conditional evaluation for debugging and testing.
/// Contains information about all conditional blocks and which branches were taken.
/// </summary>
public class ConditionalTrace
{
    /// <summary>
    /// List of all conditional blocks in the document.
    /// </summary>
    public List<ConditionalBlockTrace> Blocks { get; init; } = new();
}

/// <summary>
/// Trace information for a single conditional block ({{#if}} ... {{/if}}).
/// </summary>
public class ConditionalBlockTrace
{
    /// <summary>
    /// Starting line number of the block (1-indexed).
    /// </summary>
    public int StartLine { get; init; }

    /// <summary>
    /// Ending line number of the block (1-indexed).
    /// </summary>
    public int EndLine { get; init; }

    /// <summary>
    /// List of branches in this block (if, else-if, else).
    /// </summary>
    public List<ConditionalBranchTrace> Branches { get; init; } = new();
}

/// <summary>
/// Trace information for a single branch within a conditional block.
/// </summary>
public class ConditionalBranchTrace
{
    /// <summary>
    /// Kind of branch: "if", "else-if", or "else".
    /// </summary>
    public string Kind { get; init; } = "";

    /// <summary>
    /// Expression for this branch (null for else branches).
    /// </summary>
    public string? Expr { get; init; }

    /// <summary>
    /// Whether this branch was taken (true) or skipped (false).
    /// </summary>
    public bool Taken { get; init; }
}
