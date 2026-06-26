using ZipCode.Cli.Configurations;

namespace ZipCode.Cli.Packaging;

class PackagePlanBuilder
{
    readonly Dictionary<string, PackagePlanItem> Items;
    readonly RuntimeConfiguration Configuration;
    readonly IReadOnlyList<SelectedScope> Scopes;
    readonly IReadOnlyList<Rule> Rules;
    readonly RuleAction DefaultAction;
    readonly string? OutputPath;
    bool HasBuilt;

    public PackagePlanBuilder(RuntimeConfiguration configuration, string scopeName, string? outputPath = null)
        : this(configuration, [scopeName], outputPath)
    {
    }

    public PackagePlanBuilder(RuntimeConfiguration configuration, IReadOnlyList<string> scopeNames, string? outputPath = null)
    {
        Configuration = configuration;
        OutputPath = outputPath;
        Scopes = GetSelectedScopes(configuration, scopeNames);
        
        Items = new Dictionary<string, PackagePlanItem>
        (
            configuration.Configuration.CaseSensitive
                ? StringComparer.Ordinal
                : StringComparer.OrdinalIgnoreCase
        );
        
        Rules = BuildRules(configuration, outputPath);
        
        DefaultAction = string.Equals(configuration.Configuration.DefaultAction, "exclude", StringComparison.OrdinalIgnoreCase)
            ? RuleAction.Exclude
            : RuleAction.Include;
    }

    public PackagePlan Build()
    {
        if (HasBuilt)
            throw new InvalidOperationException($"{nameof(PackagePlanBuilder)} can build only one package plan.");

        HasBuilt = true;

        foreach (var selectedScope in Scopes)
        {
            AddScope(selectedScope);
        }

        var orderedItems = Items.Values
            .OrderBy(item => item.PackagePath, Configuration.Configuration.CaseSensitive
                ? StringComparer.Ordinal
                : StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new PackagePlan([.. Scopes.Select(scope => scope.Name)], orderedItems);
    }

    private static IReadOnlyList<Rule> BuildRules(RuntimeConfiguration configuration, string? outputPath)
    {
        var rules = new List<Rule>();

        var outputDirectoryRule = CreateOutputDirectoryRule(configuration, outputPath);

        if (outputDirectoryRule is not null)
        {
            rules.Add(outputDirectoryRule);
        }

        rules.AddRange(configuration.Configuration.Rules.Select(rule =>
            new Rule(rule, configuration.Configuration.CaseSensitive)));

        return rules;
    }

    private static Rule? CreateOutputDirectoryRule(RuntimeConfiguration configuration, string? outputPath)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return null;
        }

        var outputDirectoryPath = Path.GetDirectoryName(Path.GetFullPath(outputPath));

        if (string.IsNullOrWhiteSpace(outputDirectoryPath))
        {
            return null;
        }

        var relativePath = Path.GetRelativePath(configuration.RootPath, outputDirectoryPath);

        var isOutsideRoot = relativePath == "."
            || relativePath == ".."
            || relativePath.StartsWith("../", StringComparison.Ordinal)
            || relativePath.StartsWith(@"..\", StringComparison.Ordinal)
            || Path.IsPathRooted(relativePath);

        if (isOutsideRoot)
        {
            return null;
        }

        var packagePath = relativePath.Replace('\\', '/').Trim('/');

        if (string.IsNullOrWhiteSpace(packagePath))
        {
            return null;
        }

        var rule = new ZipCodeRule
        {
            Name = "Auto exclude ZIP output directory",
            Action = "exclude",
            Target = "any",
            Patterns =
            [
                $"{packagePath}/**"
            ]
        };

        return new Rule(rule, configuration.Configuration.CaseSensitive);
    }

    private static IReadOnlyList<SelectedScope> GetSelectedScopes(RuntimeConfiguration configuration, IReadOnlyList<string> scopeNames)
    {
        if (scopeNames.Count == 0)
        {
            throw new CliUsageException("Scope name is not defined.");
        }

        var selectedScopes = new List<SelectedScope>(scopeNames.Count);

        foreach (var scopeName in scopeNames)
        {
            if (string.IsNullOrWhiteSpace(scopeName))
            {
                throw new CliUsageException("Scope name is not defined.");
            }

            if (!configuration.Configuration.Scopes.TryGetValue(scopeName, out var scope))
            {
                throw new CliUsageException($"Scope '{scopeName}' is not defined.");
            }

            selectedScopes.Add(new SelectedScope(scopeName, scope));
        }

        return selectedScopes;
    }

