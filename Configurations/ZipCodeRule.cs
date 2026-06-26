namespace ZipCode.Cli.Configurations;

class ZipCodeRule
{
    public string? Name { get; set; }
    public string Action { get; set; } = "exclude";
    public string Target { get; set; } = "any";
    public string? Pattern { get; set; }
    public List<string> Patterns { get; set; } = [];
    public List<string> Scopes { get; set; } = [];
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? Pattern ?? "<unnamed>" : Name;

    public IEnumerable<string> GetPatterns()
    {
        if (!string.IsNullOrWhiteSpace(Pattern))
        {
            yield return Pattern;
        }

        foreach (var pattern in Patterns)
        {
            if (!string.IsNullOrWhiteSpace(pattern))
            {
                yield return pattern;
            }
        }
    }
}
