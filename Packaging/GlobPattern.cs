using System.Text;
using System.Text.RegularExpressions;

namespace ZipCode.Cli.Packaging;

class GlobPattern
{
    readonly Regex Regex;

    public GlobPattern(string pattern, bool caseSensitive)
    {
        Pattern = NormalizePattern(pattern);
        Regex = new Regex
        (
            ToRegex(Pattern),
            RegexOptions.CultureInvariant | RegexOptions.Compiled | (caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase)
        );
    }

    public string Pattern { get; }

    public bool IsMatch(string relativePath)
    {
        var normalizedPath = relativePath.Replace('\\', '/').TrimStart('/');
        return Regex.IsMatch(normalizedPath);
    }

    private static string NormalizePattern(string pattern)
    {
        return pattern.Replace('\\', '/').TrimStart('/');
    }

    private static string ToRegex(string pattern)
    {
        if (pattern == "**")
        {
            return "^.*$";
        }

        var segments = pattern.Split('/', StringSplitOptions.None);
        var builder = new StringBuilder("^");

        for (var index = 0; index < segments.Length; index++)
        {
            var segment = segments[index];

            if (segment == "**")
            {
                if (index == segments.Length - 1)
                {
                    builder.Append(".*");
                }
                else
                {
                    builder.Append("(?:[^/]+/)*");
                }

                continue;
            }

            builder.Append(SegmentToRegex(segment));

            if (index < segments.Length - 1)
            {
                builder.Append('/');
            }
        }

        builder.Append('$');
        return builder.ToString();
    }

    private static string SegmentToRegex(string segment)
    {
        var builder = new StringBuilder();

        foreach (var character in segment)
        {
            switch (character)
            {
                case '*':
                    builder.Append("[^/]*");
                    break;
                case '?':
                    builder.Append("[^/]");
                    break;
                default:
                    builder.Append(Regex.Escape(character.ToString()));
                    break;
            }
        }

        return builder.ToString();
    }
}
