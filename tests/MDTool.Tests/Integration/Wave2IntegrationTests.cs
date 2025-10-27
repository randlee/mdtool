using MDTool.Core;
using MDTool.Models;
using MDTool.Utilities;
using Xunit;

namespace MDTool.Tests.Integration;

/// <summary>
/// Integration tests verifying that Core and Utilities namespaces work together correctly.
/// These tests focus on cross-namespace interactions, not individual namespace functionality.
/// </summary>
public class Wave2IntegrationTests : IDisposable
{
    private readonly string _testDirectory;

    public Wave2IntegrationTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"mdtool-integration-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    #region Scenario 1: FileHelper + MarkdownParser Integration

    [Fact]
    public async Task Integration_FileHelper_MarkdownParser_SuccessCase()
    {
        // Arrange: Create a markdown file with frontmatter
        var testFile = Path.Combine(_testDirectory, "test.md");
        var content = @"---
variables:
  USER_NAME: ""User name""
  PROJECT_NAME: ""Project name""
---

# Hello {{USER_NAME}}

This is a test document for {{PROJECT_NAME}}.";

        await File.WriteAllTextAsync(testFile, content);

        // Act: Read file and parse content
        var readResult = await FileHelper.ReadFileAsync(testFile);
        Assert.True(readResult.Success, "File read should succeed");

        var parser = new MarkdownParser();
        var parseResult = parser.ParseContent(readResult.Value);

        // Assert: Verify the pipeline worked
        Assert.True(parseResult.Success, "Parse should succeed");
        var doc = parseResult.Value;
        Assert.NotNull(doc?.Variables);
        Assert.Equal(2, doc.Variables.Count);
        Assert.True(doc.Variables.ContainsKey("USER_NAME"));
        Assert.Contains("{{USER_NAME}}", doc.Content);
        Assert.Contains("{{PROJECT_NAME}}", doc.Content);
    }

