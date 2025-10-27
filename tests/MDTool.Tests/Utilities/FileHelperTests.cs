using System.Text;
using Xunit;
using MDTool.Models;
using MDTool.Utilities;

namespace MDTool.Tests.Utilities;

/// <summary>
/// Comprehensive tests for FileHelper class.
/// Uses temporary directories for file operations.
/// </summary>
public class FileHelperTests : IDisposable
{
    private readonly string _testDirectory;

    public FileHelperTests()
    {
        // Create unique temp directory for this test run
        _testDirectory = Path.Combine(Path.GetTempPath(), $"mdtool-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        // Clean up test directory
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    #region ReadFileAsync Tests

    [Fact]
    public async Task ReadFileAsync_ExistingFile_ReturnsContent()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "test.txt");
        var expectedContent = "Hello, World!";
        await File.WriteAllTextAsync(filePath, expectedContent, new UTF8Encoding(false));

        // Act
        var result = await FileHelper.ReadFileAsync(filePath);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(expectedContent, result.Value);
    }

    [Fact]
    public async Task ReadFileAsync_NonExistentFile_ReturnsFileNotFoundError()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "nonexistent.txt");

        // Act
        var result = await FileHelper.ReadFileAsync(filePath);

        // Assert
        Assert.False(result.Success);
        Assert.Single(result.Errors);
        Assert.Equal(ErrorType.FileNotFound, result.Errors.First().Type);
    }

    [Fact]
    public async Task ReadFileAsync_NullPath_ReturnsInvalidPathError()
    {
        // Act
        var result = await FileHelper.ReadFileAsync(null!);

        // Assert
        Assert.False(result.Success);
        Assert.Single(result.Errors);
        Assert.Equal(ErrorType.InvalidPath, result.Errors.First().Type);
    }

    [Fact]
    public async Task ReadFileAsync_EmptyPath_ReturnsInvalidPathError()
    {
        // Act
        var result = await FileHelper.ReadFileAsync("");

        // Assert
        Assert.False(result.Success);
        Assert.Single(result.Errors);
        Assert.Equal(ErrorType.InvalidPath, result.Errors.First().Type);
    }

    [Fact]
    public async Task ReadFileAsync_WhitespacePath_ReturnsInvalidPathError()
    {
        // Act
        var result = await FileHelper.ReadFileAsync("   ");

        // Assert
        Assert.False(result.Success);
        Assert.Single(result.Errors);
        Assert.Equal(ErrorType.InvalidPath, result.Errors.First().Type);
    }

    [Fact]
    public async Task ReadFileAsync_Utf8Content_ReturnsCorrectly()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "utf8.txt");
        var expectedContent = "Hello 测试 テスト привет";
        await File.WriteAllTextAsync(filePath, expectedContent, new UTF8Encoding(false));

        // Act
        var result = await FileHelper.ReadFileAsync(filePath);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(expectedContent, result.Value);
    }

    [Fact]
    public async Task ReadFileAsync_EmptyFile_ReturnsEmptyString()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "empty.txt");
        await File.WriteAllTextAsync(filePath, "", new UTF8Encoding(false));

        // Act
        var result = await FileHelper.ReadFileAsync(filePath);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("", result.Value);
    }

    [Fact]
    public async Task ReadFileAsync_LargeFileWithinLimit_Succeeds()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "large.txt");
        // Create 5MB file (within 10MB limit)
        var content = new string('x', 5 * 1024 * 1024);
        await File.WriteAllTextAsync(filePath, content, new UTF8Encoding(false));

        // Act
        var result = await FileHelper.ReadFileAsync(filePath);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(content.Length, result.Value!.Length);
    }

    [Fact]
    public async Task ReadFileAsync_FileExceedsSizeLimit_ReturnsFileSizeExceededError()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "toolarge.txt");
        // Create 11MB file (exceeds 10MB limit)
        var content = new string('x', 11 * 1024 * 1024);
        await File.WriteAllTextAsync(filePath, content);

        // Act
        var result = await FileHelper.ReadFileAsync(filePath);

        // Assert
        Assert.False(result.Success);
        Assert.Single(result.Errors);
        Assert.Equal(ErrorType.FileSizeExceeded, result.Errors.First().Type);
    }

    #endregion

    #region WriteFileAsync Tests

    [Fact]
    public async Task WriteFileAsync_NewFile_CreatesFile()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "newfile.txt");
        var content = "Test content";

        // Act
        var result = await FileHelper.WriteFileAsync(filePath, content);

        // Assert
        Assert.True(result.Success);
        Assert.True(File.Exists(filePath));
        var actualContent = await File.ReadAllTextAsync(filePath);
        Assert.Equal(content, actualContent);
    }

    [Fact]
    public async Task WriteFileAsync_ExistingFileWithoutForce_ReturnsFileExistsError()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "existing.txt");
        await File.WriteAllTextAsync(filePath, "original content");

        // Act
        var result = await FileHelper.WriteFileAsync(filePath, "new content", force: false);

        // Assert
        Assert.False(result.Success);
        Assert.Single(result.Errors);
        Assert.Equal(ErrorType.FileExists, result.Errors.First().Type);

        // Verify original content is unchanged
        var actualContent = await File.ReadAllTextAsync(filePath);
        Assert.Equal("original content", actualContent);
    }

    [Fact]
    public async Task WriteFileAsync_ExistingFileWithForce_OverwritesFile()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "overwrite.txt");
        await File.WriteAllTextAsync(filePath, "original content");
        var newContent = "new content";

        // Act
        var result = await FileHelper.WriteFileAsync(filePath, newContent, force: true);

        // Assert
        Assert.True(result.Success);
        var actualContent = await File.ReadAllTextAsync(filePath);
        Assert.Equal(newContent, actualContent);
    }

    [Fact]
    public async Task WriteFileAsync_CreatesParentDirectories()
    {
        // Arrange
        var nestedPath = Path.Combine(_testDirectory, "level1", "level2", "level3", "file.txt");
        var content = "nested content";

        // Act
        var result = await FileHelper.WriteFileAsync(nestedPath, content);

        // Assert
        Assert.True(result.Success);
        Assert.True(File.Exists(nestedPath));
        var actualContent = await File.ReadAllTextAsync(nestedPath);
        Assert.Equal(content, actualContent);
    }

    [Fact]
    public async Task WriteFileAsync_Utf8WithoutBom_WritesCorrectly()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "utf8-no-bom.txt");
        var content = "Hello 测试 テスト";

        // Act
        var result = await FileHelper.WriteFileAsync(filePath, content);

        // Assert
        Assert.True(result.Success);

        // Read raw bytes to verify no BOM
        var bytes = await File.ReadAllBytesAsync(filePath);
        Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);

        // Verify content
        var actualContent = await File.ReadAllTextAsync(filePath, new UTF8Encoding(false));
        Assert.Equal(content, actualContent);
    }

    [Fact]
    public async Task WriteFileAsync_NullPath_ReturnsInvalidPathError()
    {
        // Act
        var result = await FileHelper.WriteFileAsync(null!, "content");

        // Assert
        Assert.False(result.Success);
        Assert.Single(result.Errors);
        Assert.Equal(ErrorType.InvalidPath, result.Errors.First().Type);
    }

    [Fact]
    public async Task WriteFileAsync_EmptyPath_ReturnsInvalidPathError()
    {
        // Act
        var result = await FileHelper.WriteFileAsync("", "content");

        // Assert
        Assert.False(result.Success);
        Assert.Single(result.Errors);
        Assert.Equal(ErrorType.InvalidPath, result.Errors.First().Type);
    }

    [Fact]
    public async Task WriteFileAsync_NullContent_ReturnsProcessingError()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "null-content.txt");

        // Act
        var result = await FileHelper.WriteFileAsync(filePath, null!);

        // Assert
        Assert.False(result.Success);
        Assert.Single(result.Errors);
        Assert.Equal(ErrorType.ProcessingError, result.Errors.First().Type);
    }

    [Fact]
    public async Task WriteFileAsync_EmptyContent_WritesEmptyFile()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "empty-write.txt");

        // Act
        var result = await FileHelper.WriteFileAsync(filePath, "");

        // Assert
        Assert.True(result.Success);
        var actualContent = await File.ReadAllTextAsync(filePath);
        Assert.Equal("", actualContent);
    }

    #endregion

    #region ValidatePath Tests

    [Fact]
    public void ValidatePath_ValidPath_ReturnsAbsolutePath()
    {
        // Arrange
        var path = "test.txt";

        // Act
        var result = FileHelper.ValidatePath(path);

        // Assert
        Assert.True(result.Success);
        Assert.True(Path.IsPathRooted(result.Value));
    }

    [Fact]
    public void ValidatePath_AbsolutePath_ReturnsUnchanged()
    {
        // Arrange
        var path = Path.Combine(_testDirectory, "test.txt");

        // Act
        var result = FileHelper.ValidatePath(path);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(Path.GetFullPath(path), result.Value);
    }

    [Fact]
    public void ValidatePath_NullPath_ReturnsInvalidPathError()
    {
        // Act
        var result = FileHelper.ValidatePath(null!);

        // Assert
        Assert.False(result.Success);
        Assert.Single(result.Errors);
        Assert.Equal(ErrorType.InvalidPath, result.Errors.First().Type);
    }

    [Fact]
    public void ValidatePath_EmptyPath_ReturnsInvalidPathError()
    {
        // Act
        var result = FileHelper.ValidatePath("");

        // Assert
        Assert.False(result.Success);
        Assert.Single(result.Errors);
        Assert.Equal(ErrorType.InvalidPath, result.Errors.First().Type);
    }

    [Fact]
    public void ValidatePath_WhitespacePath_ReturnsInvalidPathError()
    {
        // Act
        var result = FileHelper.ValidatePath("   ");

        // Assert
        Assert.False(result.Success);
        Assert.Single(result.Errors);
        Assert.Equal(ErrorType.InvalidPath, result.Errors.First().Type);
    }

    [Fact]
    public void ValidatePath_WithStrictMode_DotsInPath_ReturnsPathTraversalError()
    {
        // Arrange
        var path = "../parent/file.txt";

        // Act
        var result = FileHelper.ValidatePath(path, strictForMacros: true);

        // Assert
        Assert.False(result.Success);
        Assert.Single(result.Errors);
        Assert.Equal(ErrorType.PathTraversalAttempt, result.Errors.First().Type);
    }

    [Fact]
    public void ValidatePath_WithStrictMode_TildeInPath_ReturnsPathTraversalError()
    {
        // Arrange
        var path = "~/file.txt";

        // Act
        var result = FileHelper.ValidatePath(path, strictForMacros: true);

        // Assert
        Assert.False(result.Success);
        Assert.Single(result.Errors);
        Assert.Equal(ErrorType.PathTraversalAttempt, result.Errors.First().Type);
    }

    [Fact]
    public void ValidatePath_WithoutStrictMode_DotsInPath_Succeeds()
    {
        // Arrange
        var path = "../parent/file.txt";

        // Act
        var result = FileHelper.ValidatePath(path, strictForMacros: false);

        // Assert
        Assert.True(result.Success);
        Assert.True(Path.IsPathRooted(result.Value));
    }

    [Fact]
    public void ValidatePath_WithoutStrictMode_TildeInPath_Succeeds()
    {
        // Arrange
        var path = "~/file.txt";

        // Act
        var result = FileHelper.ValidatePath(path, strictForMacros: false);

        // Assert
        Assert.True(result.Success);
    }

    #endregion

    #region CheckFileSize Tests

    [Fact]
    public async Task CheckFileSize_WithinLimit_Succeeds()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "within-limit.txt");
        var content = new string('x', 1024); // 1KB
        await File.WriteAllTextAsync(filePath, content);

        // Act
        var result = FileHelper.CheckFileSize(filePath);

        // Assert
        Assert.True(result.Success);
    }

    [Fact]
    public async Task CheckFileSize_ExceedsLimit_ReturnsFileSizeExceededError()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "exceeds-limit.txt");
        var content = new string('x', 11 * 1024 * 1024); // 11MB
        await File.WriteAllTextAsync(filePath, content);

        // Act
        var result = FileHelper.CheckFileSize(filePath);

        // Assert
        Assert.False(result.Success);
        Assert.Single(result.Errors);
        Assert.Equal(ErrorType.FileSizeExceeded, result.Errors.First().Type);
    }

    [Fact]
    public async Task CheckFileSize_ExactlyAtLimit_Succeeds()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "at-limit.txt");
        var content = new string('x', (int)FileHelper.MaxFileSizeBytes);
        await File.WriteAllTextAsync(filePath, content);

        // Act
        var result = FileHelper.CheckFileSize(filePath);

        // Assert
        Assert.True(result.Success);
    }

    [Fact]
    public void CheckFileSize_NonExistentFile_ReturnsFileNotFoundError()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "nonexistent-for-size.txt");

        // Act
        var result = FileHelper.CheckFileSize(filePath);

        // Assert
        Assert.False(result.Success);
        Assert.Single(result.Errors);
        Assert.Equal(ErrorType.FileNotFound, result.Errors.First().Type);
    }

    [Fact]
    public async Task CheckFileSize_CustomLimit_Succeeds()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "custom-limit.txt");
        var content = new string('x', 2000); // 2KB
        await File.WriteAllTextAsync(filePath, content);

        // Act
        var result = FileHelper.CheckFileSize(filePath, maxSizeBytes: 3000);

        // Assert
        Assert.True(result.Success);
    }

    [Fact]
    public async Task CheckFileSize_CustomLimit_Exceeds()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "custom-limit-exceeds.txt");
        var content = new string('x', 2000); // 2KB
        await File.WriteAllTextAsync(filePath, content);

        // Act
        var result = FileHelper.CheckFileSize(filePath, maxSizeBytes: 1000);

        // Assert
        Assert.False(result.Success);
        Assert.Single(result.Errors);
        Assert.Equal(ErrorType.FileSizeExceeded, result.Errors.First().Type);
    }

    [Fact]
    public async Task CheckFileSize_EmptyFile_Succeeds()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "empty-for-size.txt");
        await File.WriteAllTextAsync(filePath, "");

        // Act
        var result = FileHelper.CheckFileSize(filePath);

        // Assert
        Assert.True(result.Success);
    }

    #endregion

    #region ResolvePathFromCwd Tests

    [Fact]
    public void ResolvePathFromCwd_NullPath_ReturnsCurrentDirectory()
    {
        // Act
        var result = FileHelper.ResolvePathFromCwd(null!);

        // Assert
        Assert.Equal(Directory.GetCurrentDirectory(), result);
    }

    [Fact]
    public void ResolvePathFromCwd_EmptyPath_ReturnsCurrentDirectory()
    {
        // Act
        var result = FileHelper.ResolvePathFromCwd("");

        // Assert
        Assert.Equal(Directory.GetCurrentDirectory(), result);
    }

    [Fact]
    public void ResolvePathFromCwd_WhitespacePath_ReturnsCurrentDirectory()
    {
        // Act
        var result = FileHelper.ResolvePathFromCwd("   ");

        // Assert
        Assert.Equal(Directory.GetCurrentDirectory(), result);
    }

    [Fact]
    public void ResolvePathFromCwd_RelativePath_ReturnsAbsolute()
    {
        // Arrange
        var relativePath = "test.txt";
        var expected = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), relativePath));

        // Act
        var result = FileHelper.ResolvePathFromCwd(relativePath);

        // Assert
        Assert.Equal(expected, result);
        Assert.True(Path.IsPathRooted(result));
    }

    [Fact]
    public void ResolvePathFromCwd_AbsolutePath_ReturnsNormalized()
    {
        // Arrange
        var absolutePath = _testDirectory;

        // Act
        var result = FileHelper.ResolvePathFromCwd(absolutePath);

        // Assert
        Assert.Equal(Path.GetFullPath(absolutePath), result);
        Assert.True(Path.IsPathRooted(result));
    }

    [Fact]
    public void ResolvePathFromCwd_NestedRelativePath_ReturnsAbsolute()
    {
        // Arrange
        var relativePath = Path.Combine("folder1", "folder2", "file.txt");
        var expected = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), relativePath));

        // Act
        var result = FileHelper.ResolvePathFromCwd(relativePath);

        // Assert
        Assert.Equal(expected, result);
        Assert.True(Path.IsPathRooted(result));
    }

    #endregion
}