    private void AddScope(SelectedScope selectedScope)
    {
        foreach (var source in selectedScope.Scope.Sources)
        {
            var sourcePath = ResolveSourcePath(source);

            if (!Directory.Exists(sourcePath) && !File.Exists(sourcePath))
            {
                throw new CliUsageException($"Scope '{selectedScope.Name}' source does not exist: {Configuration.ToDisplayPath(sourcePath)}");
            }

            if (Directory.Exists(sourcePath))
            {
                var packagePath = NormalizePackagePath(sourcePath);
                if (packagePath.Length > 0
                    && Evaluate(selectedScope.Name, packagePath, isDirectory: true) == RuleAction.Exclude)
                {
                    continue;
                }

                AddDirectory(selectedScope.Name, sourcePath);
            }
            else
            {
                AddFile(selectedScope.Name, sourcePath);
            }
        }
    }

    private void AddDirectory(string scopeName, string directoryPath)
    {
        foreach (var filePath in Directory.EnumerateFiles(directoryPath))
        {
            AddFile(scopeName, filePath);
        }

        foreach (var childDirectoryPath in Directory.EnumerateDirectories(directoryPath))
        {
            var packagePath = NormalizePackagePath(childDirectoryPath);

            if (Evaluate(scopeName, packagePath, isDirectory: true) == RuleAction.Exclude)
            {
                continue;
            }

            if (File.GetAttributes(childDirectoryPath).HasFlag(FileAttributes.ReparsePoint))
            {
                continue;
            }

            AddDirectory(scopeName, childDirectoryPath);
        }
    }

    private void AddFile(string scopeName, string sourcePath)
    {
        var packagePath = NormalizePackagePath(sourcePath);

        if (Evaluate(scopeName, packagePath, isDirectory: false) == RuleAction.Exclude)
            return;

        if (File.GetAttributes(sourcePath).HasFlag(FileAttributes.ReparsePoint))
            return;

        var fileInfo = new FileInfo(sourcePath);

        if (!fileInfo.Exists)
            return;

        if (Items.TryGetValue(packagePath, out var existingItem))
        {
            if (false == string.Equals(existingItem.SourcePath, sourcePath, StringComparison.Ordinal))
            {
                throw new CliUsageException($"Package path collision: '{packagePath}' maps both '{existingItem.SourcePath}' and '{sourcePath}'.");
            }
        }

        Items.TryAdd
        (
            packagePath,
            new PackagePlanItem
            (
                sourcePath,
                packagePath,
                new FileStamp(new DateTimeOffset(fileInfo.LastWriteTimeUtc, TimeSpan.Zero), fileInfo.Length)
            )
        );
    }

    private RuleAction Evaluate(string scopeName, string packagePath, bool isDirectory)
    {
        foreach (var rule in Rules)
        {
            if (rule.IsMatch(scopeName, packagePath, isDirectory))
            {
                return rule.Action;
            }
        }

        return DefaultAction;
    }

    private string ResolveSourcePath(string source)
    {
        var sourcePath = Path.GetFullPath(Path.IsPathRooted(source)
            ? source
            : Path.Combine(Configuration.RootPath, source));

        EnsureInsideRoot(sourcePath);
        return sourcePath;
    }

    private string NormalizePackagePath(string path)
    {
        EnsureInsideRoot(path);
        var relativePath = Path.GetRelativePath(Configuration.RootPath, path);
        return relativePath == "."
            ? ""
            : relativePath.Replace('\\', '/');
    }

    private void EnsureInsideRoot(string path)
    {
        var relativePath = Path.GetRelativePath(Configuration.RootPath, path);

        if (relativePath == ".")
        {
            return;
        }

        var isPathOutsideRoot = relativePath == ".."
            || relativePath.StartsWith("../", StringComparison.Ordinal)
            || relativePath.StartsWith(@"..\", StringComparison.Ordinal)
            || Path.IsPathRooted(relativePath);

        if (isPathOutsideRoot)
        {
            throw new CliUsageException($"Path must be inside configured root: {path}");
        }
    }

    private sealed record SelectedScope(string Name, ZipCodeScope Scope);
}
