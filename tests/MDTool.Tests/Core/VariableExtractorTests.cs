using Xunit;
using MDTool.Core;
using MDTool.Models;

namespace MDTool.Tests.Core;

public class VariableExtractorTests
{
    [Fact]
    public void ExtractVariables_SimpleVariable_ReturnsVariable()
    {
        var content = "Hello {{NAME}}!";
        var result = VariableExtractor.ExtractVariables(content);

        Assert.Single(result);
        Assert.Contains("NAME", result);
    }

    [Fact]
    public void ExtractVariables_MultipleVariables_ReturnsAllUnique()
    {
        var content = "{{USER_NAME}} works at {{COMPANY}} in {{CITY}}";
        var result = VariableExtractor.ExtractVariables(content);

        Assert.Equal(3, result.Count);
        Assert.Contains("USER_NAME", result);
        Assert.Contains("COMPANY", result);
        Assert.Contains("CITY", result);
    }

    [Fact]
    public void ExtractVariables_DuplicateVariables_ReturnsUniqueOnly()
    {
        var content = "{{NAME}} is {{NAME}}";
        var result = VariableExtractor.ExtractVariables(content);

        Assert.Single(result);
        Assert.Contains("NAME", result);
    }

    [Fact]
    public void ExtractVariables_NestedDotNotation_ReturnsNestedVariables()
    {
        var content = "{{USER.NAME}} lives in {{USER.ADDRESS.CITY}}";
        var result = VariableExtractor.ExtractVariables(content);

        Assert.Equal(2, result.Count);
        Assert.Contains("USER.NAME", result);
        Assert.Contains("USER.ADDRESS.CITY", result);
    }

    [Fact]
    public void ExtractVariables_MixedSimpleAndNested_ReturnsBoth()
    {
        var content = "{{NAME}} at {{COMPANY.ADDRESS.CITY}}";
        var result = VariableExtractor.ExtractVariables(content);

        Assert.Equal(2, result.Count);
        Assert.Contains("NAME", result);
        Assert.Contains("COMPANY.ADDRESS.CITY", result);
    }

    [Fact]
    public void ExtractVariables_EmptyContent_ReturnsEmptyList()
    {
        var result = VariableExtractor.ExtractVariables("");

        Assert.Empty(result);
    }

    [Fact]
    public void ExtractVariables_NoVariables_ReturnsEmptyList()
    {
        var content = "This is plain text with no variables.";
        var result = VariableExtractor.ExtractVariables(content);

        Assert.Empty(result);
    }

    [Fact]
    public void ExtractVariables_LowercaseVariables_NotMatched()
    {
        var content = "{{name}} and {{email}}";
        var result = VariableExtractor.ExtractVariables(content);

        Assert.Empty(result);
    }

    [Fact]
    public void ExtractVariables_InvalidFormat_NotMatched()
    {
        var content = "{{user-name}} and {{user name}} and {{123}}";
        var result = VariableExtractor.ExtractVariables(content);

        Assert.Empty(result);
    }

    [Fact]
    public void ExtractVariables_SortedOutput_ReturnsSortedList()
    {
        var content = "{{ZEBRA}} {{ALPHA}} {{BETA}}";
        var result = VariableExtractor.ExtractVariables(content);

        Assert.Equal(3, result.Count);
        Assert.Equal("ALPHA", result[0]);
        Assert.Equal("BETA", result[1]);
        Assert.Equal("ZEBRA", result[2]);
    }

    [Fact]
    public void ExtractVariables_MultilineContent_ExtractsAll()
    {
        var content = @"
# Title {{TITLE}}
Content with {{USER.NAME}}
Footer {{DATE}}
";
        var result = VariableExtractor.ExtractVariables(content);

        Assert.Equal(3, result.Count);
        Assert.Contains("TITLE", result);
        Assert.Contains("USER.NAME", result);
        Assert.Contains("DATE", result);
    }

    [Fact]
    public void ValidateVariableFormat_ValidContent_ReturnsSuccess()
    {
        var content = "Hello {{NAME}} at {{COMPANY}}!";
        var result = VariableExtractor.ValidateVariableFormat(content);

        Assert.True(result.Success);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidateVariableFormat_EmptyVariable_ReturnsError()
    {
        var content = "Hello {{}}!";
        var result = VariableExtractor.ValidateVariableFormat(content);

        Assert.False(result.Success);
        Assert.NotEmpty(result.Errors);
        Assert.Contains(result.Errors, e => e.Type == ErrorType.InvalidVariableFormat);
    }

    [Fact]
    public void ValidateVariableFormat_UnclosedBraces_ReturnsError()
    {
        var content = "Hello {{NAME!";
        var result = VariableExtractor.ValidateVariableFormat(content);

        Assert.False(result.Success);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void ValidateVariableFormat_LowercaseVariable_ReturnsError()
    {
        var content = "Hello {{name}}!";
        var result = VariableExtractor.ValidateVariableFormat(content);

        Assert.False(result.Success);
        Assert.NotEmpty(result.Errors);
        Assert.Contains(result.Errors, e => e.Type == ErrorType.InvalidVariableFormat);
    }

    [Fact]
    public void ValidateVariableFormat_InvalidCharacters_ReturnsError()
    {
        var content = "Hello {{USER-NAME}}!";
        var result = VariableExtractor.ValidateVariableFormat(content);

        Assert.False(result.Success);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void ValidateVariableFormat_EmptyContent_ReturnsSuccess()
    {
        var result = VariableExtractor.ValidateVariableFormat("");

        Assert.True(result.Success);
    }

    [Fact]
    public void ValidateVariableFormat_NestedDotNotation_ValidatesCorrectly()
    {
        var content = "{{USER.NAME}} and {{API.KEY}}";
        var result = VariableExtractor.ValidateVariableFormat(content);

        Assert.True(result.Success);
    }

    [Fact]
    public void ValidateVariableFormat_MultipleErrors_CollectsAll()
    {
        var content = @"
{{}} empty
{{name}} lowercase
{{USER-NAME}} invalid chars
";
        var result = VariableExtractor.ValidateVariableFormat(content);

        Assert.False(result.Success);
        Assert.True(result.Errors.Count >= 2); // At least 2 errors
    }

    [Fact]
    public void ExtractVariables_WithUnderscores_ExtractsCorrectly()
    {
        var content = "{{USER_NAME}} and {{API_KEY}}";
        var result = VariableExtractor.ExtractVariables(content);

        Assert.Equal(2, result.Count);
        Assert.Contains("USER_NAME", result);
        Assert.Contains("API_KEY", result);
    }

    [Fact]
    public void ExtractVariables_WithNumbers_ExtractsCorrectly()
    {
        var content = "{{PORT8080}} and {{VERSION2}}";
        var result = VariableExtractor.ExtractVariables(content);

        Assert.Equal(2, result.Count);
        Assert.Contains("PORT8080", result);
        Assert.Contains("VERSION2", result);
    }

    [Fact]
    public void ExtractVariables_StartsWithNumber_NotMatched()
    {
        var content = "{{8080PORT}}";
        var result = VariableExtractor.ExtractVariables(content);

        Assert.Empty(result);
    }

    [Fact]
    public void ExtractVariables_DeepNesting_ExtractsCorrectly()
    {
        var content = "{{A.B.C.D.E}}";
        var result = VariableExtractor.ExtractVariables(content);

        Assert.Single(result);
        Assert.Contains("A.B.C.D.E", result);
    }
}
