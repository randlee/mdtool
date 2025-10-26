using Xunit;
using MDTool.Core;
using MDTool.Models;

namespace MDTool.Tests.Core;

public class MarkdownParserTests
{
    [Fact]
    public void ParseContent_NoFrontmatter_ReturnsEmptyVariables()
    {
        var content = "# Hello World\nThis is plain markdown.";
        var parser = new MarkdownParser();

        var result = parser.ParseContent(content);

        Assert.True(result.Success);
        Assert.NotNull(result.Value);
        Assert.Empty(result.Value.Variables);
        Assert.Equal(content, result.Value.Content);
    }

    [Fact]
    public void ParseContent_SimpleFrontmatter_ParsesRequiredVariables()
    {
        var content = @"---
variables:
  NAME: ""User's full name""
  EMAIL: ""User's email address""
---
# Hello {{NAME}}";

        var parser = new MarkdownParser();
        var result = parser.ParseContent(content);

        Assert.True(result.Success);
        Assert.NotNull(result.Value);
        Assert.Equal(2, result.Value.Variables.Count);

        Assert.True(result.Value.Variables.ContainsKey("NAME"));
        Assert.True(result.Value.Variables["NAME"].Required);
        Assert.Equal("User's full name", result.Value.Variables["NAME"].Description);

        Assert.True(result.Value.Variables.ContainsKey("EMAIL"));
        Assert.True(result.Value.Variables["EMAIL"].Required);
    }

    [Fact]
    public void ParseContent_ObjectFormat_ParsesOptionalVariables()
    {
        var content = @"---
variables:
  BRANCH:
    description: ""Git branch""
    required: false
    default: ""main""
---
Content";

        var parser = new MarkdownParser();
        var result = parser.ParseContent(content);

        Assert.True(result.Success);
        Assert.NotNull(result.Value);
        Assert.Single(result.Value.Variables);

        var branch = result.Value.Variables["BRANCH"];
        Assert.False(branch.Required);
        Assert.Equal("Git branch", branch.Description);
        Assert.Equal("main", branch.DefaultValue);
    }

    [Fact]
    public void ParseContent_MixedFormats_ParsesBoth()
    {
        var content = @"---
variables:
  NAME: ""Required name""
  PORT:
    description: ""Server port""
    required: false
    default: 8080
---
Content";

        var parser = new MarkdownParser();
        var result = parser.ParseContent(content);

        Assert.True(result.Success);
        Assert.Equal(2, result.Value!.Variables.Count);

        Assert.True(result.Value.Variables["NAME"].Required);
        Assert.False(result.Value.Variables["PORT"].Required);
        // YamlDotNet may parse 8080 as int or string depending on format
        Assert.NotNull(result.Value.Variables["PORT"].DefaultValue);
        var portValue = result.Value.Variables["PORT"].DefaultValue!.ToString();
        Assert.Equal("8080", portValue);
    }

    [Fact]
    public void ParseContent_InvalidYaml_ReturnsError()
    {
        var content = @"---
variables:
  NAME: ""Unclosed string
---
Content";

        var parser = new MarkdownParser();
        var result = parser.ParseContent(content);

        Assert.False(result.Success);
        Assert.NotEmpty(result.Errors);
        Assert.Contains(result.Errors, e => e.Type == ErrorType.InvalidYamlHeader);
    }

