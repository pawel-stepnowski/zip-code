using System.Text.Json.Serialization;

namespace ZipCode.Cli.Packaging;

class PackageManifestFile
{
    public string Path { get; init; } = "";

    public DateTimeOffset ModifiedAtUtc { get; init; }

    public long Length { get; init; }

    [JsonIgnore]
    public FileStamp Stamp => new(ModifiedAtUtc, Length);

    public PackageManifestFile()
    {
    }

    public PackageManifestFile(string path, FileStamp stamp)
    {
        Path = path;
        ModifiedAtUtc = stamp.ModifiedAtUtc;
        Length = stamp.Length;
    }
}
