namespace ZipCode.Cli.Packaging;

internal sealed record LoadedPackageManifest
(
    string Path,
    PackageManifest Manifest
);
