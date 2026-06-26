namespace ZipCode.Cli.Packaging;

record PackagePlanItem
(
    string SourcePath,
    string PackagePath,
    FileStamp Stamp
);
