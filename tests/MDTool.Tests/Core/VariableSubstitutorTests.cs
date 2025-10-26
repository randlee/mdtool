using Xunit;
using MDTool.Core;
using MDTool.Models;
using System.Text.Json;

namespace MDTool.Tests.Core;

public class VariableSubstitutorTests
{
    [Fact]
    public void Substitute_SimpleVariable_SubstitutesCorrectly()
    {
        var content = "Hello {{NAME}}!";
        var variables = new Dictionary<string, VariableDefinition>
        {
            ["NAME"] = new VariableDefinition("NAME", "User name", required: true)
        };
        var args = new Dictionary<string, object>
        {
            ["NAME"] = "John"
        };

        var result = VariableSubstitutor.Substitute(content, variables, args);

        Assert.True(result.Success);
        Assert.Equal("Hello John!", result.Value);
    }

    [Fact]
    public void Substitute_MultipleVariables_SubstitutesAll()
    {
        var content = "{{USER}} works at {{COMPANY}} in {{CITY}}";
        var variables = new Dictionary<string, VariableDefinition>
        {
            ["USER"] = new VariableDefinition("USER", "User", required: true),
            ["COMPANY"] = new VariableDefinition("COMPANY", "Company", required: true),
            ["CITY"] = new VariableDefinition("CITY", "City", required: true)
        };
        var args = new Dictionary<string, object>
        {
            ["USER"] = "Alice",
            ["COMPANY"] = "Acme Corp",
            ["CITY"] = "NYC"
        };

        var result = VariableSubstitutor.Substitute(content, variables, args);

        Assert.True(result.Success);
        Assert.Equal("Alice works at Acme Corp in NYC", result.Value);
    }

    [Fact]
    public void Substitute_CaseInsensitiveMatching_Works()
    {
        var content = "Hello {{USER_NAME}}!";
        var variables = new Dictionary<string, VariableDefinition>
        {
            ["USER_NAME"] = new VariableDefinition("USER_NAME", "User name", required: true)
        };
        var args = new Dictionary<string, object>
        {
            ["user_name"] = "Bob"  // lowercase
        };

        var result = VariableSubstitutor.Substitute(content, variables, args);

        Assert.True(result.Success);
        Assert.Equal("Hello Bob!", result.Value);
    }

    [Fact]
    public void Substitute_CamelCaseArgs_MatchesUpperSnakeCase()
    {
        var content = "Hello {{USER_NAME}}!";
        var variables = new Dictionary<string, VariableDefinition>
        {
            ["USER_NAME"] = new VariableDefinition("USER_NAME", "User name", required: true)
        };
        var args = new Dictionary<string, object>
        {
            ["userName"] = "Charlie"  // camelCase
        };

        var result = VariableSubstitutor.Substitute(content, variables, args);

        Assert.True(result.Success);
        Assert.Equal("Hello Charlie!", result.Value);
    }

    [Fact]
    public void Substitute_NestedDotNotation_ResolvesFromNestedObject()
    {
        var content = "Hello {{USER.NAME}}!";
        var variables = new Dictionary<string, VariableDefinition>
        {
            ["USER.NAME"] = new VariableDefinition("USER.NAME", "User name", required: true)
        };
        var args = new Dictionary<string, object>
        {
            ["user"] = new Dictionary<string, object>
            {
                ["name"] = "David"
            }
        };

        var result = VariableSubstitutor.Substitute(content, variables, args);

        Assert.True(result.Success);
        Assert.Equal("Hello David!", result.Value);
    }

    [Fact]
    public void Substitute_DeepNesting_ResolvesCorrectly()
    {
        var content = "Host: {{DATABASE.CONNECTION.HOST}}";
        var variables = new Dictionary<string, VariableDefinition>
        {
            ["DATABASE.CONNECTION.HOST"] = new VariableDefinition("DATABASE.CONNECTION.HOST", "DB host", required: true)
        };
        var args = new Dictionary<string, object>
        {
            ["database"] = new Dictionary<string, object>
            {
                ["connection"] = new Dictionary<string, object>
                {
                    ["host"] = "localhost"
                }
            }
        };

        var result = VariableSubstitutor.Substitute(content, variables, args);

        Assert.True(result.Success);
        Assert.Equal("Host: localhost", result.Value);
    }

    [Fact]
    public void Substitute_OptionalVariableWithDefault_UsesDefault()
    {
        var content = "Branch: {{BRANCH}}";
        var variables = new Dictionary<string, VariableDefinition>
        {
            ["BRANCH"] = new VariableDefinition("BRANCH", "Git branch", required: false, defaultValue: "main")
        };
        var args = new Dictionary<string, object>(); // Empty args

        var result = VariableSubstitutor.Substitute(content, variables, args);

        Assert.True(result.Success);
        Assert.Equal("Branch: main", result.Value);
    }

