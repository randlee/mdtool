using Xunit;
using MDTool.Models;

namespace MDTool.Tests.Models;

public class MarkdownDocumentTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_CreatesValidDocument()
    {
        // Arrange
        var variables = new Dictionary<string, VariableDefinition>
        {
            ["USER_NAME"] = VariableDefinition.RequiredVariable("USER_NAME", "User's name"),
            ["EMAIL"] = VariableDefinition.RequiredVariable("EMAIL", "User's email")
        };
        var content = "# Hello {{USER_NAME}}";
        var rawYaml = "---\nvariables:\n  USER_NAME: \"User's name\"\n---";

        // Act
        var doc = new MarkdownDocument(variables, content, rawYaml);

        // Assert
        Assert.Equal(2, doc.Variables.Count);
        Assert.Equal(content, doc.Content);
        Assert.Equal(rawYaml, doc.RawYaml);
    }

    [Fact]
    public void Constructor_ThrowsOnNullVariables()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new MarkdownDocument(null!, "content"));
    }

    [Fact]
    public void Constructor_ThrowsOnNullContent()
    {
        // Arrange
        var variables = new Dictionary<string, VariableDefinition>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new MarkdownDocument(variables, null!));
    }

    [Fact]
    public void Constructor_AcceptsNullRawYaml()
    {
        // Arrange
        var variables = new Dictionary<string, VariableDefinition>();
        var content = "# Hello";

        // Act
        var doc = new MarkdownDocument(variables, content, null);

        // Assert
        Assert.Null(doc.RawYaml);
    }

    [Fact]
    public void Constructor_AcceptsEmptyVariables()
    {
        // Arrange
        var variables = new Dictionary<string, VariableDefinition>();
        var content = "# Hello";

        // Act
        var doc = new MarkdownDocument(variables, content);

        // Assert
        Assert.Empty(doc.Variables);
    }

    [Fact]
    public void Constructor_AcceptsEmptyContent()
    {
        // Arrange
        var variables = new Dictionary<string, VariableDefinition>();

        // Act
        var doc = new MarkdownDocument(variables, string.Empty);

        // Assert
        Assert.Equal(string.Empty, doc.Content);
    }

    #endregion

    #region Empty Factory Method Tests

    [Fact]
    public void Empty_CreatesDocumentWithNoVariables()
    {
        // Act
        var doc = MarkdownDocument.Empty("# Simple document");

        // Assert
        Assert.Empty(doc.Variables);
        Assert.Equal("# Simple document", doc.Content);
        Assert.Null(doc.RawYaml);
    }

    [Fact]
    public void Empty_AcceptsEmptyContent()
    {
        // Act
        var doc = MarkdownDocument.Empty("");

        // Assert
        Assert.Empty(doc.Variables);
        Assert.Equal(string.Empty, doc.Content);
    }

    #endregion

    #region RequiredVariables Property Tests

    [Fact]
    public void RequiredVariables_ReturnsOnlyRequiredVariables()
    {
        // Arrange
        var variables = new Dictionary<string, VariableDefinition>
        {
            ["USER_NAME"] = VariableDefinition.RequiredVariable("USER_NAME", "Required var"),
            ["EMAIL"] = VariableDefinition.RequiredVariable("EMAIL", "Required var"),
            ["PORT"] = VariableDefinition.OptionalVariable("PORT", "Optional var", 8080),
            ["DEBUG"] = VariableDefinition.OptionalVariable("DEBUG", "Optional var", false)
        };
        var doc = new MarkdownDocument(variables, "content");

        // Act
        var required = doc.RequiredVariables.ToList();

        // Assert
        Assert.Equal(2, required.Count);
        Assert.Contains(required, v => v.Name == "USER_NAME");
        Assert.Contains(required, v => v.Name == "EMAIL");
    }

    [Fact]
    public void RequiredVariables_ReturnsEmptyWhenAllOptional()
    {
        // Arrange
        var variables = new Dictionary<string, VariableDefinition>
        {
            ["PORT"] = VariableDefinition.OptionalVariable("PORT", "Optional var", 8080),
            ["DEBUG"] = VariableDefinition.OptionalVariable("DEBUG", "Optional var", false)
        };
        var doc = new MarkdownDocument(variables, "content");

        // Act
        var required = doc.RequiredVariables.ToList();

        // Assert
        Assert.Empty(required);
    }

    [Fact]
    public void RequiredVariables_ReturnsEmptyWhenNoVariables()
    {
        // Arrange
        var doc = MarkdownDocument.Empty("content");

        // Act
        var required = doc.RequiredVariables.ToList();

        // Assert
        Assert.Empty(required);
    }

    #endregion

    #region OptionalVariables Property Tests

    [Fact]
    public void OptionalVariables_ReturnsOnlyOptionalVariables()
    {
        // Arrange
        var variables = new Dictionary<string, VariableDefinition>
        {
            ["USER_NAME"] = VariableDefinition.RequiredVariable("USER_NAME", "Required var"),
            ["EMAIL"] = VariableDefinition.RequiredVariable("EMAIL", "Required var"),
            ["PORT"] = VariableDefinition.OptionalVariable("PORT", "Optional var", 8080),
            ["DEBUG"] = VariableDefinition.OptionalVariable("DEBUG", "Optional var", false)
        };
        var doc = new MarkdownDocument(variables, "content");

        // Act
        var optional = doc.OptionalVariables.ToList();

        // Assert
        Assert.Equal(2, optional.Count);
        Assert.Contains(optional, v => v.Name == "PORT");
        Assert.Contains(optional, v => v.Name == "DEBUG");
    }

    [Fact]
    public void OptionalVariables_ReturnsEmptyWhenAllRequired()
    {
        // Arrange
        var variables = new Dictionary<string, VariableDefinition>
        {
            ["USER_NAME"] = VariableDefinition.RequiredVariable("USER_NAME", "Required var"),
            ["EMAIL"] = VariableDefinition.RequiredVariable("EMAIL", "Required var")
        };
        var doc = new MarkdownDocument(variables, "content");

        // Act
        var optional = doc.OptionalVariables.ToList();

        // Assert
        Assert.Empty(optional);
    }

    [Fact]
    public void OptionalVariables_ReturnsEmptyWhenNoVariables()
    {
        // Arrange
        var doc = MarkdownDocument.Empty("content");

        // Act
        var optional = doc.OptionalVariables.ToList();

        // Assert
        Assert.Empty(optional);
    }

    #endregion

    #region HasVariable Method Tests

    [Fact]
    public void HasVariable_ReturnsTrueForExistingVariable()
    {
        // Arrange
        var variables = new Dictionary<string, VariableDefinition>
        {
            ["USER_NAME"] = VariableDefinition.RequiredVariable("USER_NAME", "User's name")
        };
        var doc = new MarkdownDocument(variables, "content");

        // Act & Assert
        Assert.True(doc.HasVariable("USER_NAME"));
    }

    [Fact]
    public void HasVariable_IsCaseInsensitive()
    {
        // Arrange
        var variables = new Dictionary<string, VariableDefinition>
        {
            ["USER_NAME"] = VariableDefinition.RequiredVariable("USER_NAME", "User's name")
        };
        var doc = new MarkdownDocument(variables, "content");

        // Act & Assert
        Assert.True(doc.HasVariable("user_name"));
        Assert.True(doc.HasVariable("User_Name"));
        Assert.True(doc.HasVariable("USER_NAME"));
    }

    [Fact]
    public void HasVariable_ReturnsFalseForNonExistingVariable()
    {
        // Arrange
        var variables = new Dictionary<string, VariableDefinition>
        {
            ["USER_NAME"] = VariableDefinition.RequiredVariable("USER_NAME", "User's name")
        };
        var doc = new MarkdownDocument(variables, "content");

        // Act & Assert
        Assert.False(doc.HasVariable("EMAIL"));
    }

    [Fact]
    public void HasVariable_ReturnsFalseForEmptyDocument()
    {
        // Arrange
        var doc = MarkdownDocument.Empty("content");

        // Act & Assert
        Assert.False(doc.HasVariable("USER_NAME"));
    }

    #endregion

    #region GetVariable Method Tests

    [Fact]
    public void GetVariable_ReturnsVariableIfExists()
    {
        // Arrange
        var variable = VariableDefinition.RequiredVariable("USER_NAME", "User's name");
        var variables = new Dictionary<string, VariableDefinition>
        {
            ["USER_NAME"] = variable
        };
        var doc = new MarkdownDocument(variables, "content");

        // Act
        var result = doc.GetVariable("USER_NAME");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("USER_NAME", result.Name);
        Assert.Equal("User's name", result.Description);
    }

    [Fact]
    public void GetVariable_IsCaseInsensitive()
    {
        // Arrange
        var variable = VariableDefinition.RequiredVariable("USER_NAME", "User's name");
        var variables = new Dictionary<string, VariableDefinition>
        {
            ["USER_NAME"] = variable
        };
        var doc = new MarkdownDocument(variables, "content");

        // Act
        var result1 = doc.GetVariable("user_name");
        var result2 = doc.GetVariable("User_Name");
        var result3 = doc.GetVariable("USER_NAME");

        // Assert
        Assert.NotNull(result1);
        Assert.NotNull(result2);
        Assert.NotNull(result3);
        Assert.Equal("USER_NAME", result1.Name);
        Assert.Equal("USER_NAME", result2.Name);
        Assert.Equal("USER_NAME", result3.Name);
    }

    [Fact]
    public void GetVariable_ReturnsNullIfNotExists()
    {
        // Arrange
        var variables = new Dictionary<string, VariableDefinition>
        {
            ["USER_NAME"] = VariableDefinition.RequiredVariable("USER_NAME", "User's name")
        };
        var doc = new MarkdownDocument(variables, "content");

        // Act
        var result = doc.GetVariable("EMAIL");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetVariable_ReturnsNullForEmptyDocument()
    {
        // Arrange
        var doc = MarkdownDocument.Empty("content");

        // Act
        var result = doc.GetVariable("USER_NAME");

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void Document_WithDotNotationVariables()
    {
        // Arrange
        var variables = new Dictionary<string, VariableDefinition>
        {
            ["USER.NAME"] = VariableDefinition.RequiredVariable("USER.NAME", "User name"),
            ["USER.EMAIL"] = VariableDefinition.RequiredVariable("USER.EMAIL", "User email"),
            ["API.KEY"] = VariableDefinition.OptionalVariable("API.KEY", "API key", "default-key")
        };
        var doc = new MarkdownDocument(variables, "content");

        // Act & Assert
        Assert.Equal(3, doc.Variables.Count);
        Assert.True(doc.HasVariable("USER.NAME"));
        Assert.True(doc.HasVariable("user.email"));
        Assert.NotNull(doc.GetVariable("API.KEY"));

        var required = doc.RequiredVariables.ToList();
        var optional = doc.OptionalVariables.ToList();

        Assert.Equal(2, required.Count);
        Assert.Single(optional);
    }

    [Fact]
    public void Document_WithComplexContent()
    {
        // Arrange
        var variables = new Dictionary<string, VariableDefinition>
        {
            ["TITLE"] = VariableDefinition.RequiredVariable("TITLE", "Document title"),
            ["AUTHOR"] = VariableDefinition.OptionalVariable("AUTHOR", "Document author", "Unknown")
        };
        var content = @"# {{TITLE}}

By {{AUTHOR}}

## Introduction

This is a test document with variables.";

        // Act
        var doc = new MarkdownDocument(variables, content);

        // Assert
        Assert.Equal(2, doc.Variables.Count);
        Assert.Contains("{{TITLE}}", doc.Content);
        Assert.Contains("{{AUTHOR}}", doc.Content);
    }

    [Fact]
    public void Document_WithLargeNumberOfVariables()
    {
        // Arrange
        var variables = new Dictionary<string, VariableDefinition>();
        for (int i = 0; i < 100; i++)
        {
            variables[$"VAR_{i}"] = VariableDefinition.RequiredVariable($"VAR_{i}", $"Variable {i}");
        }
        var doc = new MarkdownDocument(variables, "content");

        // Act & Assert
        Assert.Equal(100, doc.Variables.Count);
        Assert.Equal(100, doc.RequiredVariables.Count());
        Assert.Empty(doc.OptionalVariables);

        for (int i = 0; i < 100; i++)
        {
            Assert.True(doc.HasVariable($"VAR_{i}"));
        }
    }

    #endregion
}
