using Xunit;
using MDTool.Models;

namespace MDTool.Tests.Models;

public class ValidationResultTests
{
    #region Ok Factory Method Tests

    [Fact]
    public void Ok_CreatesSuccessfulResult()
    {
        // Arrange
        var provided = new List<string> { "VAR1", "VAR2", "VAR3" };

        // Act
        var result = ValidationResult.Ok(provided);

        // Assert
        Assert.True(result.Success);
        Assert.Empty(result.Errors);
        Assert.Equal(3, result.ProvidedVariables.Count);
        Assert.Empty(result.MissingVariables);
    }

    [Fact]
    public void Ok_AcceptsEmptyProvidedList()
    {
        // Arrange
        var provided = new List<string>();

        // Act
        var result = ValidationResult.Ok(provided);

        // Assert
        Assert.True(result.Success);
        Assert.Empty(result.ProvidedVariables);
    }

    [Fact]
    public void Ok_AcceptsNullProvidedList()
    {
        // Act
        var result = ValidationResult.Ok(null!);

        // Assert
        Assert.True(result.Success);
        Assert.Empty(result.ProvidedVariables);
    }

    #endregion

    #region Fail Factory Method Tests (Multiple Errors)

    [Fact]
    public void Fail_WithMultipleErrors_CreatesFailedResult()
    {
        // Arrange
        var errors = new List<ValidationError>
        {
            ValidationError.MissingVariable("VAR1", "Description 1"),
            ValidationError.MissingVariable("VAR2", "Description 2")
        };
        var provided = new List<string> { "VAR3" };
        var missing = new List<string> { "VAR1", "VAR2" };

        // Act
        var result = ValidationResult.Fail(errors, provided, missing);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(2, result.Errors.Count);
        Assert.Single(result.ProvidedVariables);
        Assert.Equal(2, result.MissingVariables.Count);
    }