    [Fact]
    public void Substitute_OptionalVariableProvided_UsesProvidedValue()
    {
        var content = "Branch: {{BRANCH}}";
        var variables = new Dictionary<string, VariableDefinition>
        {
            ["BRANCH"] = new VariableDefinition("BRANCH", "Git branch", required: false, defaultValue: "main")
        };
        var args = new Dictionary<string, object>
        {
            ["branch"] = "develop"
        };

        var result = VariableSubstitutor.Substitute(content, variables, args);

        Assert.True(result.Success);
        Assert.Equal("Branch: develop", result.Value);
    }

    [Fact]
    public void Substitute_MissingRequiredVariable_ReturnsError()
    {
        var content = "Hello {{NAME}}!";
        var variables = new Dictionary<string, VariableDefinition>
        {
            ["NAME"] = new VariableDefinition("NAME", "User name", required: true)
        };
        var args = new Dictionary<string, object>(); // Empty

        var result = VariableSubstitutor.Substitute(content, variables, args);

        Assert.False(result.Success);
        Assert.NotEmpty(result.Errors);
        Assert.Contains(result.Errors, e => e.Type == ErrorType.MissingRequiredVariable);
    }

    [Fact]
    public void Substitute_MultipleMissingVariables_CollectsAllErrors()
    {
        var content = "{{NAME}} at {{EMAIL}} works at {{COMPANY}}";
        var variables = new Dictionary<string, VariableDefinition>
        {
            ["NAME"] = new VariableDefinition("NAME", "Name", required: true),
            ["EMAIL"] = new VariableDefinition("EMAIL", "Email", required: true),
            ["COMPANY"] = new VariableDefinition("COMPANY", "Company", required: true)
        };
        var args = new Dictionary<string, object>(); // All missing

        var result = VariableSubstitutor.Substitute(content, variables, args);

        Assert.False(result.Success);
        Assert.Equal(3, result.Errors.Count);
        Assert.All(result.Errors, e => Assert.Equal(ErrorType.MissingRequiredVariable, e.Type));
    }

    [Fact]
    public void Substitute_PartiallyProvidedVariables_ReportsOnlyMissing()
    {
        var content = "{{NAME}} at {{EMAIL}}";
        var variables = new Dictionary<string, VariableDefinition>
        {
            ["NAME"] = new VariableDefinition("NAME", "Name", required: true),
            ["EMAIL"] = new VariableDefinition("EMAIL", "Email", required: true)
        };
        var args = new Dictionary<string, object>
        {
            ["NAME"] = "Alice"
            // EMAIL missing
        };

        var result = VariableSubstitutor.Substitute(content, variables, args);

        Assert.False(result.Success);
        Assert.Single(result.Errors);
        Assert.Contains("EMAIL", result.Errors[0].Variable);
    }

    [Fact]
    public void Substitute_NumericValue_ConvertsToString()
    {
        var content = "Port: {{PORT}}";
        var variables = new Dictionary<string, VariableDefinition>
        {
            ["PORT"] = new VariableDefinition("PORT", "Server port", required: true)
        };
        var args = new Dictionary<string, object>
        {
            ["PORT"] = 8080
        };

        var result = VariableSubstitutor.Substitute(content, variables, args);

        Assert.True(result.Success);
        Assert.Equal("Port: 8080", result.Value);
    }

    [Fact]
    public void Substitute_BooleanValue_ConvertsToString()
    {
        var content = "Debug: {{DEBUG}}";
        var variables = new Dictionary<string, VariableDefinition>
        {
            ["DEBUG"] = new VariableDefinition("DEBUG", "Debug mode", required: true)
        };
        var args = new Dictionary<string, object>
        {
            ["DEBUG"] = true
        };

        var result = VariableSubstitutor.Substitute(content, variables, args);

        Assert.True(result.Success);
        Assert.Equal("Debug: True", result.Value);
    }

    [Fact]
    public void Substitute_EmptyContent_ReturnsEmpty()
    {
        var result = VariableSubstitutor.Substitute(
            "",
            new Dictionary<string, VariableDefinition>(),
            new Dictionary<string, object>()
        );

        Assert.True(result.Success);
        Assert.Equal("", result.Value);
    }

    [Fact]
    public void Substitute_NoVariables_ReturnsContentUnchanged()
    {
        var content = "This is plain text.";
        var result = VariableSubstitutor.Substitute(
            content,
            new Dictionary<string, VariableDefinition>(),
            new Dictionary<string, object>()
        );

        Assert.True(result.Success);
        Assert.Equal(content, result.Value);
    }

