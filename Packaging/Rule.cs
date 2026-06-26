using ZipCode.Cli.Configurations;

namespace ZipCode.Cli.Packaging;

internal sealed class Rule
{
    private readonly IReadOnlyList<GlobPattern> Patterns;
    private readonly HashSet<string>? Scopes;

    public Rule(ZipCodeRule rule, bool caseSensitive)
    {
        Name = rule.DisplayName;
        Action = ParseAction(rule.Action);
        Target = ParseTarget(rule.Target);
        Patterns = [.. rule.GetPatterns().Select(pattern => new GlobPattern(pattern, caseSensitive))];
        Scopes = rule.Scopes.Count == 0
            ? null
            : new HashSet<string>(rule.Scopes, StringComparer.OrdinalIgnoreCase);
    }

    public string Name { get; }

    public RuleAction Action { get; }

    public RuleTarget Target { get; }

    public bool IsMatch(string scopeName, string relativePath, bool isDirectory)
    {
        if (Scopes is not null && !Scopes.Contains(scopeName))
            return false;

        if (Target == RuleTarget.File && isDirectory)
            return false;

        if (Target == RuleTarget.Directory && !isDirectory)
            return false;

        foreach (var pattern in Patterns)
        {
            if (pattern.IsMatch(relativePath))
                return true;

            if (isDirectory && pattern.IsMatch(relativePath + "/"))
                return true;
        }

        return false;
    }

    private static RuleAction ParseAction(string value)
    {
        return string.Equals(value, "include", StringComparison.OrdinalIgnoreCase)
            ? RuleAction.Include
            : RuleAction.Exclude;
    }

    private static RuleTarget ParseTarget(string value)
    {
        if (string.Equals(value, "file", StringComparison.OrdinalIgnoreCase))
            return RuleTarget.File;

        if (string.Equals(value, "directory", StringComparison.OrdinalIgnoreCase))
            return RuleTarget.Directory;

        return RuleTarget.Any;
    }
}
