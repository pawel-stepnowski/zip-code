namespace ZipCode.Cli;

record CliOptions
(
    string Command,
    string? ConfigurationFilePath,
    IReadOnlyList<string> Scopes,
    string? OutputPath,
    bool PrintFiles,
    bool DryRun,
    bool ShowHelp
);
