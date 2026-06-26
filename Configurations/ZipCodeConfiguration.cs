namespace ZipCode.Cli.Configurations;

internal sealed class ZipCodeConfiguration
{
    public string Root { get; set; } = ".";
    public string? DefaultScope { get; set; }
    public bool CaseSensitive { get; set; }
    public string DefaultAction { get; set; } = "include";
    public ZipCodeOutputOptions Output { get; set; } = new();
    public Dictionary<string, ZipCodeScope> Scopes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<ZipCodeRule> Rules { get; set; } = [];
}
