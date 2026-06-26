using ZipCode.Cli.Configurations;
using ZipCode.Cli.Packaging;

namespace ZipCode.Cli;

static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            var options = CliOptionsParser.Parse(args);

            if (options.ShowHelp)
            {
                Console.WriteLine(HelpText);
                return 0;
            }

            if (!string.Equals(options.Command, "pack", StringComparison.OrdinalIgnoreCase))
            {
                throw new CliUsageException($"Unknown command '{options.Command}'.");
            }

            var loadedConfiguration = RuntimeConfigurationLoader.Load(options.ConfigurationFilePath);
            var scopeName = options.Scope
                ?? loadedConfiguration.Configuration.DefaultScope
                ?? "All";

            var outputPath = ZipPackageWriter.GetOutputPath(loadedConfiguration, scopeName, options.OutputPath);
            var planBuilder = new PackagePlanBuilder(loadedConfiguration, scopeName, outputPath);
            var plan = planBuilder.Build();

            if (options.PrintFiles)
            {
                foreach (var item in plan.Items)
                {
                    Console.WriteLine(item.PackagePath);
                }
            }

            if (options.DryRun)
            {
                Console.WriteLine($"Scopes: {string.Join(", ", plan.Scopes)}");
                Console.WriteLine($"Files: {plan.Items.Count}");
                Console.WriteLine($"Output: {loadedConfiguration.ToDisplayPath(outputPath)}");
                Console.WriteLine("Dry run: ZIP was not created.");
                return 0;
            }

            var writer = new ZipPackageWriter();
            var result = writer.Write(loadedConfiguration, plan, outputPath);

            Console.WriteLine($"Created: {loadedConfiguration.ToDisplayPath(result.OutputPath)}");
            Console.WriteLine($"Files: {result.FileCount}");
            Console.WriteLine($"Manifest: {result.ManifestEntryName}");
            Console.WriteLine($"Mode: {result.Mode}");

            if (result.BasePackagePath is not null)
            {
                Console.WriteLine($"Base: {loadedConfiguration.ToDisplayPath(result.BasePackagePath)}");
                Console.WriteLine($"Changes: +{result.AddedCount} ~{result.UpdatedCount} -{result.RemovedCount} ={result.UnchangedCount}");
            }

            return 0;
        }
        catch (CliUsageException ex)
        {
            Console.Error.WriteLine($"usage error: {ex.Message}");
            Console.Error.WriteLine("Run 'dotnet run -- --help' for usage.");
            return 2;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            return 1;
        }
    }

    private const string HelpText = """
        zip-code - package source code into a ZIP archive

        Usage:
          zip-code [pack] [options]

        Options:
          -c, --config <path>   Configuration file. Default: search zip-code.config.json from current directory upward
          -s, --scope <name>    Scope name from configuration. Default: config defaultScope or All
          -o, --output <path>   Output ZIP path override
              --print-files     Print included relative paths
              --dry-run         Build the file list without creating ZIP
          -h, --help            Show help

        Dev default:
          dotnet run
        """;
}
