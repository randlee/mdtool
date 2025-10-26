using Xunit;
using MDTool.Models;

namespace MDTool.Tests.Models;

public class VariableDefinitionTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_CreatesValidRequiredVariable()
    {
        // Act
        var variable = new VariableDefinition("USER_NAME", "The user's full name");

        // Assert
        Assert.Equal("USER_NAME", variable.Name);
        Assert.Equal("The user's full name", variable.Description);
        Assert.True(variable.Required);
        Assert.Null(variable.DefaultValue);
    }

    [Fact]
    public void Constructor_CreatesValidOptionalVariable()
    {
        // Act
        var variable = new VariableDefinition("PORT", "Server port", required: false, defaultValue: 8080);

        // Assert
        Assert.Equal("PORT", variable.Name);
        Assert.Equal("Server port", variable.Description);
        Assert.False(variable.Required);
        Assert.Equal(8080, variable.DefaultValue);
    }

    [Fact]
    public void Constructor_NormalizesNameToUppercase()
    {
        // Act
        var variable = new VariableDefinition("user_name", "Description");

        // Assert
        Assert.Equal("USER_NAME", variable.Name);
    }

    [Fact]
    public void Constructor_ThrowsOnNullName()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new VariableDefinition(null!, "Description"));
    }

    [Fact]
    public void Constructor_ThrowsOnEmptyName()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new VariableDefinition("", "Description"));
    }

    [Fact]
    public void Constructor_ThrowsOnWhitespaceName()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new VariableDefinition("   ", "Description"));
    }

    [Fact]
    public void Constructor_AcceptsNullDescription()
    {
        // Act
        var variable = new VariableDefinition("VAR", null!);

        // Assert
        Assert.Equal(string.Empty, variable.Description);
    }

    [Fact]
    public void Constructor_ThrowsWhenOptionalWithoutDefault()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            new VariableDefinition("VAR", "Description", required: false, defaultValue: null));

        Assert.Contains("Optional variable", exception.Message);
        Assert.Contains("must have a default value", exception.Message);
    }

    #endregion

    #region Variable Name Validation Tests

    [Theory]
    [InlineData("USER_NAME")]
    [InlineData("USER")]
    [InlineData("U")]
    [InlineData("USER_NAME_FULL")]
    [InlineData("USER123")]
    [InlineData("USER_123_NAME")]
    [InlineData("A1B2C3")]
    [InlineData("USER_NAME_123")]
    public void Constructor_AcceptsValidNames(string name)
    {
        // Act
        var variable = new VariableDefinition(name, "Description");

        // Assert
        Assert.Equal(name.ToUpperInvariant(), variable.Name);
    }

    [Theory]
    [InlineData("USER.EMAIL")]
    [InlineData("USER.PROFILE.NAME")]
    [InlineData("API.KEY")]
    [InlineData("A.B")]
    [InlineData("USER_NAME.EMAIL_ADDRESS")]
    public void Constructor_AcceptsValidDotNotation(string name)
    {
        // Act
        var variable = new VariableDefinition(name, "Description");

        // Assert
        Assert.Equal(name.ToUpperInvariant(), variable.Name);
    }

    [Theory]
    [InlineData("User-Name")]           // hyphen
    [InlineData("USER NAME")]           // space
    [InlineData("123USER")]             // starts with digit
    [InlineData("_USER")]               // starts with underscore
    [InlineData("USER@NAME")]           // special character
    [InlineData("USER$NAME")]           // special character
    [InlineData("USER-NAME")]           // hyphen
    [InlineData("USER.")]               // ends with dot
    [InlineData(".USER")]               // starts with dot
    [InlineData("USER..NAME")]          // double dot
    [InlineData("USER._NAME")]          // segment starts with underscore
    [InlineData("USER.123")]            // segment starts with digit
    public void Constructor_ThrowsOnInvalidNames(string name)
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            new VariableDefinition(name, "Description"));

        Assert.Contains("must be uppercase snake-case", exception.Message);
    }

    [Theory]
    [InlineData("user_name", "USER_NAME")]          // lowercase normalizes
    [InlineData("userName", "USERNAME")]            // camelCase normalizes
    [InlineData("user.email", "USER.EMAIL")]        // lowercase with dot normalizes
    public void Constructor_NormalizesToUppercase(string input, string expected)
    {
        // Act
        var variable = new VariableDefinition(input, "Description");

        // Assert
        Assert.Equal(expected, variable.Name);
    }

    #endregion

    #region Factory Method Tests

    [Fact]
    public void Required_CreatesRequiredVariable()
    {
        // Act
        var variable = VariableDefinition.RequiredVariable("USER_NAME", "The user's full name");

        // Assert
        Assert.Equal("USER_NAME", variable.Name);
        Assert.Equal("The user's full name", variable.Description);
        Assert.True(variable.Required);
        Assert.Null(variable.DefaultValue);
    }

    [Fact]
    public void Optional_CreatesOptionalVariable_WithStringDefault()
    {
        // Act
        var variable = VariableDefinition.OptionalVariable("BRANCH", "Git branch", "main");

        // Assert
        Assert.Equal("BRANCH", variable.Name);
        Assert.Equal("Git branch", variable.Description);
        Assert.False(variable.Required);
        Assert.Equal("main", variable.DefaultValue);
    }

    [Fact]
    public void Optional_CreatesOptionalVariable_WithIntDefault()
    {
        // Act
        var variable = VariableDefinition.OptionalVariable("PORT", "Server port", 8080);

        // Assert
        Assert.Equal("PORT", variable.Name);
        Assert.False(variable.Required);
        Assert.Equal(8080, variable.DefaultValue);
    }

    [Fact]
    public void Optional_CreatesOptionalVariable_WithBoolDefault()
    {
        // Act
        var variable = VariableDefinition.OptionalVariable("DEBUG", "Debug mode", false);

        // Assert
        Assert.Equal("DEBUG", variable.Name);
        Assert.False(variable.Required);
        Assert.Equal(false, variable.DefaultValue);
    }

    #endregion

    #region Type Inference Tests

    [Fact]
    public void InferredType_ReturnsNullForRequiredVariable()
    {
        // Arrange
        var variable = VariableDefinition.RequiredVariable("VAR", "Description");

        // Act & Assert
        Assert.Null(variable.InferredType);
    }

    [Fact]
    public void InferredType_ReturnsStringType()
    {
        // Arrange
        var variable = VariableDefinition.OptionalVariable("VAR", "Description", "default");

        // Act & Assert
        Assert.Equal(typeof(string), variable.InferredType);
    }

    [Fact]
    public void InferredType_ReturnsIntType()
    {
        // Arrange
        var variable = VariableDefinition.OptionalVariable("VAR", "Description", 42);

        // Act & Assert
        Assert.Equal(typeof(int), variable.InferredType);
    }

    [Fact]
    public void InferredType_ReturnsBoolType()
    {
        // Arrange
        var variable = VariableDefinition.OptionalVariable("VAR", "Description", true);

        // Act & Assert
        Assert.Equal(typeof(bool), variable.InferredType);
    }

    [Fact]
    public void TypeName_ReturnsStringForRequiredVariable()
    {
        // Arrange
        var variable = VariableDefinition.RequiredVariable("VAR", "Description");

        // Act & Assert
        Assert.Equal("string", variable.TypeName);
    }

    [Fact]
    public void TypeName_ReturnsCorrectTypeName_String()
    {
        // Arrange
        var variable = VariableDefinition.OptionalVariable("VAR", "Description", "default");

        // Act & Assert
        Assert.Equal("String", variable.TypeName);
    }

    [Fact]
    public void TypeName_ReturnsCorrectTypeName_Int32()
    {
        // Arrange
        var variable = VariableDefinition.OptionalVariable("VAR", "Description", 42);

        // Act & Assert
        Assert.Equal("Int32", variable.TypeName);
    }

    [Fact]
    public void TypeName_ReturnsCorrectTypeName_Boolean()
    {
        // Arrange
        var variable = VariableDefinition.OptionalVariable("VAR", "Description", true);

        // Act & Assert
        Assert.Equal("Boolean", variable.TypeName);
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public void Constructor_AcceptsEmptyDescription()
    {
        // Act
        var variable = new VariableDefinition("VAR", "");

        // Assert
        Assert.Equal(string.Empty, variable.Description);
    }

    [Fact]
    public void Optional_WithComplexObject()
    {
        // Arrange
        var defaultValue = new { name = "test", value = 123 };

        // Act
        var variable = VariableDefinition.OptionalVariable("VAR", "Description", defaultValue);

        // Assert
        Assert.False(variable.Required);
        Assert.NotNull(variable.DefaultValue);
        Assert.Equal(defaultValue, variable.DefaultValue);
    }

    [Fact]
    public void Constructor_PreservesDefaultValueReference()
    {
        // Arrange
        var list = new List<string> { "a", "b", "c" };

        // Act
        var variable = VariableDefinition.OptionalVariable("VAR", "Description", list);

        // Assert
        Assert.Same(list, variable.DefaultValue);
    }

    [Fact]
    public void Constructor_VeryLongName_IsValid()
    {
        // Arrange
        var longName = "A" + new string('_', 100) + "B";

        // Act
        var variable = new VariableDefinition(longName, "Description");

        // Assert
        Assert.Equal(longName, variable.Name);
    }

    [Fact]
    public void Constructor_VeryLongDotNotationName_IsValid()
    {
        // Arrange
        var segments = Enumerable.Repeat("SEGMENT", 10);
        var longName = string.Join(".", segments);

        // Act
        var variable = new VariableDefinition(longName, "Description");

        // Assert
        Assert.Equal(longName, variable.Name);
    }

    #endregion
}
