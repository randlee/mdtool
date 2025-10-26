namespace MDTool.Core;

/// <summary>
/// Provides case-insensitive, dot-path access to argument values.
/// Used for resolving variables in conditional expressions.
/// </summary>
public interface IArgsAccessor
{
    /// <summary>
    /// Attempts to retrieve a value by path (e.g., "ROLE" or "USER.NAME").
    /// Key matching is case-insensitive.
    /// </summary>
    /// <param name="path">Variable path (simple or dot-separated)</param>
    /// <param name="value">Retrieved value if found</param>
    /// <returns>True if value was found, false otherwise</returns>
    bool TryGet(string path, out object? value);
}
