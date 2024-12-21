using CppAst;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Diagnostics;
using System.IO;
using ClangSharp;
using Microsoft.Extensions.Logging;
using static BridgeGeneratorTree.CommandLineOptions;

// TODO: Issue list of bugs and other plans for the tool so far

// TODO: Figure out how to make classes with parent classes to work.
// TODO: Currently operator functions cause invalid names and need to be fixed. One solution is to instead use a UUID or something.
// TODO:    > Maybe just state you can't apply the function on operator types?
// TODO: Template functions also currently don't really work. Decide if we should just not allow them for generation?
// TODO:    > Undecided but likely not.

namespace BridgeGeneratorTree
{
    class Program
    {
        private static readonly ILogger DefaultLogger = Helpers.Logging.CreateDefaultLogger<Program>();
            
        public static async Task<int> Main(params string[] args)
        {
            var parser = GetCommandLineBuilder().Build();
            return await parser.InvokeAsync(args).ConfigureAwait(false);
        }
        
        public static void Run(InvocationContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Debug);
            });


            var outputPath = context.ParseResult.GetValueForOption(Options.OptOutputOption);
            var rootIncludeDir = context.ParseResult.GetValueForOption(Options.OptRootIncludeDirOption);
            var rawHeaderFiles = context.ParseResult.GetValueForOption(Options.OptHeadersOption);

            Debug.Assert(outputPath != null, nameof(outputPath) + " != null");
            Debug.Assert(rootIncludeDir != null, nameof(rootIncludeDir) + " != null");
            Debug.Assert(rawHeaderFiles != null, nameof(rawHeaderFiles) + " != null");
            
            //FileManager.SetupOutputPath(outputPath, DefaultLogger);
            
            // Currently we only generate C++ code. We do not yet automatically generate C# code YET.
            CodeGenerator.GenerateBridge(outputPath, rootIncludeDir, rawHeaderFiles, DefaultLogger);

            DefaultLogger.LogInformation("C++ bridge generation complete.");
        }
        
    }
}
