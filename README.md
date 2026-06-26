# ZipCode

ZipCode packages source code into a ZIP file for ChatGPT context sharing, code review, and handoff between tools.

It reads a JSON configuration, builds a filtered list of files, writes the ZIP directly from that list, and stores a manifest in the archive. Later runs can reuse the newest matching ZIP and update only changed entries.

## Features

- Named scopes such as `Frontend`, `Backend`, `Docs`, or `All`.
- Ordered include/exclude rules where the first matching rule wins.
- Cross-platform path matching on normalized `/` package paths.
- No temporary source-copy directory.
- Manifest with package path, UTC modified time, and file length.
- Incremental ZIP updates from a previous package.
- Safe staging writes before the final ZIP is renamed into place.

## Requirements

- .NET 10 runtime.
- PowerShell if you use the `zip-code.ps1` helper script.

## Quick Start

Copy `zip-code.ps1` and `zip-code.config.json` into your repository, then run from the repository root:

```powershell
.\zip-code.ps1 -DryRun -Print
.\zip-code.ps1
```

The first command previews included files. The second command creates a ZIP in `.zipcode` by default.

## Configuration

`zip-code.config.json` defines the project root, output settings, scopes, and rules.

```json
{
  "$schema": "./zip-code.schema.json",
  "root": ".",
  "defaultScope": "All",
  "defaultAction": "include",
  "output": {
    "directory": ".zipcode",
    "fileName": "code_{scopeLower}_{timestampUtc}.zip",
    "manifestEntryName": "manifest.json",
    "compressionLevel": "Fastest"
  },
  "scopes": {
    "Backend": {
      "sources": [
        "backend"
      ]
    },
    "All": {
      "sources": [
        "frontend",
        "backend",
        "docs"
      ]
    }
  },
  "rules": [
    {
      "name": "Include selected generated sources",
      "action": "include",
      "target": "any",
      "patterns": [
        "backend/My.Project/Generated/**"
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
        "**/dist/**",
        "**/build/**",
        "**/out/**",
        "**/generated/**"
      ]
    },
    {
      "name": "Exclude archives, logs, and lock files",
      "action": "exclude",
      "target": "file",
      "patterns": [
        "**/*.zip",
        "**/*.log",
        "**/*.tsbuildinfo",
        "**/package-lock.json",
        "**/pnpm-lock.yaml",
        "**/yarn.lock"
      ]
    }
  ]
}
```

## Path Rules

- `root` is the project/package root.
- Relative `root` values are resolved from the process working directory.
- Scope `sources` are resolved from `root`.
- `output.directory` is resolved from `root`.
- ZIP paths are relative to `root` and use `/`.
- Console paths are shown relative to `root` when possible.

## Include and Exclude Rules

Rules are evaluated in order. The first matching rule decides whether a file or directory is included or excluded.

Use this order for generated-source exceptions:

```json
[
  {
    "action": "include",
    "target": "any",
    "patterns": [
      "backend/My.Project/Generated/**"
    ]
  },
  {
    "action": "exclude",
    "target": "directory",
    "patterns": [
      "**/generated/**"
    ]
  }
]
```

Supported glob behavior:

- `*` matches within one path segment.
- `?` matches one character within one path segment.
- `**` matches across path segments.
- Matching is case-insensitive by default unless `caseSensitive` is `true`.

## Script Usage

```powershell
# Package the default scope.
.\zip-code.ps1

# Preview files.
.\zip-code.ps1 -DryRun -Print

# Package one scope.
.\zip-code.ps1 -Scope Backend

# Package several scopes as one generated composite scope.
.\zip-code.ps1 -Scope Frontend,Backend

# Use a config from another location.
.\zip-code.ps1 -ConfigurationPath .\tools\zip-code.config.json
```

Useful parameters:

- `-Scope`: one or more scope names. Comma-separated values are accepted.
- `-ConfigurationPath`: path to the config file.
- `-RepositoryRootPath`: root used as the process working directory. Defaults to the current directory.
- `-DryRun`: build the file list without creating a ZIP.
- `-Print`: print included package paths.
- `-Latest`: use the latest GitHub release asset.
- `-NoDownload`: use only a local/cached CLI.

## Output

Example:

```text
Created: .zipcode\code_all_20260626_120927.zip
Files: 38
Manifest: manifest.json
Mode: incremental
Base: .zipcode\code_all_20260626_105421.zip
Changes: +6 ~0 -2 =32
```

Change counters:

- `+`: added files
- `~`: updated files
- `-`: removed files
- `=`: unchanged files

## More Documentation

Development, release, and implementation notes are in [docs/development.md](docs/development.md).
