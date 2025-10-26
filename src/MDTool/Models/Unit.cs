namespace MDTool.Models;

/// <summary>
/// Represents a void/unit type for operations that don't return a meaningful value.
/// Used with ProcessingResult&lt;Unit&gt; for void operations.
/// </summary>
public readonly struct Unit
{
    /// <summary>
    /// The singleton instance of Unit.
    /// </summary>
    public static readonly Unit Value = new();
}
