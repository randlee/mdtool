using Xunit;
using MDTool.Models;

namespace MDTool.Tests.Models;

public class ValidationErrorTests
{
    [Fact]
    public void MissingVariable_CreatesCorrectError()
    {
        // Act
        var error = ValidationError.MissingVariable("USER_NAME", "The user's full name");

        // Assert
        Assert.Equal(ErrorType.MissingRequiredVariable, error.Type);
        Assert.Equal("USER_NAME", error.Variable);
        Assert.Contains("USER_NAME", error.Description);
        Assert.Contains("The user's full name", error.Description);
        Assert.Null(error.Line);
    }

    [Fact]
    public void InvalidYaml_CreatesCorrectError()
    {
        // Act
        var error = ValidationError.InvalidYaml("Unexpected character", 5);

        // Assert
        Assert.Equal(ErrorType.InvalidYamlHeader, error.Type);
        Assert.Contains("Invalid YAML frontmatter", error.Description);
        Assert.Contains("Unexpected character", error.Description);
        Assert.Equal(5, error.Line);
    }

    [Fact]
    public void InvalidJson_CreatesCorrectError()
    {
        // Act
        var error = ValidationError.InvalidJson("Unexpected token");

        // Assert
        Assert.Equal(ErrorType.InvalidJsonArgs, error.Type);
        Assert.Contains("Invalid JSON arguments", error.Description);
        Assert.Contains("Unexpected token", error.Description);
    }

    [Fact]
    public void FileNotFound_CreatesCorrectError()
    {
        // Act
        var error = ValidationError.FileNotFound("/path/to/file.md");

        // Assert
        Assert.Equal(ErrorType.FileNotFound, error.Type);
        Assert.Contains("File not found", error.Description);
        Assert.Contains("/path/to/file.md", error.Description);
    }

    [Fact]
    public void FileSizeExceeded_CreatesCorrectError()
    {
        // Act
        var error = ValidationError.FileSizeExceeded("/path/to/file.md", 15000000, 10000000);

        // Assert
        Assert.Equal(ErrorType.FileSizeExceeded, error.Type);
        Assert.Contains("File size exceeded", error.Description);
        Assert.Contains("/path/to/file.md", error.Description);
        Assert.Contains("15000000", error.Description);
        Assert.Contains("10000000", error.Description);
    }

    [Fact]
    public void FileExists_CreatesCorrectError()
    {
        // Act
        var error = ValidationError.FileExists("/path/to/file.md");

        // Assert
        Assert.Equal(ErrorType.FileExists, error.Type);
        Assert.Contains("File already exists", error.Description);
        Assert.Contains("/path/to/file.md", error.Description);
    }

    [Fact]
    public void FileAccessDenied_CreatesCorrectError()
    {
        // Act
        var error = ValidationError.FileAccessDenied("/path/to/file.md");

        // Assert
        Assert.Equal(ErrorType.FileAccessDenied, error.Type);
        Assert.Contains("Access denied", error.Description);
        Assert.Contains("/path/to/file.md", error.Description);
    }

    [Fact]
    public void FileReadError_CreatesCorrectError()
    {
        // Act
        var error = ValidationError.FileReadError("/path/to/file.md", "Disk error");

        // Assert
        Assert.Equal(ErrorType.FileReadError, error.Type);
        Assert.Contains("Error reading file", error.Description);
        Assert.Contains("/path/to/file.md", error.Description);
        Assert.Contains("Disk error", error.Description);
    }

    [Fact]
    public void FileWriteError_CreatesCorrectError()
    {
        // Act
        var error = ValidationError.FileWriteError("/path/to/file.md", "Disk full");

        // Assert
        Assert.Equal(ErrorType.FileWriteError, error.Type);
        Assert.Contains("Error writing file", error.Description);
        Assert.Contains("/path/to/file.md", error.Description);
        Assert.Contains("Disk full", error.Description);
    }

    [Fact]
    public void DirectoryCreationFailed_CreatesCorrectError()
    {
        // Act
        var error = ValidationError.DirectoryCreationFailed("/path/to/dir", "Permission denied");

        // Assert
        Assert.Equal(ErrorType.DirectoryCreationFailed, error.Type);
        Assert.Contains("Failed to create directory", error.Description);
        Assert.Contains("/path/to/dir", error.Description);
        Assert.Contains("Permission denied", error.Description);
    }

    [Fact]
    public void InvalidPath_CreatesCorrectError()
    {
        // Act
        var error = ValidationError.InvalidPath("/invalid/path", "Contains illegal characters");

        // Assert
        Assert.Equal(ErrorType.InvalidPath, error.Type);
        Assert.Contains("Invalid path", error.Description);
        Assert.Contains("/invalid/path", error.Description);
        Assert.Contains("Contains illegal characters", error.Description);
    }

    [Fact]
    public void PathTraversalAttempt_CreatesCorrectError()
    {
        // Act
        var error = ValidationError.PathTraversalAttempt("../../etc/passwd");

        // Assert
        Assert.Equal(ErrorType.PathTraversalAttempt, error.Type);
        Assert.Contains("Path traversal attempt detected", error.Description);
        Assert.Contains("../../etc/passwd", error.Description);
    }

