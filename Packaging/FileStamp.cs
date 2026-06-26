namespace ZipCode.Cli.Packaging;

internal sealed record FileStamp
(
    DateTimeOffset ModifiedAtUtc,
    long Length
);
