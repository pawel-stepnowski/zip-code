namespace ZipCode.Cli.Configurations;

class RuntimeConfiguration
{
    public readonly ZipCodeConfiguration Configuration;
    public readonly string ConfigurationPath;
    public readonly string ConfigurationDirectoryPath;
    public readonly string WorkingDirectoryPath;
    public readonly string RootPath;

    public RuntimeConfiguration
    (
        ZipCodeConfiguration configuration,
        string configPath,
        string workingDirectoryPath,
        string rootPath
    )
    {
        Configuration = configuration;
        ConfigurationPath = configPath;
        ConfigurationDirectoryPath = Path.GetDirectoryName(configPath) ?? throw new InvalidOperationException($"Cannot get directory name for path: {configPath}");
        WorkingDirectoryPath = workingDirectoryPath;
        RootPath = rootPath;
    }

    public string ToDisplayPath(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var relativePath = Path.GetRelativePath(RootPath, fullPath);

        if (relativePath == ".")
        {
            return ".";
        }

        var isOutsideRoot = relativePath == ".."
            || relativePath.StartsWith("../", StringComparison.Ordinal)
            || relativePath.StartsWith(@"..\", StringComparison.Ordinal)
            || Path.IsPathRooted(relativePath);

        return isOutsideRoot
            ? fullPath
            : relativePath;
    }
}