    [Fact]
    public void InvalidFormat_CreatesCorrectError()
    {
        // Act
        var error = ValidationError.InvalidFormat("{{UNCLOSED", "Not properly closed", 10);

        // Assert
        Assert.Equal(ErrorType.InvalidVariableFormat, error.Type);
        Assert.Equal("{{UNCLOSED", error.Variable);
        Assert.Contains("Invalid variable format", error.Description);
        Assert.Contains("Not properly closed", error.Description);
        Assert.Equal(10, error.Line);
    }

    [Fact]
    public void RecursionDepthExceeded_CreatesCorrectError()
    {
        // Act
        var error = ValidationError.RecursionDepthExceeded(100);

        // Assert
        Assert.Equal(ErrorType.RecursionDepthExceeded, error.Type);
        Assert.Contains("Maximum recursion depth exceeded", error.Description);
        Assert.Contains("100", error.Description);
    }

    [Fact]
    public void UnhandledException_CreatesCorrectError()
    {
        // Act
        var error = ValidationError.UnhandledException("Something went wrong");

        // Assert
        Assert.Equal(ErrorType.UnhandledException, error.Type);
        Assert.Contains("Unhandled exception", error.Description);
        Assert.Contains("Something went wrong", error.Description);
    }

    [Fact]
    public void SerializationError_CreatesCorrectError()
    {
        // Act
        var error = ValidationError.SerializationError("Cannot serialize object");

        // Assert
        Assert.Equal(ErrorType.SerializationError, error.Type);
        Assert.Contains("Serialization error", error.Description);
        Assert.Contains("Cannot serialize object", error.Description);
    }

    [Fact]
    public void ProcessingError_CreatesCorrectError()
    {
        // Act
        var error = ValidationError.ProcessingError("Generic error message");

        // Assert
        Assert.Equal(ErrorType.ProcessingError, error.Type);
        Assert.Equal("Generic error message", error.Description);
    }

    [Fact]
    public void ToString_FormatsCorrectly_WithAllFields()
    {
        // Act
        var error = ValidationError.InvalidFormat("VAR", "Bad format", 5);
        var str = error.ToString();

        // Assert
        Assert.Contains("InvalidVariableFormat", str);
        Assert.Contains("[VAR]", str);
        Assert.Contains("(line 5)", str);
        Assert.Contains("Bad format", str);
    }

    [Fact]
    public void ToString_FormatsCorrectly_WithoutLine()
    {
        // Act
        var error = ValidationError.FileNotFound("/path/file.md");
        var str = error.ToString();

        // Assert
        Assert.Contains("FileNotFound", str);
        Assert.DoesNotContain("(line", str);
    }

    [Fact]
    public void ToString_FormatsCorrectly_WithoutVariable()
    {
        // Act
        var error = ValidationError.ProcessingError("Generic error");
        var str = error.ToString();

        // Assert
        Assert.Contains("ProcessingError", str);
        Assert.DoesNotContain("[", str);
    }

    [Fact]
    public void ToJsonObject_ReturnsCorrectStructure()
    {
        // Act
        var error = ValidationError.InvalidFormat("VAR", "Bad format", 5);
        var json = error.ToJsonObject();

        // Assert
        Assert.NotNull(json);
        var dict = json as dynamic;
        Assert.NotNull(dict);
    }

    [Fact]
    public void ErrorType_HasAllRequiredValues()
    {
        // Assert - verify all 17 error types exist
        Assert.Equal(17, Enum.GetValues(typeof(ErrorType)).Length);
        Assert.True(Enum.IsDefined(typeof(ErrorType), ErrorType.MissingRequiredVariable));
        Assert.True(Enum.IsDefined(typeof(ErrorType), ErrorType.InvalidYamlHeader));
        Assert.True(Enum.IsDefined(typeof(ErrorType), ErrorType.InvalidJsonArgs));
        Assert.True(Enum.IsDefined(typeof(ErrorType), ErrorType.FileNotFound));
        Assert.True(Enum.IsDefined(typeof(ErrorType), ErrorType.FileSizeExceeded));
        Assert.True(Enum.IsDefined(typeof(ErrorType), ErrorType.FileExists));
        Assert.True(Enum.IsDefined(typeof(ErrorType), ErrorType.FileAccessDenied));
        Assert.True(Enum.IsDefined(typeof(ErrorType), ErrorType.FileReadError));
        Assert.True(Enum.IsDefined(typeof(ErrorType), ErrorType.FileWriteError));
        Assert.True(Enum.IsDefined(typeof(ErrorType), ErrorType.DirectoryCreationFailed));
        Assert.True(Enum.IsDefined(typeof(ErrorType), ErrorType.InvalidPath));
        Assert.True(Enum.IsDefined(typeof(ErrorType), ErrorType.PathTraversalAttempt));
        Assert.True(Enum.IsDefined(typeof(ErrorType), ErrorType.InvalidVariableFormat));
        Assert.True(Enum.IsDefined(typeof(ErrorType), ErrorType.RecursionDepthExceeded));
        Assert.True(Enum.IsDefined(typeof(ErrorType), ErrorType.UnhandledException));
        Assert.True(Enum.IsDefined(typeof(ErrorType), ErrorType.SerializationError));
        Assert.True(Enum.IsDefined(typeof(ErrorType), ErrorType.ProcessingError));
    }
}
