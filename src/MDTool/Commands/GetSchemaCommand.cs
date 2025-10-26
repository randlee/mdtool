using System.CommandLine;
using MDTool.Core;
using MDTool.Utilities;

namespace MDTool.Commands;

/// <summary>
/// Command to extract JSON schema from markdown template.
/// Usage: mdtool get-schema <file> [--output <path>]
/// </summary>
public class GetSchemaCommand : Command
{
    public GetSchemaCommand()
        : base("get-schema", "Extract variable schema from markdown file")
    {
        // Define arguments
        var fileArgument = new Argument<string>(
            name: "file",
            description: "Path to markdown template file");

        // Define options
        var outputOption = new Option<string?>(
            name: "--output",
            description: "Output file path (default: stdout)");
        outputOption.AddAlias("-o");

        // Add to command
        AddArgument(fileArgument);
        AddOption(outputOption);

        // Set handler
        this.SetHandler(async (context) =>
        {
            var file = context.ParseResult.GetValueForArgument(fileArgument);
            var output = context.ParseResult.GetValueForOption(outputOption);
            context.ExitCode = await ExecuteAsync(file, output);
        });
    }

    private static async Task<int> ExecuteAsync(string filePath, string? outputPath)
    {
        try
        {
            // Step 1: Read markdown file
            var readResult = await FileHelper.ReadFileAsync(filePath);
            if (!readResult.Success)
            {
                Console.WriteLine(JsonOutput.Failure(readResult.Errors));
                return 1;
            }

            var content = readResult.Value!;

            // Step 2: Parse markdown content
            var parser = new MarkdownParser();
            var parseResult = parser.ParseContent(content);
            if (!parseResult.Success)
            {
                Console.WriteLine(JsonOutput.Failure(parseResult.Errors));
                return 1;
            }

            var document = parseResult.Value!;

            // Step 3: Generate JSON schema
            var schemaJson = SchemaGenerator.GenerateSchema(document.Variables);

            // Step 4: Output schema
            if (string.IsNullOrEmpty(outputPath))
            {
                // Output to stdout (raw JSON, not wrapped)
                Console.WriteLine(schemaJson);
            }
            else
            {
                // Write to file
                var writeResult = await FileHelper.WriteFileAsync(outputPath, schemaJson);
                if (!writeResult.Success)
                {
                    Console.WriteLine(JsonOutput.Failure(writeResult.Errors));
                    return 1;
                }

                // Success message
                Console.WriteLine(JsonOutput.Success(new
                {
                    message = $"Schema written to {outputPath}",
                    file = outputPath
                }));
            }

            return 0;
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    private static int HandleException(Exception ex)
    {
        var error = Models.ValidationError.UnhandledException(ex.Message);
        Console.WriteLine(JsonOutput.Failure(new List<Models.ValidationError> { error }));
        return 1;
    }
}
