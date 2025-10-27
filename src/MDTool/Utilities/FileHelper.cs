using System.Text;
using MDTool.Models;

namespace MDTool.Utilities;

/// <summary>
/// Provides secure file I/O operations with comprehensive validation and error handling.
/// All operations return ProcessingResult for predictable error handling.
/// </summary>
public static class FileHelper
{
    /// <summary>
    /// Maximum file size in bytes (10 MB).
    /// </summary>
    public const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

    /// <summary>
    /// Reads a file asynchronously with validation and UTF-8 decoding without BOM.
    /// </summary>
    /// <param name="path">Path to the file to read</param>
    /// <returns>ProcessingResult containing file content or errors</returns>
    public static async Task<ProcessingResult<string>> ReadFileAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            // ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
            return ProcessingResult<string>.Fail( ValidationError.InvalidPath(path ?? "", "File path cannot be null or empty") );
        }

        // Validate path security
        var pathResult = ValidatePath(path, strictForMacros: false);
        if (!pathResult.Success)
        {
            return ProcessingResult<string>.Fail(pathResult.Errors);
        }

        // ReSharper disable once NullableWarningSuppressionIsUsed
        var validatedPath = pathResult.Value!;

        // Check file exists
        if (!File.Exists(validatedPath))
        {
            return ProcessingResult<string>.Fail( ValidationError.FileNotFound(validatedPath) );
        }

        // Check file size
        var sizeResult = CheckFileSize(validatedPath);
        if (!sizeResult.Success)
        {
            return ProcessingResult<string>.Fail(sizeResult.Errors);
        }

        // Read file with UTF-8 encoding without BOM
        try
        {
            var content = await File.ReadAllTextAsync(validatedPath, new UTF8Encoding(false));
            return ProcessingResult<string>.Ok(content);
        }
        catch (UnauthorizedAccessException)
        {
            return ProcessingResult<string>.Fail( ValidationError.FileAccessDenied(validatedPath) );
        }
        catch (IOException ex)
        {
            return ProcessingResult<string>.Fail( ValidationError.FileReadError(validatedPath, ex.Message) );
        }
        catch (Exception ex)
        {
            return ProcessingResult<string>.Fail( ValidationError.FileReadError(validatedPath, ex.Message) );
        }
    }

    /// <summary>
    /// Writes content to a file asynchronously with overwrite protection.
    /// Creates parent directories automatically.
    /// </summary>
    /// <param name="path">Path to the file to write</param>
    /// <param name="content">Content to write to the file</param>
    /// <param name="force">If true, overwrites existing files; if false, returns error</param>
    /// <returns>ProcessingResult indicating success or failure</returns>
    public static async Task<ProcessingResult<Unit>> WriteFileAsync(string path, string content, bool force = false)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            // ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
            return ProcessingResult<Unit>.Fail( ValidationError.InvalidPath(path ?? "", "File path cannot be null or empty") );
        }

        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (content == null)
        {
            return ProcessingResult<Unit>.Fail( ValidationError.ProcessingError("Content cannot be null") );
        }

        // Validate path security
        var pathResult = ValidatePath(path, strictForMacros: false);
        if (!pathResult.Success)
        {
            return ProcessingResult<Unit>.Fail(pathResult.Errors);
        }

        // ReSharper disable once NullableWarningSuppressionIsUsed
        var validatedPath = pathResult.Value!;

        // Check if file exists and force flag
        if (File.Exists(validatedPath) && !force)
        {
            return ProcessingResult<Unit>.Fail( ValidationError.FileExists(validatedPath) );
        }

        // Create parent directories if they don't exist
        var directory = Path.GetDirectoryName(validatedPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            try
            {
                Directory.CreateDirectory(directory);
            }
            catch (UnauthorizedAccessException ex)
            {
                return ProcessingResult<Unit>.Fail( ValidationError.DirectoryCreationFailed(directory, ex.Message) );
            }
            catch (IOException ex)
            {
                return ProcessingResult<Unit>.Fail( ValidationError.DirectoryCreationFailed(directory, ex.Message) );
            }
            catch (Exception ex)
            {
                return ProcessingResult<Unit>.Fail( ValidationError.DirectoryCreationFailed(directory, ex.Message) );
            }
        }

        // Write file with UTF-8 encoding without BOM
        try
        {
            await File.WriteAllTextAsync(validatedPath, content, new UTF8Encoding(false));
            return ProcessingResult<Unit>.Ok(Unit.Value);
        }
        catch (UnauthorizedAccessException)
        {
            return ProcessingResult<Unit>.Fail( ValidationError.FileAccessDenied(validatedPath) );
        }
        catch (IOException ex)
        {
            return ProcessingResult<Unit>.Fail( ValidationError.FileWriteError(validatedPath, ex.Message) );
        }
        catch (Exception ex)
        {
            return ProcessingResult<Unit>.Fail( ValidationError.FileWriteError(validatedPath, ex.Message) );
        }
    }

    /// <summary>
    /// Validates a file path for security issues.
    /// In Phase 1, strictForMacros is false by default (no global CWD restriction).
    /// When strictForMacros is true (Phase 2), enforces additional traversal rules.
    /// </summary>
    /// <param name="path">Path to validate</param>
    /// <param name="strictForMacros">If true, enforces strict validation for macro expansions</param>
    /// <returns>ProcessingResult containing absolute path or errors</returns>
    public static ProcessingResult<string> ValidatePath(string path, bool strictForMacros = false)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            // ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
            return ProcessingResult<string>.Fail( ValidationError.InvalidPath(path ?? "", "Path cannot be null or empty") );
        }

        try
        {
            // Get absolute path
            var absolutePath = Path.GetFullPath(path);

            // Strict validation for macro expansions (Phase 2)
            if (strictForMacros)
            {
                if (path.Contains("..") || path.Contains("~"))
                {
                    return ProcessingResult<string>.Fail( ValidationError.PathTraversalAttempt(path) );
                }
            }

            // Check for invalid characters
            var invalidChars = Path.GetInvalidPathChars();
            if (path.Any(c => invalidChars.Contains(c)))
            {
                return ProcessingResult<string>.Fail( ValidationError.InvalidPath(path, "Path contains invalid characters") );
            }

            return ProcessingResult<string>.Ok(absolutePath);
        }
        catch (ArgumentException ex)
        {
            return ProcessingResult<string>.Fail( ValidationError.InvalidPath(path, ex.Message) );
        }
        catch (UnauthorizedAccessException)
        {
            return ProcessingResult<string>.Fail( ValidationError.FileAccessDenied(path) );
        }
        catch (Exception ex)
        {
            return ProcessingResult<string>.Fail( ValidationError.InvalidPath(path, ex.Message) );
        }
    }

    /// <summary>
    /// Checks if a file's size is within the allowed limit (10 MB).
    /// </summary>
    /// <param name="path">Path to the file to check</param>
    /// <param name="maxSizeBytes">Maximum allowed size in bytes (default: 10 MB)</param>
    /// <returns>ProcessingResult indicating success or failure</returns>
    public static ProcessingResult<Unit> CheckFileSize(string path, long maxSizeBytes = MaxFileSizeBytes)
    {
        if (!File.Exists(path))
        {
            return ProcessingResult<Unit>.Fail( ValidationError.FileNotFound(path) );
        }

        try
        {
            var fileInfo = new FileInfo(path);
            var fileSizeBytes = fileInfo.Length;

            if (fileSizeBytes > maxSizeBytes)
            {
                return ProcessingResult<Unit>.Fail( ValidationError.FileSizeExceeded(path, fileSizeBytes, maxSizeBytes) );
            }

            return ProcessingResult<Unit>.Ok(Unit.Value);
        }
        catch (UnauthorizedAccessException)
        {
            return ProcessingResult<Unit>.Fail( ValidationError.FileAccessDenied(path) );
        }
        catch (IOException ex)
        {
            return ProcessingResult<Unit>.Fail( ValidationError.FileReadError(path, ex.Message) );
        }
        catch (Exception ex)
        {
            return ProcessingResult<Unit>.Fail( ValidationError.FileReadError(path, ex.Message) );
        }
    }

    /// <summary>
    /// Resolves a relative path from the current working directory.
    /// </summary>
    /// <param name="path">Path to resolve</param>
    /// <returns>Absolute path</returns>
    public static string ResolvePathFromCwd(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Directory.GetCurrentDirectory();
        }

        // If already absolute, return normalized path
        if (Path.IsPathRooted(path))
        {
            return Path.GetFullPath(path);
        }

        // Resolve relative to CWD
        var cwd = Directory.GetCurrentDirectory();
        return Path.GetFullPath(Path.Combine(cwd, path));
    }
}
