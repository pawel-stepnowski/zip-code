using System.Text;
using System.Text.RegularExpressions;
using ZipCode.Cli.Configurations;

namespace ZipCode.Cli.Packaging;

internal sealed class PreviousPackageFinder
{
    private readonly PackageManifestReader ManifestReader = new();

    public LoadedPackageManifest? Find
    (
        RuntimeConfiguration loadedConfiguration,
        PackagePlan plan,
        string outputPath,
        string manifestEntryName
    )
    {
        var outputDirectory = Path.GetDirectoryName(outputPath) ?? loadedConfiguration.RootPath;

        if (!Directory.Exists(outputDirectory))
        {
            return null;
        }

        var configuration = loadedConfiguration.Configuration;
        var fileNameRegex = CreateFileNameRegex
        (
            configuration.Output.FileName,
            plan.ScopeLabel,
            configuration.CaseSensitive
        );

        var scopeComparer = StringComparer.OrdinalIgnoreCase;
        var candidatePaths = Directory
            .EnumerateFiles(outputDirectory, "*.zip", SearchOption.TopDirectoryOnly)
            .Where(path => fileNameRegex.IsMatch(Path.GetFileName(path)))
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .ToArray();

        foreach (var candidatePath in candidatePaths)
        {
            var manifest = PackageManifestReader.TryRead(candidatePath, manifestEntryName);

            if (manifest is null)
                continue;

            if (!manifest.Scopes.SequenceEqual(plan.Scopes, scopeComparer))
                continue;

            return new LoadedPackageManifest(candidatePath, manifest);
        }

        return null;
    }

    private static Regex CreateFileNameRegex(string template, string scopeLabel, bool caseSensitive)
    {
        var builder = new StringBuilder("^");

        for (var index = 0; index < template.Length;)
        {
            if (template[index] == '{')
            {
                var endIndex = template.IndexOf('}', index);
                if (endIndex > index)
                {
                    var token = template[(index + 1)..endIndex];
                    builder.Append(TokenToRegex(token, scopeLabel));
                    index = endIndex + 1;
                    continue;
                }
            }

            builder.Append(Regex.Escape(template[index].ToString()));
            index++;
        }

        builder.Append('$');
        
        return new Regex
        (
            builder.ToString(),
            RegexOptions.CultureInvariant | (caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase)
        );
    }

    private static string TokenToRegex(string token, string scopeLabel)
    {
        return token switch
        {
            "scope" => Regex.Escape(scopeLabel),
            "scopeLower" => Regex.Escape(scopeLabel.ToLowerInvariant()),
            "timestamp" or "timestampUtc" => @"\d{8}_\d{6}",
            _ => @"[^\\/]*"
        };
    }
}
