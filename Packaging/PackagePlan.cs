namespace ZipCode.Cli.Packaging;

internal sealed record PackagePlan
(
    IReadOnlyList<string> Scopes,
    IReadOnlyList<PackagePlanItem> Items
)
{
    public string ScopeLabel => Scopes.Count == 1
        ? Scopes[0]
        : string.Join("+", Scopes);
}
