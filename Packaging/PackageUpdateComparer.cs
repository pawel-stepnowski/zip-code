namespace ZipCode.Cli.Packaging;

class PackageUpdateComparer
{
    private readonly StringComparer PathComparer;

    public PackageUpdateComparer(StringComparer pathComparer)
    {
        PathComparer = pathComparer;
    }

    public PackageUpdatePlan Compare(PackageManifest existingManifest, PackagePlan newPlan)
    {
        var existingFiles = new Dictionary<string, PackageManifestFile>(PathComparer);

        foreach (var file in existingManifest.Files)
        {
            existingFiles.TryAdd(file.Path, file);
        }

        var entriesToDelete = new List<string>();
        var itemsToWrite = new List<PackagePlanItem>();
        var addedCount = 0;
        var updatedCount = 0;
        var unchangedCount = 0;

        foreach (var item in newPlan.Items)
        {
            if (!existingFiles.Remove(item.PackagePath, out var existingFile))
            {
                itemsToWrite.Add(item);
                addedCount++;
                continue;
            }

            if (existingFile.Stamp != item.Stamp)
            {
                entriesToDelete.Add(item.PackagePath);
                itemsToWrite.Add(item);
                updatedCount++;
                continue;
            }

            unchangedCount++;
        }

        var removedEntries = existingFiles.Keys
            .OrderBy(path => path, PathComparer)
            .ToArray();

        entriesToDelete.AddRange(removedEntries);

        return new PackageUpdatePlan
        (
            entriesToDelete,
            itemsToWrite,
            addedCount,
            updatedCount,
            removedEntries.Length,
            unchangedCount
        );
    }
}
