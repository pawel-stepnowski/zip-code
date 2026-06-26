# Development Notes

This document is for maintaining ZipCode itself. User-facing usage is documented in the repository README.

## Build and Run

```powershell
dotnet build
dotnet run -- --dry-run --print-files
dotnet run
.\zip-code.ps1 -DryRun -NoDownload
```

The default repository config packages this project and writes ZIP files to `.zipcode`.

## Direct CLI Usage

```powershell
dotnet ZipCode.Cli.dll pack --config .\zip-code.config.json --scope All
dotnet ZipCode.Cli.dll pack --scope Backend --dry-run --print-files
dotnet ZipCode.Cli.dll pack --scope All --output .zipcode\manual.zip
```

If `--config` is omitted, ZipCode searches upward from the process working directory for:

- `zip-code.config.json`
- `tools/zip-code.config.json`
- `.zipcode/zip-code.config.json`

## Runtime Model

- `RuntimeConfiguration`: normalized runtime snapshot.
- `PackagePlanBuilder`: one-shot builder for the next package plan.
- `PackagePlan`: transient model for a new package.
- `PackagePlanItem`: source file plus package-relative path and stamp.
- `PackageManifest`: persisted description of an existing or newly written package; it does not contain absolute source paths.
- `PackageUpdateComparer`: compares an existing manifest with a new plan and returns entries to delete or write.
- `PreviousPackageFinder`: finds the newest matching ZIP in the output directory.
- `ZipPackageWriter`: writes fresh packages or updates a staging copy of a previous package.

## Incremental Write Flow

Every package contains a manifest entry, defaulting to `manifest.json`.

The manifest stores:

- package path
- UTC modification time
- file length

On later runs, ZipCode searches the output directory for the newest ZIP matching the configured file name template and scope. If a readable manifest is found, ZipCode:

1. Copies the previous ZIP to a staging file named `*.building-<guid>.zip`.
2. Compares the old manifest with the new package plan.
3. Deletes removed or changed entries from the staging ZIP.
4. Adds new or changed entries.
5. Rewrites the manifest.
6. Renames the staging ZIP to the final output path after success.

If writing fails, the incomplete staging ZIP is left in place and named clearly.

## Wrapper Internals

`zip-code.ps1` is intended to be copied into repositories that need packaging. It:

- runs from `RepositoryRootPath`;
- creates generated composite-scope configs under `.zip-code/configs`;
- uses a local development DLL when available;
- otherwise uses a cached/downloaded release asset;
- forwards `-DryRun`, `-Print`, and extra CLI arguments.

Wrapper release parameters:

- `-ZipCodeVersion`: release version to cache/download. Defaults to `0.1.2`.
- `-Latest`: download from the latest GitHub release URL.
- `-ForceDownload`: refresh the cached CLI.
- `-NoDownload`: use only a local development DLL or existing cache.

## Release

The GitHub workflow publishes a framework-dependent release asset named `zip-code.zip` when a tag matching `*.*.*` is pushed.

```powershell
git tag 0.1.2
git push origin 0.1.2
```

The wrapper downloads release assets from:

```text
https://github.com/pawel-stepnowski/zip-code/releases/download/<version>/zip-code.zip
```

or, with `-Latest`:

```text
https://github.com/pawel-stepnowski/zip-code/releases/latest/download/zip-code.zip
```

## Repository Hygiene

The repository ignores build output, package output, and wrapper cache directories such as:

- `bin`
- `obj`
- `.zipcode`
- `.zip-code`

`zip-code.old.ps1` is not part of the tracked project unless intentionally added.
