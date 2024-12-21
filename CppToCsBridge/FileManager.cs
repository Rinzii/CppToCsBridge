using Microsoft.Extensions.Logging;

namespace BridgeGeneratorTree
{
    class FileManager
    {
        public static void SetupOutputPath(string outputPath, ILogger logger)
        {
            if (!Directory.Exists(outputPath))
            {
                try
                {
                    Directory.CreateDirectory(outputPath);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error creating output directory '{OutputPath}'", outputPath);
                    throw new ApplicationException("Failed to setup output directory.");
                }
            }
        }

        public static void WriteGeneratedFile(string filePath, string content)
        {
            File.WriteAllText(filePath, content);
        }
    }
}