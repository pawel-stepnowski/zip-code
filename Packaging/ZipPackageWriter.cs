using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using ZipCode.Cli.Configurations;

namespace ZipCode.Cli.Packaging;

class ZipPackageWriter
{
    private readonly PreviousPackageFinder PreviousPackageFinder = new();

    private static readonly JsonSerializerOptions ManifestJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static string GetOutputPath(RuntimeConfiguration loadedConfiguration, string scopeLabel, string? outputOverride)
    {
        var configuration = loadedConfiguration.Configuration;

        if (!string.IsNullOrWhiteSpace(outputOverride))
        {
            return Path.GetFullPath(Path.IsPathRooted(outputOverride)
                ? outputOverride
                : Path.Combine(loadedConfiguration.RootPath, outputOverride));
        }

        var outputDirectory = Path.GetFullPath(Path.Combine(loadedConfiguration.RootPath, configuration.Output.Directory));
        var fileName = ExpandFileName(configuration.Output.FileName, scopeLabel);
        return Path.Combine(outputDirectory, fileName);
    }

    public ZipPackageResult Write(RuntimeConfiguration loadedConfiguration, PackagePlan plan, string outputPath)
    {
        var configuration = loadedConfiguration.Configuration;
        var manifestEntryName = NormalizeEntryName(configuration.Output.ManifestEntryName);
        var outputDirectory = Path.GetDirectoryName(outputPath) ?? loadedConfiguration.RootPath;

        if (plan.Items.Any(item => string.Equals(item.PackagePath, manifestEntryName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new CliUsageException($"Manifest entry conflicts with a source file: {manifestEntryName}");
        }

        if (File.Exists(outputPath))
        {
            if (!configuration.Output.Overwrite)
            {
                throw new CliUsageException($"Output file already exists: {loadedConfiguration.ToDisplayPath(outputPath)}");
            }
        }

        Directory.CreateDirectory(outputDirectory);

        var compressionLevel = ParseCompressionLevel(configuration.Output.CompressionLevel);
        var nowUtc = DateTimeOffset.UtcNow;
        var manifestJson = CreateManifestJson(plan, nowUtc);
        var previousPackage = PreviousPackageFinder.Find(loadedConfiguration, plan, outputPath, manifestEntryName);

        if (previousPackage is not null)
        {
            return WriteIncremental
            (
                loadedConfiguration,
                plan,
                outputPath,
                previousPackage,
                manifestEntryName,
                manifestJson,
                compressionLevel
            );
        }

        return WriteFresh
        (
            loadedConfiguration,
            plan,
            outputPath,
            manifestEntryName,
            manifestJson,
            compressionLevel
        );
    }

    private static ZipPackageResult WriteFresh
    (
        RuntimeConfiguration loadedConfiguration,
        PackagePlan plan,
        string outputPath,
        string manifestEntryName,
        string manifestJson,
        CompressionLevel compressionLevel
    )
    {
        var stagingPath = GetStagingPath(outputPath);

        try
        {
            using (var archive = ZipFile.Open(stagingPath, ZipArchiveMode.Create))
            {
                foreach (var item in plan.Items)
                {
                    WriteItem(archive, item, compressionLevel);
                }

                WriteManifest(archive, manifestEntryName, manifestJson, compressionLevel);
            }

            MoveStagingToOutput(stagingPath, outputPath, loadedConfiguration.Configuration.Output.Overwrite);

            return new ZipPackageResult
            (
                outputPath,
                plan.Items.Count,
                manifestEntryName,
                "fresh",
                null,
                plan.Items.Count,
                0,
                0,
                0
            );
        }
        catch (Exception ex) when (File.Exists(stagingPath))
        {
            throw new IOException($"Failed to create package. Incomplete ZIP was left at: {loadedConfiguration.ToDisplayPath(stagingPath)}", ex);
        }
    }

    private static ZipPackageResult WriteIncremental
    (
        RuntimeConfiguration loadedConfiguration,
        PackagePlan plan,
        string outputPath,
        LoadedPackageManifest previousPackage,
        string manifestEntryName,
        string manifestJson,
        CompressionLevel compressionLevel
    )
    {
        var comparer = new PackageUpdateComparer(loadedConfiguration.Configuration.CaseSensitive
            ? StringComparer.Ordinal
            : StringComparer.OrdinalIgnoreCase);
        var pathComparer = loadedConfiguration.Configuration.CaseSensitive
            ? StringComparer.Ordinal
            : StringComparer.OrdinalIgnoreCase;
        var updatePlan = comparer.Compare(previousPackage.Manifest, plan);
        var stagingPath = GetStagingPath(outputPath);

        try
        {
            File.Copy(previousPackage.Path, stagingPath);

            using (var archive = ZipFile.Open(stagingPath, ZipArchiveMode.Update))
            {
                ApplyUpdatePlan(archive, plan, updatePlan, manifestEntryName, manifestJson, compressionLevel, pathComparer);
            }

            MoveStagingToOutput(stagingPath, outputPath, loadedConfiguration.Configuration.Output.Overwrite);

            return new ZipPackageResult
            (
                outputPath,
                plan.Items.Count,
                manifestEntryName,
                "incremental",
                previousPackage.Path,
                updatePlan.AddedCount,
                updatePlan.UpdatedCount,
                updatePlan.RemovedCount,
                updatePlan.UnchangedCount
            );
        }
        catch (Exception ex) when (File.Exists(stagingPath))
        {
            throw new IOException($"Failed to update package. Incomplete ZIP was left at: {loadedConfiguration.ToDisplayPath(stagingPath)}", ex);
        }
    }

    private static void ApplyUpdatePlan
    (
        ZipArchive archive,
        PackagePlan plan,
        PackageUpdatePlan updatePlan,
        string manifestEntryName,
        string manifestJson,
        CompressionLevel compressionLevel,
        StringComparer pathComparer
    )
    {
        var desiredEntryPaths = plan.Items
            .Select(item => item.PackagePath)
            .Append(manifestEntryName)
            .ToHashSet(pathComparer);

        var entriesToReplace = updatePlan.ItemsToWrite
            .Select(item => item.PackagePath)
            .Append(manifestEntryName)
            .Concat(updatePlan.EntriesToDelete)
            .ToHashSet(pathComparer);

        foreach (var entry in archive.Entries.ToArray())
        {
            if (!desiredEntryPaths.Contains(entry.FullName) || entriesToReplace.Contains(entry.FullName))
            {
                entry.Delete();
            }
        }

        var existingEntryPaths = archive.Entries
            .Select(entry => entry.FullName)
            .ToHashSet(pathComparer);

        var itemsToWrite = updatePlan.ItemsToWrite
            .ToDictionary(item => item.PackagePath, pathComparer);

        foreach (var item in plan.Items)
        {
            if (!existingEntryPaths.Contains(item.PackagePath))
            {
                itemsToWrite.TryAdd(item.PackagePath, item);
            }
        }

        foreach (var item in itemsToWrite.Values.OrderBy(item => item.PackagePath, pathComparer))
        {
            WriteItem(archive, item, compressionLevel);
        }

        WriteManifest(archive, manifestEntryName, manifestJson, compressionLevel);
    }

    private static void WriteItem(ZipArchive archive, PackagePlanItem item, CompressionLevel compressionLevel)
    {
        var entry = archive.CreateEntry(item.PackagePath, compressionLevel);
        entry.LastWriteTime = item.Stamp.ModifiedAtUtc;

        using var input = new FileStream
        (
            item.SourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite,
            bufferSize: 1024 * 64,
            FileOptions.SequentialScan
        );
        
        using var output = entry.Open();
        input.CopyTo(output);
    }

    private static void WriteManifest(ZipArchive archive, string manifestEntryName, string manifestJson, CompressionLevel compressionLevel)
    {
        var manifestEntry = archive.CreateEntry(manifestEntryName, compressionLevel);
        manifestEntry.LastWriteTime = DateTimeOffset.UtcNow;

        using var writer = new StreamWriter(manifestEntry.Open());
        writer.Write(manifestJson);
    }

    private static string CreateManifestJson(PackagePlan plan, DateTimeOffset createdAtUtc)
    {
        var manifest = PackageManifest.FromPlan(plan, createdAtUtc);
        return JsonSerializer.Serialize(manifest, ManifestJsonOptions);
    }

    private static string ExpandFileName(string template, string scopeName)
    {
        var nowLocal = DateTimeOffset.Now;
        var nowUtc = DateTimeOffset.UtcNow;
        return template
            .Replace("{scope}", scopeName, StringComparison.Ordinal)
            .Replace("{scopeLower}", scopeName.ToLowerInvariant(), StringComparison.Ordinal)
            .Replace("{timestamp}", nowLocal.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{timestampUtc}", nowUtc.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    private static string NormalizeEntryName(string value)
    {
        return value.Replace('\\', '/').TrimStart('/');
    }

    private static CompressionLevel ParseCompressionLevel(string value)
    {
        if (Enum.TryParse<CompressionLevel>(value, ignoreCase: true, out var compressionLevel))
        {
            return compressionLevel;
        }

        throw new CliUsageException($"Unknown compression level '{value}'. Use Optimal, Fastest, NoCompression, or SmallestSize.");
    }

    private static string GetStagingPath(string outputPath)
    {
        var directory = Path.GetDirectoryName(outputPath) ?? Directory.GetCurrentDirectory();
        var fileName = Path.GetFileNameWithoutExtension(outputPath);
        var extension = Path.GetExtension(outputPath);
        return Path.Combine(directory, $"{fileName}.building-{Guid.NewGuid():N}{extension}");
    }

    private static void MoveStagingToOutput(string stagingPath, string outputPath, bool overwrite)
    {
        if (File.Exists(outputPath))
        {
            if (!overwrite)
                throw new CliUsageException($"Output file already exists: {outputPath}");

            File.Delete(outputPath);
        }

        File.Move(stagingPath, outputPath);
    }
}
