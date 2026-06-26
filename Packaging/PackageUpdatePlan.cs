namespace ZipCode.Cli.Packaging;

internal sealed record PackageUpdatePlan
(
    IReadOnlyList<string> EntriesToDelete,
    IReadOnlyList<PackagePlanItem> ItemsToWrite,
    int AddedCount,
    int UpdatedCount,
    int RemovedCount,
    int UnchangedCount
);
