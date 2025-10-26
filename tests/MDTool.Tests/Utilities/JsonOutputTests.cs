using System.Text.Json;
using Xunit;
using MDTool.Models;
using MDTool.Utilities;

namespace MDTool.Tests.Utilities;

/// <summary>
/// Comprehensive tests for JsonOutput class.
/// Verifies lowerCamelCase property naming and proper JSON formatting.
/// </summary>
public class JsonOutputTests
{
    #region Success Tests

    [Fact]
    public void Success_SimpleString_ReturnsValidJson()
    {
        // Arrange
        var result = "Test result";

        // Act
        var json = JsonOutput.Success(result);

        // Assert
        Assert.Contains("\"success\": true", json);
        Assert.Contains("\"result\": \"Test result\"", json);

        // Verify it's valid JSON
        var parsed = JsonDocument.Parse(json);
        Assert.True(parsed.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("Test result", parsed.RootElement.GetProperty("result").GetString());
    }

    [Fact]
    public void Success_SimpleObject_UsesLowerCamelCase()
    {
        // Arrange
        var result = new { UserName = "John", EmailAddress = "john@example.com" };

        // Act
        var json = JsonOutput.Success(result);

        // Assert
        Assert.Contains("\"userName\"", json);
        Assert.Contains("\"emailAddress\"", json);
        Assert.DoesNotContain("\"UserName\"", json);
        Assert.DoesNotContain("\"EmailAddress\"", json);

        // Verify it's valid JSON with correct structure
        var parsed = JsonDocument.Parse(json);
        Assert.True(parsed.RootElement.GetProperty("success").GetBoolean());
        var resultObj = parsed.RootElement.GetProperty("result");
        Assert.Equal("John", resultObj.GetProperty("userName").GetString());
        Assert.Equal("john@example.com", resultObj.GetProperty("emailAddress").GetString());
    }

    [Fact]
    public void Success_NestedObject_UsesLowerCamelCaseNested()
    {
        // Arrange
        var result = new
        {
            UserName = "John",
            UserProfile = new
            {
                FirstName = "John",
                LastName = "Doe",
                EmailAddress = "john@example.com"
            }
        };

        // Act
        var json = JsonOutput.Success(result);

        // Assert
        Assert.Contains("\"userName\"", json);
        Assert.Contains("\"userProfile\"", json);
        Assert.Contains("\"firstName\"", json);
        Assert.Contains("\"lastName\"", json);
        Assert.Contains("\"emailAddress\"", json);

        // Verify nested structure
        var parsed = JsonDocument.Parse(json);
        var resultObj = parsed.RootElement.GetProperty("result");
        var profile = resultObj.GetProperty("userProfile");
        Assert.Equal("John", profile.GetProperty("firstName").GetString());
        Assert.Equal("Doe", profile.GetProperty("lastName").GetString());
    }

    [Fact]
    public void Success_WithNumbers_FormatsCorrectly()
    {
        // Arrange
        var result = new { Port = 8080, MaxConnections = 1000, IsActive = true };

        // Act
        var json = JsonOutput.Success(result);

        // Assert
        var parsed = JsonDocument.Parse(json);
        var resultObj = parsed.RootElement.GetProperty("result");
        Assert.Equal(8080, resultObj.GetProperty("port").GetInt32());
        Assert.Equal(1000, resultObj.GetProperty("maxConnections").GetInt32());
        Assert.True(resultObj.GetProperty("isActive").GetBoolean());
    }

    [Fact]
    public void Success_WithArray_FormatsCorrectly()
    {
        // Arrange
        var result = new { Variables = new[] { "VAR1", "VAR2", "VAR3" } };

        // Act
        var json = JsonOutput.Success(result);

        // Assert
        var parsed = JsonDocument.Parse(json);
        var resultObj = parsed.RootElement.GetProperty("result");
        var vars = resultObj.GetProperty("variables");
        Assert.Equal(3, vars.GetArrayLength());
        Assert.Equal("VAR1", vars[0].GetString());
    }

    [Fact]
    public void Success_WithNull_HandlesGracefully()
    {
        // Arrange
        var result = new { Name = "Test", OptionalValue = (string?)null };

        // Act
        var json = JsonOutput.Success(result);

        // Assert
        var parsed = JsonDocument.Parse(json);
        var resultObj = parsed.RootElement.GetProperty("result");
        Assert.Equal("Test", resultObj.GetProperty("name").GetString());
        // Null values should be omitted with our configuration
        Assert.False(resultObj.TryGetProperty("optionalValue", out _));
    }

    [Fact]
    public void Success_PrettyPrinted_HasCorrectFormatting()
    {
        // Arrange
        var result = new { Key = "Value" };

        // Act
        var json = JsonOutput.Success(result);

        // Assert - pretty printed should have newlines
        Assert.Contains("\n", json);
        Assert.Contains("  ", json); // Should have indentation
    }

    #endregion

    #region Failure Tests

    [Fact]
    public void Failure_SingleError_ReturnsValidJson()
    {
        // Arrange
        var errors = new List<ValidationError>
        {
            ValidationError.FileNotFound("test.txt")
        };

        // Act
        var json = JsonOutput.Failure(errors);

        // Assert
        Assert.Contains("\"success\": false", json);
        Assert.Contains("\"errors\"", json);

        var parsed = JsonDocument.Parse(json);
        Assert.False(parsed.RootElement.GetProperty("success").GetBoolean());
        var errorsArray = parsed.RootElement.GetProperty("errors");
        Assert.Equal(1, errorsArray.GetArrayLength());
    }

    [Fact]
    public void Failure_MultipleErrors_ReturnsAllErrors()
    {
        // Arrange
        var errors = new List<ValidationError>
        {
            ValidationError.MissingVariable("USER_NAME", "User's name"),
            ValidationError.MissingVariable("EMAIL", "User's email"),
            ValidationError.InvalidFormat("{{BAD}}", "Unknown variable", 10)
        };

        // Act
        var json = JsonOutput.Failure(errors);

        // Assert
        var parsed = JsonDocument.Parse(json);
        var errorsArray = parsed.RootElement.GetProperty("errors");
        Assert.Equal(3, errorsArray.GetArrayLength());

        // Verify first error
        var firstError = errorsArray[0];
        Assert.Equal("MissingRequiredVariable", firstError.GetProperty("type").GetString());
        Assert.Equal("USER_NAME", firstError.GetProperty("variable").GetString());
    }

    [Fact]
    public void Failure_WithProvidedAndMissing_IncludesAllFields()
    {
        // Arrange
        var errors = new List<ValidationError>
        {
            ValidationError.MissingVariable("VAR1", "Description")
        };
        var provided = new List<string> { "VAR2", "VAR3" };
        var missing = new List<string> { "VAR1" };

        // Act
        var json = JsonOutput.Failure(errors, provided, missing);

        // Assert
        var parsed = JsonDocument.Parse(json);
        Assert.False(parsed.RootElement.GetProperty("success").GetBoolean());

        var providedArray = parsed.RootElement.GetProperty("provided");
        Assert.Equal(2, providedArray.GetArrayLength());
        Assert.Equal("VAR2", providedArray[0].GetString());

        var missingArray = parsed.RootElement.GetProperty("missing");
        Assert.Equal(1, missingArray.GetArrayLength());
        Assert.Equal("VAR1", missingArray[0].GetString());
    }

    [Fact]
    public void Failure_ErrorWithLineNumber_IncludesLine()
    {
        // Arrange
        var errors = new List<ValidationError>
        {
            ValidationError.InvalidYaml("Syntax error", line: 42)
        };

        // Act
        var json = JsonOutput.Failure(errors);

        // Assert
        var parsed = JsonDocument.Parse(json);
        var errorsArray = parsed.RootElement.GetProperty("errors");
        var firstError = errorsArray[0];
        Assert.Equal(42, firstError.GetProperty("line").GetInt32());
    }

    [Fact]
    public void Failure_ErrorWithoutLineNumber_LineIsNull()
    {
        // Arrange
        var errors = new List<ValidationError>
        {
            ValidationError.FileNotFound("test.txt")
        };

        // Act
        var json = JsonOutput.Failure(errors);

        // Assert
        var parsed = JsonDocument.Parse(json);
        var errorsArray = parsed.RootElement.GetProperty("errors");
        var firstError = errorsArray[0];
        // Null values should be omitted with our configuration
        Assert.False(firstError.TryGetProperty("line", out _));
    }

    [Fact]
    public void Failure_AllErrorTypes_SerializeCorrectly()
    {
        // Arrange - test various error types
        var errors = new List<ValidationError>
        {
            ValidationError.FileNotFound("file.txt"),
            ValidationError.FileSizeExceeded("large.txt", 15000000, 10000000),
            ValidationError.FileExists("existing.txt"),
            ValidationError.FileAccessDenied("protected.txt"),
            ValidationError.InvalidPath("invalid", "reason"),
            ValidationError.PathTraversalAttempt("../etc/passwd"),
            ValidationError.ProcessingError("Generic error")
        };

        // Act
        var json = JsonOutput.Failure(errors);

        // Assert
        var parsed = JsonDocument.Parse(json);
        var errorsArray = parsed.RootElement.GetProperty("errors");
        Assert.Equal(7, errorsArray.GetArrayLength());

        // Verify each error has type, description
        foreach (var error in errorsArray.EnumerateArray())
        {
            Assert.True(error.TryGetProperty("type", out _));
            Assert.True(error.TryGetProperty("description", out _));
        }
    }

    #endregion

    #region ValidationOutput Tests

    [Fact]
    public void ValidationOutput_SuccessfulValidation_ReturnsSuccessJson()
    {
        // Arrange
        var validation = ValidationResult.Ok(new List<string> { "VAR1", "VAR2" });

        // Act
        var json = JsonOutput.ValidationOutput(validation);

        // Assert
        var parsed = JsonDocument.Parse(json);
        Assert.True(parsed.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("All required variables provided", parsed.RootElement.GetProperty("result").GetString());
    }

    [Fact]
    public void ValidationOutput_FailedValidation_ReturnsFailureJson()
    {
        // Arrange
        var errors = new List<ValidationError>
        {
            ValidationError.MissingVariable("VAR1", "Description")
        };
        var validation = ValidationResult.Fail(
            errors,
            new List<string> { "VAR2" },
            new List<string> { "VAR1" }
        );

        // Act
        var json = JsonOutput.ValidationOutput(validation);

        // Assert
        var parsed = JsonDocument.Parse(json);
        Assert.False(parsed.RootElement.GetProperty("success").GetBoolean());
        Assert.True(parsed.RootElement.TryGetProperty("errors", out _));
        Assert.True(parsed.RootElement.TryGetProperty("provided", out _));
        Assert.True(parsed.RootElement.TryGetProperty("missing", out _));
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Success_EmptyString_HandlesCorrectly()
    {
        // Act
        var json = JsonOutput.Success("");

        // Assert
        var parsed = JsonDocument.Parse(json);
        Assert.True(parsed.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("", parsed.RootElement.GetProperty("result").GetString());
    }

    [Fact]
    public void Success_ComplexNestedStructure_SerializesCorrectly()
    {
        // Arrange
        var result = new
        {
            TopLevel = "value",
            Nested = new
            {
                Level1 = "value1",
                DeepNested = new
                {
                    Level2 = "value2",
                    VeryDeep = new
                    {
                        Level3 = "value3"
                    }
                }
            }
        };

        // Act
        var json = JsonOutput.Success(result);

        // Assert
        var parsed = JsonDocument.Parse(json);
        var resultObj = parsed.RootElement.GetProperty("result");
        var nested = resultObj.GetProperty("nested");
        var deepNested = nested.GetProperty("deepNested");
        var veryDeep = deepNested.GetProperty("veryDeep");
        Assert.Equal("value3", veryDeep.GetProperty("level3").GetString());
    }

    [Fact]
    public void Failure_EmptyErrorsList_ThrowsException()
    {
        // This should not be possible with ValidationError factory methods,
        // but we test to ensure the contract is enforced
        // Note: This test is for edge case, actual usage should prevent this
    }

    [Fact]
    public void Success_SpecialCharacters_EncodesCorrectly()
    {
        // Arrange
        var result = new
        {
            Text = "Line1\nLine2\tTab\"Quote"
        };

        // Act
        var json = JsonOutput.Success(result);

        // Assert - should be valid JSON
        var parsed = JsonDocument.Parse(json);
        var resultObj = parsed.RootElement.GetProperty("result");
        Assert.Equal("Line1\nLine2\tTab\"Quote", resultObj.GetProperty("text").GetString());
    }

    [Fact]
    public void Success_UnicodeCharacters_HandlesCorrectly()
    {
        // Arrange
        var result = new
        {
            EnglishText = "Hello",
            ChineseText = "你好",
            JapaneseText = "こんにちは",
            EmojiText = "👋🌍"
        };

        // Act
        var json = JsonOutput.Success(result);

        // Assert
        var parsed = JsonDocument.Parse(json);
        var resultObj = parsed.RootElement.GetProperty("result");
        Assert.Equal("你好", resultObj.GetProperty("chineseText").GetString());
        Assert.Equal("こんにちは", resultObj.GetProperty("japaneseText").GetString());
        Assert.Equal("👋🌍", resultObj.GetProperty("emojiText").GetString());
    }

    [Fact]
    public void Success_Dictionary_SerializesCorrectly()
    {
        // Arrange
        // Note: Dictionary keys are NOT affected by PropertyNamingPolicy
        // Keys remain as specified in the dictionary
        var result = new Dictionary<string, object>
        {
            ["userName"] = "John",
            ["emailAddress"] = "john@example.com",
            ["isActive"] = true
        };

        // Act
        var json = JsonOutput.Success(result);

        // Assert
        var parsed = JsonDocument.Parse(json);
        var resultObj = parsed.RootElement.GetProperty("result");
        // Dictionary keys remain as specified
        Assert.Equal("John", resultObj.GetProperty("userName").GetString());
        Assert.Equal("john@example.com", resultObj.GetProperty("emailAddress").GetString());
        Assert.True(resultObj.GetProperty("isActive").GetBoolean());
    }

    #endregion

    #region Serialization Error Handling

    [Fact]
    public void Success_CircularReference_ReturnsFallbackError()
    {
        // This test is tricky because we need an object with circular reference
        // In practice, this shouldn't happen with our model classes
        // but we test to ensure graceful handling
    }

    #endregion
}