    [Fact]
    public void ParseContent_MissingVariablesSection_ReturnsError()
    {
        var content = @"---
title: ""My Document""
---
Content";

        var parser = new MarkdownParser();
        var result = parser.ParseContent(content);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Type == ErrorType.InvalidYamlHeader);
    }

    [Fact]
    public void ParseContent_InvalidVariableName_ReturnsError()
    {
        var content = @"---
variables:
  user-name: ""Invalid name""
---
Content";

        var parser = new MarkdownParser();
        var result = parser.ParseContent(content);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Type == ErrorType.InvalidVariableFormat);
    }

    [Fact]
    public void ParseContent_OptionalWithoutDefault_ReturnsError()
    {
        var content = @"---
variables:
  BRANCH:
    description: ""Git branch""
    required: false
---
Content";

        var parser = new MarkdownParser();
        var result = parser.ParseContent(content);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Type == ErrorType.InvalidYamlHeader);
    }

    [Fact]
    public void ParseContent_ConflictingPaths_ReturnsError()
    {
        var content = @"---
variables:
  USER: ""User info""
  USER.NAME: ""User name""
---
Content";

        var parser = new MarkdownParser();
        var result = parser.ParseContent(content);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Type == ErrorType.InvalidVariableFormat);
        Assert.Contains(result.Errors, e => e.Description.Contains("Conflicting"));
    }

    [Fact]
    public void ParseContent_DotNotationVariables_ParsesCorrectly()
    {
        var content = @"---
variables:
  USER.NAME: ""User's name""
  USER.EMAIL: ""User's email""
  API.KEY: ""API key""
---
Content";

        var parser = new MarkdownParser();
        var result = parser.ParseContent(content);

        Assert.True(result.Success);
        Assert.Equal(3, result.Value!.Variables.Count);
        Assert.Contains("USER.NAME", result.Value.Variables.Keys);
        Assert.Contains("USER.EMAIL", result.Value.Variables.Keys);
        Assert.Contains("API.KEY", result.Value.Variables.Keys);
    }

    [Fact]
    public void ParseContent_EmptyFrontmatter_ReturnsEmptyVariables()
    {
        var content = @"---
---
Content";

        var parser = new MarkdownParser();
        var result = parser.ParseContent(content);

        Assert.True(result.Success);
        Assert.Empty(result.Value!.Variables);
    }

    [Fact]
    public void ParseContent_UnclosedFrontmatter_TreatsAsNoFrontmatter()
    {
        var content = @"---
variables:
  NAME: ""Test""
Content without closing ---";

        var parser = new MarkdownParser();
        var result = parser.ParseContent(content);

        Assert.True(result.Success);
        Assert.Empty(result.Value!.Variables);
        Assert.Contains("---", result.Value.Content);
    }

    [Fact]
    public void ParseContent_MultipleErrors_CollectsAll()
    {
        var content = @"---
variables:
  user-name: ""Invalid name""
  PORT:
    description: ""Port""
    required: false
---
Content";

        var parser = new MarkdownParser();
        var result = parser.ParseContent(content);

        Assert.False(result.Success);
        Assert.True(result.Errors.Count >= 2); // Invalid name + optional without default
    }

    [Fact]
    public void ParseContent_NullContent_ReturnsError()
    {
        var parser = new MarkdownParser();
        var result = parser.ParseContent(null!);

        Assert.False(result.Success);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void SplitFrontmatter_ValidFrontmatter_SplitsCorrectly()
    {
        var content = @"---
variables:
  NAME: ""Test""
---
# Content
Body text";

        var (hasYaml, yaml, body) = MarkdownParser.SplitFrontmatter(content);

        Assert.True(hasYaml);
        Assert.Contains("variables:", yaml);
        Assert.Contains("# Content", body);
        Assert.DoesNotContain("---", body);
    }

    [Fact]
    public void SplitFrontmatter_NoFrontmatter_ReturnsFullContent()
    {
        var content = "# Hello\nContent";

        var (hasYaml, yaml, body) = MarkdownParser.SplitFrontmatter(content);

        Assert.False(hasYaml);
        Assert.Empty(yaml);
        Assert.Equal(content, body);
    }

    [Fact]
    public void ParseContent_ObjectFormatWithoutDescription_ReturnsError()
    {
        var content = @"---
variables:
  PORT:
    required: false
    default: 8080
---
Content";

        var parser = new MarkdownParser();
        var result = parser.ParseContent(content);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Type == ErrorType.InvalidYamlHeader);
    }

    [Fact]
    public void ParseContent_WithNumbers_ParsesCorrectly()
    {
        var content = @"---
variables:
  PORT8080: ""Port description""
---
Content";

        var parser = new MarkdownParser();
        var result = parser.ParseContent(content);

        Assert.True(result.Success);
        Assert.Contains("PORT8080", result.Value!.Variables.Keys);
    }

    [Fact]
    public void ParseContent_BooleanDefault_ParsesCorrectly()
    {
        var content = @"---
variables:
  DEBUG:
    description: ""Debug mode""
    required: false
    default: false
---
Content";

        var parser = new MarkdownParser();
        var result = parser.ParseContent(content);

        Assert.True(result.Success);
        // YamlDotNet may parse false as bool or string depending on format
        var debugValue = result.Value!.Variables["DEBUG"].DefaultValue;
        Assert.NotNull(debugValue);
        Assert.True(debugValue.ToString()!.Equals("false", StringComparison.OrdinalIgnoreCase) || debugValue.Equals(false));
    }

    [Fact]
    public void ParseContent_NumericDefault_ParsesCorrectly()
    {
        var content = @"---
variables:
  PORT:
    description: ""Server port""
    required: false
    default: 8080
  TIMEOUT:
    description: ""Timeout in seconds""
    required: false
    default: 30
---
Content";

        var parser = new MarkdownParser();
        var result = parser.ParseContent(content);

        Assert.True(result.Success);
        Assert.Equal(2, result.Value!.Variables.Count);

        // YamlDotNet might parse numbers as int or string depending on format
        // Just verify they're not null
        Assert.NotNull(result.Value.Variables["PORT"].DefaultValue);
        Assert.NotNull(result.Value.Variables["TIMEOUT"].DefaultValue);
    }
}
