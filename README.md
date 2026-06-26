# ZipCode

ZipCode is a small .NET CLI for packaging source code into a ZIP file for ChatGPT context sharing, code review, or handoff between tools.

It is designed to live outside any one application project. A repository provides a JSON configuration with scopes and rules; ZipCode builds a file list, writes a ZIP directly from that list, and includes a manifest so later runs can update from a previous ZIP.

## Features

- Scope-based packaging, for example `Frontend`, `Backend`, `Docs`, `Tools`, or `All`.
- Ordered include/exclude rules where the first matching rule wins.
- Cross-platform path matching on normalized package paths using `/`.
- Direct ZIP writing without copying files to a temporary source directory.
- Manifest stored in the ZIP with package path, UTC modified time, and file length.
- Incremental ZIP updates from the newest matching previous package.
- Safe staging writes using `*.building-<guid>.zip` before the final rename.
- PowerShell wrapper that can download/cache a released CLI or use a local development build.

## Requirements

- .NET 10 runtime for release usage.
- .NET 10 SDK for local development/building.
- PowerShell if you use `zip-code.ps1`.

## Quick Start

Build locally:

```powershell
dotnet build
```

Run from the repository root:

```powershell
dotnet run -- --dry-run --print-files
dotnet run
```

Use the wrapper:

```powershell
.\zip-code.ps1 -DryRun -NoDownload
.\zip-code.ps1
```

The default config in this repository packages the ZipCode project itself and writes archives to `.zipcode`.

## Wrapper Usage

`zip-code.ps1` is intended to be copied into repositories that need packaging. It runs ZipCode from the repository root, using a local development DLL when available or a cached/downloaded release otherwise.

Common examples:

```powershell
# Package the default scope.
.\zip-code.ps1

# Preview files without writing a ZIP.
.\zip-code.ps1 -DryRun -Print

# Package one scope.
.\zip-code.ps1 -Scope Backend

# Package multiple scopes as a generated composite scope.
.\zip-code.ps1 -Scope Frontend,Backend

# Use a specific config file.
.\zip-code.ps1 -ConfigurationPath .\tools\zip-code.config.json

# Use the latest GitHub release asset.
.\zip-code.ps1 -Latest

# Do not download; fail if no local/cache CLI is available.
.\zip-code.ps1 -NoDownload
```

Important wrapper parameters:

- `-Scope`: one or more scope names. Comma-separated values are accepted.
- `-ConfigurationPath`: path to `zip-code.config.json`. Defaults to a config next to the script.
- `-RepositoryRootPath`: root directory used as the process working directory. Defaults to the current directory.
- `-ZipCodeVersion`: release version to cache/download. Defaults to `0.1.2`.
- `-Latest`: download from the latest GitHub release URL.
- `-ForceDownload`: refresh the cached CLI.
- `-NoDownload`: use only a local development DLL or existing cache.
- `-DryRun`: build the package plan without creating a ZIP.
- `-Print`: print included package paths.
- `-CliArguments`: pass extra arguments through to the CLI.

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

## Configuration

A configuration file defines a project root, scopes, output settings, and ordered rules.

```json
{
  "$schema": "./zip-code.schema.json",
  "root": ".",
  "defaultScope": "All",
  "caseSensitive": false,
  "defaultAction": "include",
  "output": {
    "directory": ".zipcode",
    "fileName": "code_{scopeLower}_{timestampUtc}.zip",
    "manifestEntryName": "manifest.json",
    "compressionLevel": "Fastest",
    "overwrite": false
  },
  "scopes": {
    "Backend": {
      "sources": [
        "backend"
      ]
    },
    "All": {
      "sources": [
        "."
      ]
    }
  },
  "rules": [
    {
      "name": "Include selected generated sources",
      "action": "include",
      "target": "any",
      "patterns": [
        "backend/Connectico.Integrations.Amazon/Generated/**"
      ]
    },
    {
      "name": "Exclude build and cache directories",
      "action": "exclude",
      "target": "directory",
      "patterns": [
        "**/.git/**",
        "**/.zipcode/**",
        "**/bin/**",
        "**/obj/**",
        "**/node_modules/**",
        "**/.next/**",
        "**/.turbo/**",
        "**/dist/**",
        "**/build/**",
        "**/out/**",
        "**/generated/**"
      ]
    },
    {
      "name": "Exclude generated files and archives",
      "action": "exclude",
      "target": "file",
      "patterns": [
        "**/*.zip",
        "**/*.log",
        "**/*.tsbuildinfo",
        "**/*.lscache",
        "**/package-lock.json",
        "**/pnpm-lock.yaml",
        "**/yarn.lock"
      ]
    }
  ]
}
```

### Path Semantics

- `root` is the project/package root.
- Relative `root` values are resolved from the process working directory.
- Scope `sources` are resolved from `root`.
- `output.directory` is resolved from `root`.
- Package paths inside the ZIP are relative to `root` and always use `/`.
- Console output displays paths relative to `root` when possible.

For wrapper usage, `zip-code.ps1` runs the CLI from `RepositoryRootPath`, so `root: "."` means the repository root.

## Rule Model

Rules are evaluated in order. The first matching rule decides whether a file or directory is included or excluded.

Rule fields:

- `action`: `include` or `exclude`.
- `target`: `any`, `file`, or `directory`.
- `pattern`: one glob pattern.
- `patterns`: multiple glob patterns.
- `scopes`: optional list of scope names where the rule applies.

Put intentional includes before broad excludes. For example, include one generated source directory before excluding `**/generated/**`.

Supported glob behavior:

- `*` matches within one path segment.
- `?` matches one character within one path segment.
- `**` matches across path segments.
- Matching is case-insensitive by default unless `caseSensitive` is `true`.

## Manifest and Incremental Updates

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

Example output:

```text
Created: .zipcode\code_all_20260626_120927.zip
Files: 38
Manifest: manifest.json
Mode: incremental
Base: .zipcode\code_all_20260626_105421.zip
Changes: +6 ~0 -2 =32
```

Change counters mean:

- `+`: added files
- `~`: updated files
- `-`: removed files
- `=`: unchanged files

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

## Development

Useful commands:

```powershell
dotnet build
dotnet run -- --dry-run --print-files
dotnet run
.\zip-code.ps1 -DryRun -NoDownload
```

The repository intentionally ignores build output, package output, and wrapper cache directories such as `bin`, `obj`, `.zipcode`, and `.zip-code`.
