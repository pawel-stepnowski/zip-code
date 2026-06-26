namespace ZipCode.Cli.Packaging;

internal sealed record PackagePlan
(
    IReadOnlyList<string> Scopes,
    IReadOnlyList<PackagePlanItem> Items
)
{
    public string ScopeLabel => GetScopeLabel(Scopes);

    public static string GetScopeLabel(IReadOnlyList<string> scopes)
    {
        return scopes.Count == 1
            ? scopes[0]
            : string.Join("+", scopes);
    }
}
