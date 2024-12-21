using System.CommandLine;
using System.CommandLine.Builder;

namespace BridgeGeneratorTree
{
    static class CommandLineOptions
    {
        private static readonly RootCommand DefaultRootCommand = CreateRootCommand();

        public static CommandLineBuilder GetCommandLineBuilder()
        {
            return new CommandLineBuilder(DefaultRootCommand)
                .UseEnvironmentVariableDirective()
                .UseParseDirective()
                .UseSuggestDirective()
                .RegisterWithDotnetSuggest()
                .UseTypoCorrections()
                .UseParseErrorReporting()
                .UseExceptionHandler()
                .CancelOnProcessTermination();
        }
        
        private static RootCommand CreateRootCommand()
        {
            var rootCommand = new RootCommand("Bridge Generator Tool")
            {
                Options.OptOutputOption,
                Options.OptRootIncludeDirOption,
                Options.OptHeadersOption
            };
            rootCommand.SetHandler(Program.Run);
            return rootCommand;
        }
    }

    static class Options
    {
        public static readonly Option<string> OptOutputOption = new(
            new[] { "--output", "-o" },
            description: "Path to output directory where generated files will be stored.")
        {
            IsRequired = true
        };

        public static readonly Option<string> OptRootIncludeDirOption = new(
            new[] { "--include", "-i" },
            description: "Path to the root include directory.")
        {
            IsRequired = true
        };

        public static readonly Option<string[]> OptHeadersOption = new(
            new[] { "--headers", "-h" },
            description: "List of header files to process separated by spaces.")
        {
            IsRequired = true,
            AllowMultipleArgumentsPerToken = true
        };
    }
    
}