    [Fact]
    public void Fail_ThrowsOnEmptyErrors()
    {
        // Arrange
        var errors = new List<ValidationError>();

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            ValidationResult.Fail(errors, new List<string>(), new List<string>()));
    }

    [Fact]
    public void Fail_ThrowsOnNullErrors()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            ValidationResult.Fail(null!, new List<string>(), new List<string>()));
    }

    [Fact]
    public void Fail_AcceptsNullProvidedList()
    {
        // Arrange
        var errors = new List<ValidationError>
        {
            ValidationError.MissingVariable("VAR1", "Description")
        };

        // Act
        var result = ValidationResult.Fail(errors, null!, new List<string> { "VAR1" });

        // Assert
        Assert.False(result.Success);
        Assert.Empty(result.ProvidedVariables);
    }

    [Fact]
    public void Fail_AcceptsNullMissingList()
    {
        // Arrange
        var errors = new List<ValidationError>
        {
            ValidationError.ProcessingError("Error")
        };

        // Act
        var result = ValidationResult.Fail(errors, new List<string>(), null!);

        // Assert
        Assert.False(result.Success);
        Assert.Empty(result.MissingVariables);
    }

    #endregion

    #region Fail Factory Method Tests (Single Error)

    [Fact]
    public void Fail_WithSingleError_CreatesFailedResult()
    {
        // Arrange
        var error = ValidationError.MissingVariable("VAR1", "Description");

        // Act
        var result = ValidationResult.Fail(error);

        // Assert
        Assert.False(result.Success);
        Assert.Single(result.Errors);
        Assert.Equal("VAR1", result.Errors[0].Variable);
        Assert.Contains("VAR1", result.MissingVariables);
    }

    [Fact]
    public void Fail_WithSingleError_HandlesErrorWithoutVariable()
    {
        // Arrange
        var error = ValidationError.ProcessingError("Generic error");

        // Act
        var result = ValidationResult.Fail(error);

        // Assert
        Assert.False(result.Success);
        Assert.Single(result.Errors);
        Assert.Contains(string.Empty, result.MissingVariables);
    }

    #endregion

    #region Combine Method Tests

    [Fact]
    public void Combine_AllSuccessful_ReturnsSuccess()
    {
        // Arrange
        var result1 = ValidationResult.Ok(new List<string> { "VAR1" });
        var result2 = ValidationResult.Ok(new List<string> { "VAR2" });
        var result3 = ValidationResult.Ok(new List<string> { "VAR3" });

        // Act
        var combined = ValidationResult.Combine(result1, result2, result3);

        // Assert
        Assert.True(combined.Success);
        Assert.Empty(combined.Errors);
        Assert.Equal(3, combined.ProvidedVariables.Count);
        Assert.Contains("VAR1", combined.ProvidedVariables);
        Assert.Contains("VAR2", combined.ProvidedVariables);
        Assert.Contains("VAR3", combined.ProvidedVariables);
    }

    [Fact]
    public void Combine_OneFailure_ReturnsFailure()
    {
        // Arrange
        var result1 = ValidationResult.Ok(new List<string> { "VAR1" });
        var result2 = ValidationResult.Fail(
            ValidationError.MissingVariable("VAR2", "Description")
        );
        var result3 = ValidationResult.Ok(new List<string> { "VAR3" });

        // Act
        var combined = ValidationResult.Combine(result1, result2, result3);

        // Assert
        Assert.False(combined.Success);
        Assert.Single(combined.Errors);
        Assert.Equal(2, combined.ProvidedVariables.Count);
        Assert.Contains("VAR1", combined.ProvidedVariables);
        Assert.Contains("VAR3", combined.ProvidedVariables);
        Assert.Contains("VAR2", combined.MissingVariables);
    }

    [Fact]
    public void Combine_MultipleFailures_AggregatesErrors()
    {
        // Arrange
        var result1 = ValidationResult.Fail(
            ValidationError.MissingVariable("VAR1", "Description 1")
        );
        var result2 = ValidationResult.Fail(
            ValidationError.MissingVariable("VAR2", "Description 2")
        );
        var result3 = ValidationResult.Fail(
            ValidationError.MissingVariable("VAR3", "Description 3")
        );

        // Act
        var combined = ValidationResult.Combine(result1, result2, result3);

        // Assert
        Assert.False(combined.Success);
        Assert.Equal(3, combined.Errors.Count);
        Assert.Equal(3, combined.MissingVariables.Count);
    }

    [Fact]
    public void Combine_DeduplicatesProvidedVariables()
    {
        // Arrange
        var result1 = ValidationResult.Ok(new List<string> { "VAR1", "VAR2" });
        var result2 = ValidationResult.Ok(new List<string> { "VAR2", "VAR3" });
        var result3 = ValidationResult.Ok(new List<string> { "VAR3", "VAR1" });

        // Act
        var combined = ValidationResult.Combine(result1, result2, result3);

        // Assert
        Assert.True(combined.Success);
        Assert.Equal(3, combined.ProvidedVariables.Count);
        Assert.Contains("VAR1", combined.ProvidedVariables);
        Assert.Contains("VAR2", combined.ProvidedVariables);
        Assert.Contains("VAR3", combined.ProvidedVariables);
    }

    [Fact]
    public void Combine_DeduplicatesMissingVariables()
    {
        // Arrange
        var result1 = ValidationResult.Fail(
            new List<ValidationError>
            {
                ValidationError.MissingVariable("VAR1", "Desc")
            },
            new List<string>(),
            new List<string> { "VAR1" }
        );
        var result2 = ValidationResult.Fail(
            new List<ValidationError>
            {
                ValidationError.MissingVariable("VAR1", "Desc")
            },
            new List<string>(),
            new List<string> { "VAR1" }
        );

        // Act
        var combined = ValidationResult.Combine(result1, result2);

        // Assert
        Assert.False(combined.Success);
        Assert.Equal(2, combined.Errors.Count); // Errors not deduplicated
        Assert.Single(combined.MissingVariables); // Missing deduplicated
    }

    [Fact]
    public void Combine_EmptyArray_ReturnsSuccess()
    {
        // Act
        var combined = ValidationResult.Combine();

        // Assert
        Assert.True(combined.Success);
        Assert.Empty(combined.Errors);
        Assert.Empty(combined.ProvidedVariables);
    }

    #endregion

    #region GetSummary Method Tests

    [Fact]
    public void GetSummary_SuccessfulValidation()
    {
        // Arrange
        var result = ValidationResult.Ok(new List<string> { "VAR1", "VAR2", "VAR3" });

        // Act
        var summary = result.GetSummary();

        // Assert
        Assert.Contains("Validation successful", summary);
        Assert.Contains("3 variables provided", summary);
    }

    [Fact]
    public void GetSummary_FailedValidation()
    {
        // Arrange
        var errors = new List<ValidationError>
        {
            ValidationError.MissingVariable("VAR1", "Desc 1"),
            ValidationError.MissingVariable("VAR2", "Desc 2")
        };
        var result = ValidationResult.Fail(
            errors,
            new List<string>(),
            new List<string> { "VAR1", "VAR2" }
        );

        // Act
        var summary = result.GetSummary();

        // Assert
        Assert.Contains("Validation failed", summary);
        Assert.Contains("2 error(s)", summary);
        Assert.Contains("2 missing variable(s)", summary);
    }

    [Fact]
    public void GetSummary_NoVariablesProvided()
    {
        // Arrange
        var result = ValidationResult.Ok(new List<string>());

        // Act
        var summary = result.GetSummary();

        // Assert
        Assert.Contains("0 variables provided", summary);
    }

    #endregion

    #region ToJson Method Tests

    [Fact]
    public void ToJson_SuccessfulValidation_ReturnsCorrectFormat()
    {
        // Arrange
        var provided = new List<string> { "VAR1", "VAR2" };
        var result = ValidationResult.Ok(provided);

        // Act
        var json = result.ToJson();

        // Assert
        Assert.Contains("\"success\": true", json);
        Assert.Contains("\"provided\"", json);
        Assert.Contains("VAR1", json);
        Assert.Contains("VAR2", json);
        Assert.Contains("\"missing\"", json);
    }

    [Fact]
    public void ToJson_FailedValidation_ReturnsCorrectFormat()
    {
        // Arrange
        var error = ValidationError.MissingVariable("VAR1", "Description");
        var result = ValidationResult.Fail(error);

        // Act
        var json = result.ToJson();

        // Assert
        Assert.Contains("\"success\": false", json);
        Assert.Contains("\"errors\"", json);
        Assert.Contains("MissingRequiredVariable", json);
        Assert.Contains("VAR1", json);
        Assert.Contains("Description", json);
    }

    [Fact]
    public void ToJson_MultipleErrors_IncludesAllErrors()
    {
        // Arrange
        var errors = new List<ValidationError>
        {
            ValidationError.MissingVariable("VAR1", "Desc 1"),
            ValidationError.MissingVariable("VAR2", "Desc 2"),
            ValidationError.InvalidFormat("VAR3", "Bad format", 5)
        };
        var result = ValidationResult.Fail(
            errors,
            new List<string>(),
            new List<string> { "VAR1", "VAR2" }
        );

        // Act
        var json = result.ToJson();

        // Assert
        Assert.Contains("VAR1", json);
        Assert.Contains("VAR2", json);
        Assert.Contains("VAR3", json);
        Assert.Contains("MissingRequiredVariable", json);
        Assert.Contains("InvalidVariableFormat", json);
        Assert.Contains("\"line\": 5", json);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void CompleteWorkflow_AllVariablesProvided()
    {
        // Arrange
        var provided = new List<string> { "USER_NAME", "EMAIL", "PORT" };

        // Act
        var result = ValidationResult.Ok(provided);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(3, result.ProvidedVariables.Count);
        Assert.Empty(result.MissingVariables);

        var summary = result.GetSummary();
        Assert.Contains("successful", summary);

        var json = result.ToJson();
        Assert.Contains("\"success\": true", json);
    }

    [Fact]
    public void CompleteWorkflow_SomeMissingVariables()
    {
        // Arrange
        var errors = new List<ValidationError>
        {
            ValidationError.MissingVariable("USER_NAME", "User's name"),
            ValidationError.MissingVariable("EMAIL", "User's email")
        };
        var provided = new List<string> { "PORT" };
        var missing = new List<string> { "USER_NAME", "EMAIL" };

        // Act
        var result = ValidationResult.Fail(errors, provided, missing);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(2, result.Errors.Count);
        Assert.Single(result.ProvidedVariables);
        Assert.Equal(2, result.MissingVariables.Count);

        var summary = result.GetSummary();
        Assert.Contains("failed", summary);
        Assert.Contains("2 error(s)", summary);

        var json = result.ToJson();
        Assert.Contains("\"success\": false", json);
        Assert.Contains("USER_NAME", json);
        Assert.Contains("EMAIL", json);
    }

    [Fact]
    public void CompleteWorkflow_CombiningResults()
    {
        // Arrange - simulate multiple validation steps
        var step1 = ValidationResult.Ok(new List<string> { "VAR1", "VAR2" });
        var step2 = ValidationResult.Ok(new List<string> { "VAR3" });
        var step3 = ValidationResult.Fail(
            ValidationError.MissingVariable("VAR4", "Missing variable")
        );

        // Act
        var combined = ValidationResult.Combine(step1, step2, step3);

        // Assert
        Assert.False(combined.Success);
        Assert.Single(combined.Errors);
        Assert.Equal(3, combined.ProvidedVariables.Count);
        Assert.Contains("VAR1", combined.ProvidedVariables);
        Assert.Contains("VAR2", combined.ProvidedVariables);
        Assert.Contains("VAR3", combined.ProvidedVariables);
        Assert.Contains("VAR4", combined.MissingVariables);

        var summary = combined.GetSummary();
        Assert.Contains("failed", summary);
    }

    #endregion
}
