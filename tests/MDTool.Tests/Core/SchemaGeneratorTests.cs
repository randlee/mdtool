using Xunit;
using MDTool.Core;
using MDTool.Models;
using System.Text.Json;

namespace MDTool.Tests.Core;

public class SchemaGeneratorTests
{
    [Fact]
    public void GenerateSchema_EmptyVariables_ReturnsEmptyObject()
    {
        var variables = new Dictionary<string, VariableDefinition>();
        var schema = SchemaGenerator.GenerateSchema(variables);

        Assert.Equal("{}", schema);
    }

    [Fact]
    public void GenerateSchema_SimpleRequiredVariable_UsesDescription()
    {
        var variables = new Dictionary<string, VariableDefinition>
        {
            ["NAME"] = new VariableDefinition("NAME", "User's full name", required: true)
        };

        var schema = SchemaGenerator.GenerateSchema(variables);
        var json = JsonDocument.Parse(schema);

        Assert.True(json.RootElement.TryGetProperty("name", out var nameProperty));
        Assert.Equal("User's full name", nameProperty.GetString());
    }

    [Fact]
    public void GenerateSchema_OptionalVariable_UsesDefault()
    {
        var variables = new Dictionary<string, VariableDefinition>
        {
            ["BRANCH"] = new VariableDefinition("BRANCH", "Git branch", required: false, defaultValue: "main")
        };

        var schema = SchemaGenerator.GenerateSchema(variables);
        var json = JsonDocument.Parse(schema);

        Assert.True(json.RootElement.TryGetProperty("branch", out var branchProperty));
        Assert.Equal("main", branchProperty.GetString());
    }

    [Fact]
    public void GenerateSchema_LowerCamelCaseConversion_ConvertsCorrectly()
    {
        var variables = new Dictionary<string, VariableDefinition>
        {
            ["USER_NAME"] = new VariableDefinition("USER_NAME", "User name", required: true),
            ["API_KEY"] = new VariableDefinition("API_KEY", "API key", required: true),
            ["EMAIL"] = new VariableDefinition("EMAIL", "Email address", required: true)
        };

        var schema = SchemaGenerator.GenerateSchema(variables);
        var json = JsonDocument.Parse(schema);

        Assert.True(json.RootElement.TryGetProperty("userName", out _));
        Assert.True(json.RootElement.TryGetProperty("apiKey", out _));
        Assert.True(json.RootElement.TryGetProperty("email", out _));
    }

    [Fact]
    public void GenerateSchema_DotNotation_CreatesNestedStructure()
    {
        var variables = new Dictionary<string, VariableDefinition>
        {
            ["USER.NAME"] = new VariableDefinition("USER.NAME", "User name", required: true),
            ["USER.EMAIL"] = new VariableDefinition("USER.EMAIL", "User email", required: true)
        };

        var schema = SchemaGenerator.GenerateSchema(variables);
        var json = JsonDocument.Parse(schema);

        Assert.True(json.RootElement.TryGetProperty("user", out var userProperty));
        Assert.Equal(JsonValueKind.Object, userProperty.ValueKind);

        Assert.True(userProperty.TryGetProperty("name", out var nameProperty));
        Assert.Equal("User name", nameProperty.GetString());

        Assert.True(userProperty.TryGetProperty("email", out var emailProperty));
        Assert.Equal("User email", emailProperty.GetString());
    }

    [Fact]
    public void GenerateSchema_DeepNesting_CreatesMultipleLevels()
    {
        var variables = new Dictionary<string, VariableDefinition>
        {
            ["API.KEY.NAME"] = new VariableDefinition("API.KEY.NAME", "API key name", required: true)
        };

        var schema = SchemaGenerator.GenerateSchema(variables);
        var json = JsonDocument.Parse(schema);

        Assert.True(json.RootElement.TryGetProperty("api", out var apiProperty));
        Assert.True(apiProperty.TryGetProperty("key", out var keyProperty));
        Assert.True(keyProperty.TryGetProperty("name", out var nameProperty));
        Assert.Equal("API key name", nameProperty.GetString());
    }

