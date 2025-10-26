using System.CommandLine;
using MDTool.Commands;

namespace MDTool;

class Program
{
    static async Task<int> Main(string[] args)
    {
        try
        {
            var rootCommand = new RootCommand("MDTool - Markdown processing with variable substitution")
            {
                new GetSchemaCommand(),
                new ValidateCommand(),
                new ProcessCommand(),
                new GenerateHeaderCommand()
            };

            rootCommand.Description = "Process markdown files with YAML-defined variable substitution";

            return await rootCommand.InvokeAsync(args);
        }
        catch (Exception ex)
        {
            // Global exception handler for unhandled exceptions
            var error = new
            {
                success = false,
                errors = new[]
                {
                    new
                    {
                        type = "UnhandledException",
                        description = ex.Message,
                        stackTrace = ex.StackTrace
                    }
                }
            };

            var json = System.Text.Json.JsonSerializer.Serialize(error, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            });

            Console.WriteLine(json);
            return 1;
        }
    }
}
