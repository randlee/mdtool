using System.CommandLine;
using System.Text;
using MDTool.Core;
using MDTool.Models;
using MDTool.Utilities;

namespace MDTool.Commands;

/// <summary>
/// Command to auto-generate YAML header from document variables.
/// Usage: mdtool generate-header <file> [--output <path>]
/// </summary>
public class GenerateHeaderCommand : Command
{
    public GenerateHeaderCommand()
        : base("generate-header", "Auto-generate YAML header from document variables")
    {
        // Define arguments
        var fileArgument = new Argument<string>(
            name: "file",
            description: "Path to markdown file");

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

            // Step 2: Extract all variables from content
            var variables = VariableExtractor.ExtractVariables(content);

            if (variables.Count == 0)
            {
                Console.WriteLine(JsonOutput.Failure(new List<ValidationError>
                {
                    ValidationError.ProcessingError("No variables found in the document")
                }));
                return 1;
            }

            // Step 3: Generate YAML header (flat structure only in Phase 1)
            var yamlHeader = GenerateYamlHeader(variables);

            // Step 4: Output header
            if (string.IsNullOrEmpty(outputPath))
            {
                // Output to stdout (raw YAML, not JSON wrapped)
                Console.WriteLine(yamlHeader);
            }
            else
            {
                // Write to file
                var writeResult = await FileHelper.WriteFileAsync(outputPath, yamlHeader);
                if (!writeResult.Success)
                {
                    Console.WriteLine(JsonOutput.Failure(writeResult.Errors));
                    return 1;
                }

                // Success message
                Console.WriteLine(JsonOutput.Success(new
                {
                    message = $"Header written to: {outputPath}",
                    file = outputPath,
                    variableCount = variables.Count
                }));
            }

            return 0;
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    private static string GenerateYamlHeader(List<string> variables)
    {
        var sb = new StringBuilder();
        sb.AppendLine("---");
        sb.AppendLine("variables:");

        // Sort variables alphabetically for consistency
        var sortedVars = variables.OrderBy(v => v).ToList();

        foreach (var variable in sortedVars)
        {
            // Generate placeholder description
            // Phase 1: Flat structure only (no nested YAML objects)
            var description = $"Description for {variable}";
            sb.AppendLine($"  {variable}: \"{description}\"");
        }

        sb.AppendLine("---");

        return sb.ToString();
    }

    private static int HandleException(Exception ex)
    {
        var error = ValidationError.UnhandledException(ex.Message);
        Console.WriteLine(JsonOutput.Failure(new List<ValidationError> { error }));
        return 1;
    }
}