    [Fact]
    public void GenerateSchema_MixedSimpleAndNested_HandlesBoth()
    {
        var variables = new Dictionary<string, VariableDefinition>
        {
            ["NAME"] = new VariableDefinition("NAME", "Name", required: true),
            ["USER.EMAIL"] = new VariableDefinition("USER.EMAIL", "Email", required: true)
        };

        var schema = SchemaGenerator.GenerateSchema(variables);
        var json = JsonDocument.Parse(schema);

        Assert.True(json.RootElement.TryGetProperty("name", out _));
        Assert.True(json.RootElement.TryGetProperty("user", out var userProperty));
        Assert.True(userProperty.TryGetProperty("email", out _));
    }

    [Fact]
    public void GenerateSchema_MultipleNestedPaths_MergesCorrectly()
    {
        var variables = new Dictionary<string, VariableDefinition>
        {
            ["DATABASE.HOST"] = new VariableDefinition("DATABASE.HOST", "DB host", required: true),
            ["DATABASE.PORT"] = new VariableDefinition("DATABASE.PORT", "DB port", required: false, defaultValue: 5432),
            ["DATABASE.NAME"] = new VariableDefinition("DATABASE.NAME", "DB name", required: true)
        };

        var schema = SchemaGenerator.GenerateSchema(variables);
        var json = JsonDocument.Parse(schema);

        Assert.True(json.RootElement.TryGetProperty("database", out var dbProperty));
        Assert.True(dbProperty.TryGetProperty("host", out _));
        Assert.True(dbProperty.TryGetProperty("port", out var portProperty));
        Assert.True(dbProperty.TryGetProperty("name", out _));

        Assert.Equal(5432, portProperty.GetInt32());
    }

    [Fact]
    public void GenerateSchema_NumericDefault_PreservesType()
    {
        var variables = new Dictionary<string, VariableDefinition>
        {
            ["PORT"] = new VariableDefinition("PORT", "Server port", required: false, defaultValue: 8080)
        };

        var schema = SchemaGenerator.GenerateSchema(variables);
        var json = JsonDocument.Parse(schema);

        Assert.True(json.RootElement.TryGetProperty("port", out var portProperty));
        Assert.Equal(JsonValueKind.Number, portProperty.ValueKind);
        Assert.Equal(8080, portProperty.GetInt32());
    }

    [Fact]
    public void GenerateSchema_BooleanDefault_PreservesType()
    {
        var variables = new Dictionary<string, VariableDefinition>
        {
            ["DEBUG"] = new VariableDefinition("DEBUG", "Debug mode", required: false, defaultValue: false)
        };

        var schema = SchemaGenerator.GenerateSchema(variables);
        var json = JsonDocument.Parse(schema);

        Assert.True(json.RootElement.TryGetProperty("debug", out var debugProperty));
        // The value is false, either as boolean or as the boolean value false
        if (debugProperty.ValueKind == JsonValueKind.False)
        {
            Assert.False(debugProperty.GetBoolean());
        }
        else if (debugProperty.ValueKind == JsonValueKind.True)
        {
            Assert.True(debugProperty.GetBoolean());
        }
        else
        {
            Assert.False(debugProperty.GetBoolean());
        }
    }

    [Fact]
    public void GenerateSchema_MixedRequiredAndOptional_HandlesCorrectly()
    {
        var variables = new Dictionary<string, VariableDefinition>
        {
            ["APP_NAME"] = new VariableDefinition("APP_NAME", "Application name", required: true),
            ["ENVIRONMENT"] = new VariableDefinition("ENVIRONMENT", "Deploy environment", required: false, defaultValue: "staging"),
            ["DEBUG"] = new VariableDefinition("DEBUG", "Debug mode", required: false, defaultValue: false)
        };

        var schema = SchemaGenerator.GenerateSchema(variables);
        var json = JsonDocument.Parse(schema);

        // Required: uses description
        Assert.True(json.RootElement.TryGetProperty("appName", out var appNameProperty));
        Assert.Equal("Application name", appNameProperty.GetString());

        // Optional: uses default
        Assert.True(json.RootElement.TryGetProperty("environment", out var envProperty));
        Assert.Equal("staging", envProperty.GetString());

        Assert.True(json.RootElement.TryGetProperty("debug", out var debugProperty));
        Assert.False(debugProperty.GetBoolean());
    }

