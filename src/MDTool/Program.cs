using System.CommandLine;

namespace MDTool;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("MDTool - Markdown processing with variable substitution");

        // Commands will be added here:
        // - GetSchemaCommand
        // - ValidateCommand
        // - ProcessCommand
        // - GenerateHeaderCommand

        return await rootCommand.InvokeAsync(args);
    }
}
