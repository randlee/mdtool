using Xunit;
using MDTool.Models;

namespace MDTool.Tests.Models;

public class ProcessingResultTests
{
    #region Generic ProcessingResult<T> Tests

    [Fact]
    public void Ok_CreatesSuccessfulResult()
    {
        // Act
        var result = ProcessingResult<int>.Ok(42);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(42, result.Value);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Ok_ThrowsOnNullValue()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => ProcessingResult<string>.Ok(null!));
    }

    [Fact]
    public void Fail_WithErrors_CreatesFailedResult()
    {
        // Arrange
        var errors = new List<ValidationError>
        {
            ValidationError.FileNotFound("test.md"),
            ValidationError.InvalidJson("Bad JSON")
        };

        // Act
        var result = ProcessingResult<string>.Fail(errors);

        // Assert
        Assert.False(result.Success);
        Assert.Null(result.Value);
        Assert.Equal(2, result.Errors.Count);
    }

    [Fact]
    public void Fail_WithSingleError_CreatesFailedResult()
    {
        // Arrange
        var error = ValidationError.FileNotFound("test.md");

        // Act
        var result = ProcessingResult<string>.Fail(error);

        // Assert
        Assert.False(result.Success);
        Assert.Single(result.Errors);
        Assert.Equal(ErrorType.FileNotFound, result.Errors[0].Type);
    }

    [Fact]
    public void Fail_ThrowsOnEmptyErrors()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => ProcessingResult<string>.Fail(new List<ValidationError>()));
    }

    [Fact]
    public void Fail_ThrowsOnNullErrors()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => ProcessingResult<string>.Fail((List<ValidationError>)null!));
    }

    [Fact]
    public void FromValidation_CreatesFailedResultFromValidation()
    {
        // Arrange
        var error = ValidationError.MissingVariable("VAR", "Description");
        var validation = ValidationResult.Fail(error);

        // Act
        var result = ProcessingResult<string>.FromValidation(validation);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(validation.Errors, result.Errors);
    }

    [Fact]
    public void FromValidation_ThrowsOnSuccessfulValidation()
    {
        // Arrange
        var validation = ValidationResult.Ok(new List<string> { "VAR1" });

        // Act & Assert
        Assert.Throws<ArgumentException>(() => ProcessingResult<string>.FromValidation(validation));
    }

    [Fact]
    public void Map_TransformsValueOnSuccess()
    {
        // Arrange
        var result = ProcessingResult<int>.Ok(42);

        // Act
        var mapped = result.Map(x => x.ToString());

        // Assert
        Assert.True(mapped.Success);
        Assert.Equal("42", mapped.Value);
    }

    [Fact]
    public void Map_PropagatesErrorsOnFailure()
    {
        // Arrange
        var error = ValidationError.FileNotFound("test.md");
        var result = ProcessingResult<int>.Fail(error);

        // Act
        var mapped = result.Map(x => x.ToString());

        // Assert
        Assert.False(mapped.Success);
        Assert.Single(mapped.Errors);
        Assert.Equal(ErrorType.FileNotFound, mapped.Errors[0].Type);
    }

    [Fact]
    public void Map_CatchesExceptionsAndReturnsError()
    {
        // Arrange
        var result = ProcessingResult<int>.Ok(42);

        // Act
        var mapped = result.Map<string>(x => throw new InvalidOperationException("Test error"));

        // Assert
        Assert.False(mapped.Success);
        Assert.Single(mapped.Errors);
        Assert.Equal(ErrorType.ProcessingError, mapped.Errors[0].Type);
        Assert.Contains("Test error", mapped.Errors[0].Description);
    }

    [Fact]
    public void Bind_ChainsOperationsOnSuccess()
    {
        // Arrange
        var result = ProcessingResult<int>.Ok(42);

        // Act
        var bound = result.Bind(x => ProcessingResult<string>.Ok($"Value: {x}"));

        // Assert
        Assert.True(bound.Success);
        Assert.Equal("Value: 42", bound.Value);
    }

    [Fact]
    public void Bind_PropagatesErrorsFromFirstOperation()
    {
        // Arrange
        var error = ValidationError.FileNotFound("test.md");
        var result = ProcessingResult<int>.Fail(error);

        // Act
        var bound = result.Bind(x => ProcessingResult<string>.Ok($"Value: {x}"));

        // Assert
        Assert.False(bound.Success);
        Assert.Single(bound.Errors);
        Assert.Equal(ErrorType.FileNotFound, bound.Errors[0].Type);
    }

    [Fact]
    public void Bind_PropagatesErrorsFromSecondOperation()
    {
        // Arrange
        var result = ProcessingResult<int>.Ok(42);

        // Act
        var bound = result.Bind(x => ProcessingResult<string>.Fail(
            ValidationError.ProcessingError("Failed in second operation")
        ));

        // Assert
        Assert.False(bound.Success);
        Assert.Single(bound.Errors);
        Assert.Contains("Failed in second operation", bound.Errors[0].Description);
    }

    [Fact]
    public void GetValueOrThrow_ReturnsValueOnSuccess()
    {
        // Arrange
        var result = ProcessingResult<int>.Ok(42);

        // Act
        var value = result.GetValueOrThrow();

        // Assert
        Assert.Equal(42, value);
    }

    [Fact]
    public void GetValueOrThrow_ThrowsOnFailure()
    {
        // Arrange
        var error = ValidationError.FileNotFound("test.md");
        var result = ProcessingResult<int>.Fail(error);

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => result.GetValueOrThrow());
        Assert.Contains("Cannot get value from failed result", exception.Message);
        Assert.Contains("File not found", exception.Message);
    }

    [Fact]
    public void ToJson_SerializesSuccessCorrectly()
    {
        // Arrange
        var result = ProcessingResult<int>.Ok(42);

        // Act
        var json = result.ToJson();

        // Assert
        Assert.Contains("\"success\": true", json);
        Assert.Contains("\"result\": 42", json);
    }

    [Fact]
    public void ToJson_SerializesFailureCorrectly()
    {
        // Arrange
        var error = ValidationError.FileNotFound("test.md");
        var result = ProcessingResult<string>.Fail(error);

        // Act
        var json = result.ToJson();

        // Assert
        Assert.Contains("\"success\": false", json);
        Assert.Contains("\"errors\"", json);
        Assert.Contains("FileNotFound", json);
    }

    #endregion

    #region Non-Generic ProcessingResult Tests

    [Fact]
    public void NonGeneric_Ok_CreatesSuccessfulResult()
    {
        // Act
        var result = ProcessingResult.Ok();

        // Assert
        Assert.True(result.Success);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void NonGeneric_Fail_WithErrors_CreatesFailedResult()
    {
        // Arrange
        var errors = new List<ValidationError>
        {
            ValidationError.FileNotFound("test.md"),
            ValidationError.InvalidJson("Bad JSON")
        };

        // Act
        var result = ProcessingResult.Fail(errors);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(2, result.Errors.Count);
    }

    [Fact]
    public void NonGeneric_Fail_WithSingleError_CreatesFailedResult()
    {
        // Arrange
        var error = ValidationError.FileNotFound("test.md");

        // Act
        var result = ProcessingResult.Fail(error);

        // Assert
        Assert.False(result.Success);
        Assert.Single(result.Errors);
    }

    [Fact]
    public void NonGeneric_Fail_ThrowsOnEmptyErrors()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => ProcessingResult.Fail(new List<ValidationError>()));
    }

    [Fact]
    public void NonGeneric_FromValidation_CreatesFailedResult()
    {
        // Arrange
        var error = ValidationError.MissingVariable("VAR", "Description");
        var validation = ValidationResult.Fail(error);

        // Act
        var result = ProcessingResult.FromValidation(validation);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(validation.Errors, result.Errors);
    }

    [Fact]
    public void NonGeneric_FromValidation_ThrowsOnSuccessfulValidation()
    {
        // Arrange
        var validation = ValidationResult.Ok(new List<string> { "VAR1" });

        // Act & Assert
        Assert.Throws<ArgumentException>(() => ProcessingResult.FromValidation(validation));
    }

    [Fact]
    public void NonGeneric_ToJson_SerializesSuccessCorrectly()
    {
        // Arrange
        var result = ProcessingResult.Ok();

        // Act
        var json = result.ToJson();

        // Assert
        Assert.Contains("\"success\": true", json);
    }

    [Fact]
    public void NonGeneric_ToJson_SerializesFailureCorrectly()
    {
        // Arrange
        var error = ValidationError.FileNotFound("test.md");
        var result = ProcessingResult.Fail(error);

        // Act
        var json = result.ToJson();

        // Assert
        Assert.Contains("\"success\": false", json);
        Assert.Contains("\"errors\"", json);
        Assert.Contains("FileNotFound", json);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void ComplexChaining_MapAndBind()
    {
        // Arrange
        var result = ProcessingResult<int>.Ok(10);

        // Act - chain multiple operations
        var final = result
            .Map(x => x * 2)           // 20
            .Bind(x => ProcessingResult<int>.Ok(x + 5))  // 25
            .Map(x => x.ToString());   // "25"

        // Assert
        Assert.True(final.Success);
        Assert.Equal("25", final.Value);
    }

    [Fact]
    public void ComplexChaining_ErrorPropagation()
    {
        // Arrange
        var result = ProcessingResult<int>.Ok(10);

        // Act - chain with failure in middle
        var final = result
            .Map(x => x * 2)
            .Bind(x => ProcessingResult<int>.Fail(ValidationError.ProcessingError("Mid-chain error")))
            .Map(x => x.ToString());

        // Assert
        Assert.False(final.Success);
        Assert.Single(final.Errors);
        Assert.Contains("Mid-chain error", final.Errors[0].Description);
    }

    #endregion
}
