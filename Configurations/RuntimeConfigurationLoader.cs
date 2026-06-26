using System.Text.Json;

namespace ZipCode.Cli.Configurations;

internal static class RuntimeConfigurationLoader
{
    private static readonly string[] DefaultConfigurationRelativePaths =
    [
        "zip-code.config.json",
        Path.Combine("tools", "zip-code.config.json"),
        Path.Combine(".zipcode", "zip-code.config.json")
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    public static RuntimeConfiguration Load(string? configPath)
    {
        var workingDirectoryPath = Directory.GetCurrentDirectory();
        var fullConfigPath = string.IsNullOrWhiteSpace(configPath)
            ? FindDefaultConfigPath(workingDirectoryPath)
            : Path.GetFullPath(configPath, workingDirectoryPath);

        if (!File.Exists(fullConfigPath))
        {
            throw new CliUsageException($"Configuration file was not found: {fullConfigPath}");
        }

        var json = File.ReadAllText(fullConfigPath);
        var configuration = JsonSerializer.Deserialize<ZipCodeConfiguration>(json, JsonOptions)
            ?? throw new CliUsageException("Configuration file is empty or invalid.");

        Normalize(configuration);
        Validate(configuration);
        
        var rootPath = ResolvePath(workingDirectoryPath, configuration.Root);

        return new RuntimeConfiguration(configuration, fullConfigPath, workingDirectoryPath, rootPath);
    }

    private static string FindDefaultConfigPath(string workingDirectoryPath)
    {
        var directory = new DirectoryInfo(workingDirectoryPath);

        while (directory is not null)
        {
            foreach (var relativePath in DefaultConfigurationRelativePaths)
            {
                var candidatePath = Path.Combine(directory.FullName, relativePath);
                if (File.Exists(candidatePath))
                {
                    return candidatePath;
                }
            }

            directory = directory.Parent;
        }

        return Path.GetFullPath(DefaultConfigurationRelativePaths[0], workingDirectoryPath);
    }

    private static void Normalize(ZipCodeConfiguration configuration)
    {
        configuration.Root = string.IsNullOrWhiteSpace(configuration.Root)
            ? "."
            : configuration.Root;

        configuration.DefaultAction = string.IsNullOrWhiteSpace(configuration.DefaultAction)
            ? "include"
            : configuration.DefaultAction;

        configuration.Output ??= new ZipCodeOutputOptions();
        configuration.Scopes ??= [];
        configuration.Rules ??= [];

        configuration.Scopes = new Dictionary<string, ZipCodeScope>(configuration.Scopes, StringComparer.OrdinalIgnoreCase);

        foreach (var scope in configuration.Scopes.Values)
        {
            scope.Sources ??= [];
        }

        foreach (var rule in configuration.Rules)
        {
            rule.Action = string.IsNullOrWhiteSpace(rule.Action) ? "exclude" : rule.Action;
            rule.Target = string.IsNullOrWhiteSpace(rule.Target) ? "any" : rule.Target;
            rule.Patterns ??= [];
            rule.Scopes ??= [];
        }
    }

    private static void Validate(ZipCodeConfiguration configuration)
    {
        if (!IsAction(configuration.DefaultAction))
        {
            throw new CliUsageException("defaultAction must be 'include' or 'exclude'.");
        }

        if (configuration.Scopes.Count == 0)
        {
            throw new CliUsageException("Configuration must define at least one scope.");
        }

        foreach (var (scopeName, scope) in configuration.Scopes)
        {
            if (string.IsNullOrWhiteSpace(scopeName))
            {
                throw new CliUsageException("Scope name cannot be empty.");
            }

            if (scope.Sources.Count == 0)
            {
                throw new CliUsageException($"Scope '{scopeName}' must define at least one source.");
            }
        }

        foreach (var rule in configuration.Rules)
        {
            if (!IsAction(rule.Action))
            {
                throw new CliUsageException($"Rule '{rule.DisplayName}' has invalid action '{rule.Action}'.");
            }

            if (!IsTarget(rule.Target))
            {
                throw new CliUsageException($"Rule '{rule.DisplayName}' has invalid target '{rule.Target}'.");
            }

            if (rule.Patterns.Count == 0 && string.IsNullOrWhiteSpace(rule.Pattern))
            {
                throw new CliUsageException($"Rule '{rule.DisplayName}' must define pattern or patterns.");
            }
        }

        if (string.IsNullOrWhiteSpace(configuration.Output.ManifestEntryName))
        {
            throw new CliUsageException("output.manifestEntryName cannot be empty.");
        }

        if (Path.IsPathRooted(configuration.Output.ManifestEntryName))
        {
            throw new CliUsageException("output.manifestEntryName must be a ZIP-relative path.");
        }
    }

    private static bool IsAction(string value)
    {
        return string.Equals(value, "include", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "exclude", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTarget(string value)
    {
        return string.Equals(value, "any", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "file", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "directory", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolvePath(string baseDirectory, string path)
    {
        return Path.GetFullPath(Path.IsPathRooted(path)
            ? path
            : Path.Combine(baseDirectory, path));
    }
}