    [Fact]
    public void Substitute_NullContent_ReturnsError()
    {
        var result = VariableSubstitutor.Substitute(
            null!,
            new Dictionary<string, VariableDefinition>(),
            new Dictionary<string, object>()
        );

        Assert.False(result.Success);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void Substitute_NullVariables_ReturnsError()
    {
        var result = VariableSubstitutor.Substitute(
            "content",
            null!,
            new Dictionary<string, object>()
        );

        Assert.False(result.Success);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void Substitute_NullArgs_TreatsAsEmpty()
    {
        var content = "Hello {{NAME}}!";
        var variables = new Dictionary<string, VariableDefinition>
        {
            ["NAME"] = new VariableDefinition("NAME", "Name", required: false, defaultValue: "World")
        };

        var result = VariableSubstitutor.Substitute(content, variables, null!);

        Assert.True(result.Success);
        Assert.Equal("Hello World!", result.Value);
    }

    [Fact]
    public void Substitute_DuplicateVariable_SubstitutesAllOccurrences()
    {
        var content = "{{NAME}} said hello. {{NAME}} waved.";
        var variables = new Dictionary<string, VariableDefinition>
        {
            ["NAME"] = new VariableDefinition("NAME", "Name", required: true)
        };
        var args = new Dictionary<string, object>
        {
            ["NAME"] = "Alice"
        };

        var result = VariableSubstitutor.Substitute(content, variables, args);

        Assert.True(result.Success);
        Assert.Equal("Alice said hello. Alice waved.", result.Value);
    }

    [Fact]
    public void Substitute_MixedRequiredAndOptional_SubstitutesCorrectly()
    {
        var content = "{{NAME}} on {{BRANCH}} at {{COMPANY}}";
        var variables = new Dictionary<string, VariableDefinition>
        {
            ["NAME"] = new VariableDefinition("NAME", "Name", required: true),
            ["BRANCH"] = new VariableDefinition("BRANCH", "Branch", required: false, defaultValue: "main"),
            ["COMPANY"] = new VariableDefinition("COMPANY", "Company", required: true)
        };
        var args = new Dictionary<string, object>
        {
            ["NAME"] = "Bob",
            ["COMPANY"] = "Acme"
            // BRANCH not provided, should use default
        };

        var result = VariableSubstitutor.Substitute(content, variables, args);

        Assert.True(result.Success);
        Assert.Equal("Bob on main at Acme", result.Value);
    }

    [Fact]
    public void Substitute_JsonElementArgs_ResolvesCorrectly()
    {
        var content = "Hello {{USER.NAME}}!";
        var variables = new Dictionary<string, VariableDefinition>
        {
            ["USER.NAME"] = new VariableDefinition("USER.NAME", "Name", required: true)
        };

        // Parse JSON to get JsonElement
        var json = @"{ ""user"": { ""name"": ""Eve"" } }";
        var jsonDoc = JsonDocument.Parse(json);
        var args = new Dictionary<string, object>
        {
            ["user"] = jsonDoc.RootElement.GetProperty("user")
        };

        var result = VariableSubstitutor.Substitute(content, variables, args);

        Assert.True(result.Success);
        Assert.Equal("Hello Eve!", result.Value);
    }

    [Fact]
    public void Substitute_NestedJsonElement_NavigatesCorrectly()
    {
        var content = "Host: {{DB.HOST}}, Port: {{DB.PORT}}";
        var variables = new Dictionary<string, VariableDefinition>
        {
            ["DB.HOST"] = new VariableDefinition("DB.HOST", "Host", required: true),
            ["DB.PORT"] = new VariableDefinition("DB.PORT", "Port", required: true)
        };

        var json = @"{ ""db"": { ""host"": ""localhost"", ""port"": 5432 } }";
        var jsonDoc = JsonDocument.Parse(json);
        var args = new Dictionary<string, object>
        {
            ["db"] = jsonDoc.RootElement.GetProperty("db")
        };

        var result = VariableSubstitutor.Substitute(content, variables, args);

        Assert.True(result.Success);
        Assert.Equal("Host: localhost, Port: 5432", result.Value);
    }

    [Fact]
    public void Substitute_MultilineContent_SubstitutesCorrectly()
    {
        var content = @"# Welcome {{NAME}}

Email: {{EMAIL}}
Company: {{COMPANY}}";

        var variables = new Dictionary<string, VariableDefinition>
        {
            ["NAME"] = new VariableDefinition("NAME", "Name", required: true),
            ["EMAIL"] = new VariableDefinition("EMAIL", "Email", required: true),
            ["COMPANY"] = new VariableDefinition("COMPANY", "Company", required: true)
        };
        var args = new Dictionary<string, object>
        {
            ["NAME"] = "Frank",
            ["EMAIL"] = "frank@example.com",
            ["COMPANY"] = "Tech Co"
        };

        var result = VariableSubstitutor.Substitute(content, variables, args);

        Assert.True(result.Success);
        Assert.Contains("Welcome Frank", result.Value);
        Assert.Contains("Email: frank@example.com", result.Value);
        Assert.Contains("Company: Tech Co", result.Value);
    }

    [Fact]
    public void Substitute_UndefinedVariable_ReturnsError()
    {
        var content = "Hello {{UNDEFINED}}!";
        var variables = new Dictionary<string, VariableDefinition>(); // Empty
        var args = new Dictionary<string, object>
        {
            ["UNDEFINED"] = "value"
        };

        var result = VariableSubstitutor.Substitute(content, variables, args);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Variable == "UNDEFINED");
    }
}
