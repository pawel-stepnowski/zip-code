namespace ZipCode.Cli.Packaging;

class PackageManifest
{
    public int Version { get; init; } = 1;
    public DateTimeOffset CreatedAtUtc { get; init; }
    public IReadOnlyList<string> Scopes { get; init; } = [];
    public IReadOnlyList<PackageManifestFile> Files { get; init; } = [];

    public static PackageManifest FromPlan(PackagePlan plan, DateTimeOffset createdAtUtc)
    {
        static PackageManifestFile createPackageManifestFile(PackagePlanItem item)
        {
            return new PackageManifestFile(item.PackagePath, item.Stamp);
        }

        return new PackageManifest
        {
            CreatedAtUtc = createdAtUtc,
            Scopes = plan.Scopes,
            Files = [.. plan.Items.Select(createPackageManifestFile)]
        };
    }
}
