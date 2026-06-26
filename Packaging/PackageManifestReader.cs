using System.IO.Compression;
using System.Text.Json;

namespace ZipCode.Cli.Packaging;

class PackageManifestReader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static PackageManifest? TryRead(string packagePath, string manifestEntryName)
    {
        try
        {
            using var archive = ZipFile.OpenRead(packagePath);
            var manifestEntry = archive.GetEntry(manifestEntryName);

            if (manifestEntry is null)
            {
                return null;
            }

            using var stream = manifestEntry.Open();
            using var document = JsonDocument.Parse(stream);
            var manifest = document.RootElement.Deserialize<PackageManifest>(JsonOptions);

            if (manifest is null || manifest.Version != 1)
            {
                return null;
            }

            if (manifest.Scopes.Count == 0
                && document.RootElement.TryGetProperty("scope", out var legacyScopeElement)
                && legacyScopeElement.ValueKind == JsonValueKind.String)
            {
                var legacyScope = legacyScopeElement.GetString();

                if (!string.IsNullOrWhiteSpace(legacyScope))
                {
                    manifest = new PackageManifest
                    {
                        CreatedAtUtc = manifest.CreatedAtUtc,
                        Scopes = [legacyScope],
                        Files = manifest.Files,
                        Version = manifest.Version
                    };
                }
            }

            if (manifest.Scopes.Count == 0 || manifest.Files.Any(file => string.IsNullOrWhiteSpace(file.Path)))
            {
                return null;
            }

            return manifest;
        }
        catch (IOException)
        {
            return null;
        }
        catch (InvalidDataException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }
}
