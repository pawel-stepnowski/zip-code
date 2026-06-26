namespace ZipCode.Cli.Configurations;

class ZipCodeOutputOptions
{
    public string Directory { get; set; } = ".zipcode";
    public string FileName { get; set; } = "code_{scopeLower}_{timestampUtc}.zip";
    public string ManifestEntryName { get; set; } = "manifest.json";
    public string CompressionLevel { get; set; } = "Fastest";
    public bool Overwrite { get; set; }
}