    [Fact]
    public void GenerateSchema_ComplexNestedStructure_GeneratesCorrectly()
    {
        var variables = new Dictionary<string, VariableDefinition>
        {
            ["APP.NAME"] = new VariableDefinition("APP.NAME", "App name", required: true),
            ["APP.VERSION"] = new VariableDefinition("APP.VERSION", "App version", required: false, defaultValue: "1.0.0"),
            ["DATABASE.CONNECTION.HOST"] = new VariableDefinition("DATABASE.CONNECTION.HOST", "DB host", required: true),
            ["DATABASE.CONNECTION.PORT"] = new VariableDefinition("DATABASE.CONNECTION.PORT", "DB port", required: false, defaultValue: 5432)
        };

        var schema = SchemaGenerator.GenerateSchema(variables);
        var json = JsonDocument.Parse(schema);

        // Verify app structure
        Assert.True(json.RootElement.TryGetProperty("app", out var appProperty));
        Assert.True(appProperty.TryGetProperty("name", out _));
        Assert.True(appProperty.TryGetProperty("version", out var versionProperty));
        Assert.Equal("1.0.0", versionProperty.GetString());

        // Verify database structure
        Assert.True(json.RootElement.TryGetProperty("database", out var dbProperty));
        Assert.True(dbProperty.TryGetProperty("connection", out var connProperty));
        Assert.True(connProperty.TryGetProperty("host", out _));
        Assert.True(connProperty.TryGetProperty("port", out var portProperty));
        Assert.Equal(5432, portProperty.GetInt32());
    }

    [Fact]
    public void GenerateSchema_PrettyPrinted_IsIndented()
    {
        var variables = new Dictionary<string, VariableDefinition>
        {
            ["NAME"] = new VariableDefinition("NAME", "Name", required: true)
        };

        var schema = SchemaGenerator.GenerateSchema(variables);

        // Pretty-printed JSON should contain newlines and spaces
        Assert.Contains("\n", schema);
        Assert.Contains("  ", schema);
    }

    [Fact]
    public void GenerateSchema_NullVariables_ReturnsEmptyObject()
    {
        var schema = SchemaGenerator.GenerateSchema(null!);

        Assert.Equal("{}", schema);
    }

    [Fact]
    public void GenerateSchema_SortedOutput_IsDeterministic()
    {
        var variables = new Dictionary<string, VariableDefinition>
        {
            ["ZEBRA"] = new VariableDefinition("ZEBRA", "Last", required: true),
            ["ALPHA"] = new VariableDefinition("ALPHA", "First", required: true),
            ["BETA"] = new VariableDefinition("BETA", "Second", required: true)
        };

        var schema1 = SchemaGenerator.GenerateSchema(variables);
        var schema2 = SchemaGenerator.GenerateSchema(variables);

        Assert.Equal(schema1, schema2);
    }

    [Fact]
    public void GenerateSchema_UnderscoreConversion_Works()
    {
        var variables = new Dictionary<string, VariableDefinition>
        {
            ["USER_FULL_NAME"] = new VariableDefinition("USER_FULL_NAME", "Name", required: true),
            ["API_BASE_URL"] = new VariableDefinition("API_BASE_URL", "URL", required: true)
        };

        var schema = SchemaGenerator.GenerateSchema(variables);
        var json = JsonDocument.Parse(schema);

        Assert.True(json.RootElement.TryGetProperty("userFullName", out _));
        Assert.True(json.RootElement.TryGetProperty("apiBaseUrl", out _));
    }

    [Fact]
    public void GenerateSchema_SingleLetterSegments_ConvertsToLowercase()
    {
        var variables = new Dictionary<string, VariableDefinition>
        {
            ["A"] = new VariableDefinition("A", "Variable A", required: true),
            ["B.C"] = new VariableDefinition("B.C", "Variable B.C", required: true)
        };

        var schema = SchemaGenerator.GenerateSchema(variables);
        var json = JsonDocument.Parse(schema);

        Assert.True(json.RootElement.TryGetProperty("a", out _));
        Assert.True(json.RootElement.TryGetProperty("b", out var bProperty));
        Assert.True(bProperty.TryGetProperty("c", out _));
    }
}
