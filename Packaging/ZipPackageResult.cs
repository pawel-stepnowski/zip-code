namespace ZipCode.Cli.Packaging;

record ZipPackageResult
(
    string OutputPath,
    int FileCount,
    string ManifestEntryName,
    string Mode,
    string? BasePackagePath,
    int AddedCount,
    int UpdatedCount,
    int RemovedCount,
    int UnchangedCount
);