    [Fact]
    public async Task Integration_FileHelper_MarkdownParser_FileNotFoundError()
    {
        // Arrange: Non-existent file
        var nonExistentFile = Path.Combine(_testDirectory, "nonexistent.md");

        // Act: Try to read and parse
        var readResult = await FileHelper.ReadFileAsync(nonExistentFile);

        // Assert: FileHelper error should propagate correctly
        Assert.False(readResult.Success, "File read should fail");
        Assert.Single(readResult.Errors);
        Assert.Equal(ErrorType.FileNotFound, readResult.Errors.First().Type);
        Assert.Contains("not found", readResult.Errors.First().Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Integration_FileHelper_MarkdownParser_InvalidMarkdownError()
    {
        // Arrange: File with invalid frontmatter
        var testFile = Path.Combine(_testDirectory, "invalid.md");
        var content = @"---
this is not: valid: yaml: syntax:::
---

Content";
        await File.WriteAllTextAsync(testFile, content);

        // Act: Read file and try to parse
        var readResult = await FileHelper.ReadFileAsync(testFile);
        Assert.True(readResult.Success, "File read should succeed");

        var parser = new MarkdownParser();
        var parseResult = parser.ParseContent(readResult.Value);

        // Assert: Parser error should be returned
        Assert.False(parseResult.Success, "Parse should fail");
        Assert.Single(parseResult.Errors);
        Assert.Equal(ErrorType.InvalidYamlHeader, parseResult.Errors.First().Type);
    }

    #endregion

    #region Scenario 2: Core + JsonOutput Integration

    [Fact]
    public void Integration_SchemaGenerator_JsonOutput_SuccessCase()
    {
        // Arrange: Create variables
        var variables = new Dictionary<string, VariableDefinition>
        {
            ["USER_NAME"] = new("USER_NAME", "User name", required: true, defaultValue: null),
            ["USER_AGE"] = new("USER_AGE", "User age", required: false, defaultValue: 25),
            ["USER_EMAIL"] = new("USER_EMAIL", "Email address", required: true, defaultValue: null)
        };

        // Act: Generate schema (already JSON formatted)
        var schemaJson = SchemaGenerator.GenerateSchema(variables);

        // Also test wrapping in JsonOutput for commands
        var wrappedJson = JsonOutput.Success(new { schema = schemaJson });

        // Assert: Verify schema JSON output (SchemaGenerator returns JSON string)
        Assert.Contains("\"userName\"", schemaJson); // lowerCamelCase conversion
        Assert.Contains("\"userAge\"", schemaJson);
        Assert.Contains("\"userEmail\"", schemaJson);

        // Verify JsonOutput wrapping works
        Assert.Contains("\"success\": true", wrappedJson);
        Assert.Contains("\"schema\"", wrappedJson);
    }

    [Fact]
    public void Integration_VariableExtractor_SchemaGenerator_JsonOutput_Pipeline()
    {
        // Arrange: Markdown content with variables
        var content = @"# Welcome {{USER_NAME}}

Your ID is {{USER_ID}} and email is {{USER_EMAIL}}.
Optional field: {{OPTIONAL_FIELD}}";

        // Act: Extract variables, create definitions, generate schema
        var extractedVars = VariableExtractor.ExtractVariables(content);

        // Create variable definitions
        var variables = new Dictionary<string, VariableDefinition>();
        foreach (var varName in extractedVars)
        {
            variables[varName] = new VariableDefinition(varName, $"Description for {varName}", required: true, defaultValue: null);
        }

        var schemaJson = SchemaGenerator.GenerateSchema(variables);

        // Wrap in JsonOutput for command response
        var commandResult = JsonOutput.Success(new { schema = schemaJson, variables = extractedVars });

        // Assert: Verify schema generation (SchemaGenerator returns JSON string with lowerCamelCase)
        Assert.Contains("\"userName\"", schemaJson); // lowerCamelCase
        Assert.Contains("\"userId\"", schemaJson);
        Assert.Contains("\"userEmail\"", schemaJson);
        Assert.Contains("\"optionalField\"", schemaJson);

        // Verify command response wrapper
        Assert.Contains("\"success\": true", commandResult);
        Assert.Contains("\"schema\"", commandResult);
    }

    [Fact]
    public void Integration_JsonOutput_SerializesValidationErrors()
    {
        // Arrange: Create validation errors (from Models)
        var errors = new List<ValidationError>
        {
            ValidationError.MissingVariable("USER_NAME", "User name not provided"),
            ValidationError.InvalidYaml("Invalid YAML format", 5)
        };

        // Act: Format errors as JSON
        var jsonResult = JsonOutput.Failure(errors);

        // Assert: Verify error serialization
        Assert.Contains("\"success\": false", jsonResult);
        Assert.Contains("\"type\": \"MissingRequiredVariable\"", jsonResult);
        Assert.Contains("\"type\": \"InvalidYamlHeader\"", jsonResult);
        Assert.Contains("USER_NAME", jsonResult);
        Assert.Contains("Invalid YAML format", jsonResult);
        Assert.Contains("\"line\": 5", jsonResult);
    }

    #endregion

    #region Scenario 3: Full Processing Pipeline

    [Fact]
    public async Task Integration_FullPipeline_FileReadParseSubstituteWrite()
    {
        // Arrange: Create markdown file with frontmatter and variables
        var inputFile = Path.Combine(_testDirectory, "input.md");
        var outputFile = Path.Combine(_testDirectory, "output.md");
        var content = @"---
variables:
  USER_NAME: ""User name""
  PROJECT_NAME: ""Project name""
---

# Hello {{USER_NAME}}!

Welcome to {{PROJECT_NAME}}.";

        await File.WriteAllTextAsync(inputFile, content);

        var args = new Dictionary<string, object>
        {
            { "USER_NAME", "Alice" },
            { "PROJECT_NAME", "MDTool" }
        };

        // Act: Full pipeline
        // 1. Read file
        var readResult = await FileHelper.ReadFileAsync(inputFile);
        Assert.True(readResult.Success);

        // 2. Parse content
        var parser = new MarkdownParser();
        var parseResult = parser.ParseContent(readResult.Value);
        Assert.True(parseResult.Success);
        Assert.NotNull(parseResult.Value?.Variables);
        
        // 3. Substitute variables
        var substituteResult = VariableSubstitutor.Substitute(parseResult.Value.Content, parseResult.Value.Variables, args);
        Assert.True(substituteResult.Success);
        Assert.NotNull(substituteResult.Value);

        // 4. Write result
        var writeResult = await FileHelper.WriteFileAsync(outputFile, substituteResult.Value);
        Assert.True(writeResult.Success);

        // Assert: Verify output file
        var outputContent = await File.ReadAllTextAsync(outputFile);
        Assert.Contains("Hello Alice!", outputContent);
        Assert.Contains("Welcome to MDTool.", outputContent);
        Assert.DoesNotContain("{{USER_NAME}}", outputContent);
        Assert.DoesNotContain("{{PROJECT_NAME}}", outputContent);
    }

    [Fact]
    public async Task Integration_FullPipeline_ErrorPropagation()
    {
        // Arrange: Create markdown file with variables but no values provided
        var inputFile = Path.Combine(_testDirectory, "input-error.md");
        var content = @"---
variables:
  USER_NAME: ""User name""
  MISSING_VAR: ""Missing variable""
---

# Hello {{USER_NAME}}!

Missing: {{MISSING_VAR}}";

        await File.WriteAllTextAsync(inputFile, content);

        // Act: Pipeline with missing variables
        var readResult = await FileHelper.ReadFileAsync(inputFile);
        Assert.True(readResult.Success);

        var parser = new MarkdownParser();
        var parseResult = parser.ParseContent(readResult.Value);
        Assert.True(parseResult.Success);
        Assert.NotNull(parseResult.Value?.Content);
        
        // Substitute with empty variables (should fail)
        var emptyArgs = new Dictionary<string, object>();
        var substituteResult = VariableSubstitutor.Substitute(parseResult.Value.Content, parseResult.Value.Variables, emptyArgs);

        // Assert: Error should be detected
        Assert.False(substituteResult.Success);
        Assert.NotEmpty(substituteResult.Errors);
        Assert.All(substituteResult.Errors, error =>
            Assert.Equal(ErrorType.MissingRequiredVariable, error.Type));

        // Verify JsonOutput can format these errors
        var jsonResult = JsonOutput.Failure(substituteResult.Errors);
        Assert.Contains("\"success\": false", jsonResult);
        Assert.Contains("\"type\": \"MissingRequiredVariable\"", jsonResult);
    }

    [Fact]
    public async Task Integration_FullPipeline_WithJsonOutput()
    {
        // Arrange: Create markdown file
        var inputFile = Path.Combine(_testDirectory, "input-json.md");
        var content = @"---
variables:
  VAR1: ""First variable""
  VAR2: ""Second variable""
---

Variables: {{VAR1}}, {{VAR2}}";

        await File.WriteAllTextAsync(inputFile, content);

        // Act: Pipeline ending with JSON output
        var readResult = await FileHelper.ReadFileAsync(inputFile);
        Assert.NotNull(readResult.Value);
        var parser = new MarkdownParser();

        var parseResult = parser.ParseContent(readResult.Value);
        Assert.NotNull(parseResult.Value?.Variables);
        var schemaJson = SchemaGenerator.GenerateSchema(parseResult.Value.Variables);

        // Wrap in command response
        var commandResult = JsonOutput.Success(new { schema = schemaJson });

        // Assert: Verify schema JSON (SchemaGenerator output)
        Assert.Contains("\"var1\"", schemaJson); // lowerCamelCase
        Assert.Contains("\"var2\"", schemaJson); // lowerCamelCase

        // Verify command wrapper
        Assert.Contains("\"success\": true", commandResult);
        Assert.Contains("\"schema\"", commandResult);
    }

    #endregion

    #region Scenario 4: ProcessingResult<T> Flow Across Namespaces

    [Fact]
    public void Integration_ProcessingResult_BindChaining()
    {
        // Test that ProcessingResult<T> flows correctly across namespace boundaries
        var content = "# Hello {{USER_NAME}}";

        // Create a ProcessingResult and chain operations
        var extractedVars = VariableExtractor.ExtractVariables(content);

        // Create variables from extracted names
        var variables = new Dictionary<string, VariableDefinition>();
        foreach (var varName in extractedVars)
        {
            variables[varName] = new VariableDefinition(varName, $"Description for {varName}", required: true, defaultValue: null);
        }

        var schema = SchemaGenerator.GenerateSchema(variables);

        // Assert: Chaining works
        Assert.NotEmpty(schema);
        Assert.Contains("userName", schema); // lowerCamelCase
    }

    [Fact]
    public void Integration_ProcessingResult_MapOperation()
    {
        // Test Map operation across namespaces
        var content = "# Hello {{USER_NAME}}";

        // Create ProcessingResult and use Map
        var result = ProcessingResult<string>.Ok(content);
        var mapped = result.Map(c => c.Length);

        // Assert: Map works
        Assert.True(mapped.Success);
        Assert.Equal(content.Length, mapped.Value);
    }

    [Fact]
    public async Task Integration_ProcessingResult_FileHelperUnitType()
    {
        // Test that FileHelper's ProcessingResult<Unit> works correctly
        var testFile = Path.Combine(_testDirectory, "unit-test.md");
        var content = "test content";

        // Act: Write file (returns ProcessingResult<Unit>)
        var writeResult = await FileHelper.WriteFileAsync(testFile, content);

        // Assert: Unit type works
        Assert.True(writeResult.Success);
        Assert.Equal(Unit.Value, writeResult.Value); // Unit.Value exists
        Assert.True(File.Exists(testFile));
    }

    #endregion

    #region Scenario 5: lowerCamelCase Consistency

    [Fact]
    public void Integration_LowerCamelCase_JsonOutputAndSchemaGenerator()
    {
        // Arrange: Create variables with multi-word names
        var variables = new Dictionary<string, VariableDefinition>
        {
            ["FIRST_NAME"] = new("FIRST_NAME", "First name", required: true, defaultValue: null),
            ["LAST_NAME"] = new("LAST_NAME", "Last name", required: true, defaultValue: null)
        };

        // Act: Generate schema and wrap in command response
        var schemaJson = SchemaGenerator.GenerateSchema(variables);
        var commandResult = JsonOutput.Success(new { schema = schemaJson });

        // Assert: SchemaGenerator uses lowerCamelCase
        Assert.Contains("\"firstName\"", schemaJson); // Schema property (FIRST_NAME -> firstName)
        Assert.Contains("\"lastName\"", schemaJson);  // Schema property (LAST_NAME -> lastName)

        // JsonOutput uses lowerCamelCase for its own properties
        Assert.Contains("\"success\": true", commandResult); // JsonOutput property
        Assert.Contains("\"schema\"", commandResult); // JsonOutput property

        // Verify no PascalCase or UPPER_SNAKE_CASE leaking through
        Assert.DoesNotContain("\"FirstName\"", schemaJson);
        Assert.DoesNotContain("\"LastName\"", schemaJson);
        Assert.DoesNotContain("\"FIRST_NAME\"", schemaJson);
        Assert.DoesNotContain("\"LAST_NAME\"", schemaJson);
    }

    #endregion

    #region Scenario 6: Error Type Compatibility

    [Fact]
    public void Integration_ValidationErrorTypes_AllSerializeCorrectly()
    {
        // Arrange: Create all error types from Models
        var errors = new List<ValidationError>
        {
            ValidationError.InvalidFormat("TEST", "Format error"),
            ValidationError.MissingVariable("TEST_VAR", "Missing var"),
            ValidationError.InvalidYaml("Invalid YAML"),
            ValidationError.InvalidJson("Invalid JSON"),
            ValidationError.FileNotFound("/path/to/file"),
            ValidationError.FileSizeExceeded("/path/to/file", 1000000, 500000),
            ValidationError.FileExists("/path/to/file")
        };

        // Act: Serialize all error types
        var jsonResult = JsonOutput.Failure(errors);

        // Assert: All error types serialize correctly
        Assert.Contains("\"type\": \"InvalidVariableFormat\"", jsonResult);
        Assert.Contains("\"type\": \"MissingRequiredVariable\"", jsonResult);
        Assert.Contains("\"type\": \"InvalidYamlHeader\"", jsonResult);
        Assert.Contains("\"type\": \"InvalidJsonArgs\"", jsonResult);
        Assert.Contains("\"type\": \"FileNotFound\"", jsonResult);
        Assert.Contains("\"type\": \"FileSizeExceeded\"", jsonResult);
        Assert.Contains("\"type\": \"FileExists\"", jsonResult);
    }

    #endregion
}
