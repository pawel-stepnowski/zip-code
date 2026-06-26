namespace ZipCode.Cli;

record CliOptions
(
    string Command,
    string? ConfigurationFilePath,
    string? Scope,
    string? OutputPath,
    bool PrintFiles,
    bool DryRun,
    bool ShowHelp
);
