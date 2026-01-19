# Forlet.AssemblyScanner

[![NuGet](https://img.shields.io/nuget/v/Forlet.AssemblyScanner.svg)](https://www.nuget.org/packages/Forlet.AssemblyScanner/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

A lightweight .NET assembly scanner for discovering types by interface implementation or base class inheritance. Uses `MetadataLoadContext` for safe metadata-only inspection with smart build detection.

Perfect for code generation tools, CLI scaffolders, build-time analyzers, and development tooling.

## Features

- **Smart Build Detection** — Automatically builds projects when DLLs are missing or stale
- **Metadata-Only Loading** — Safe, non-executing inspection via `MetadataLoadContext`
- **Performance Optimized** — Configurable path scanning for large projects
- **Generic Type Support** — Full support using backtick notation (`ICommand`1`)
- **Flexible Filtering** — Include/exclude abstract classes, structs, nested types, non-public types

## Installation

```bash
dotnet add package Forlet.AssemblyScanner
```

## Quick Start

```csharp
using Forlet.AssemblyScanner;

// Resolve and build if needed
var result = await ProjectDllResolver.PrepareAssemblyAsync("/path/to/MyProject.csproj");

// Scan for types
using var scanner = new MetadataScanner(result.DllPath);
var commands = scanner.FindTypesImplementing("ICommand");

foreach (var type in commands)
{
    Console.WriteLine($"Found: {type.FullName}");
}
```

### Check if a Type Exists

```csharp
var result = await ProjectDllResolver.PrepareAssemblyAsync(csprojPath);
using var scanner = new MetadataScanner(result.DllPath);

// Early-exit optimization — stops at first match
var user = scanner.FindTypeByNameDerivedFrom("User", "BaseEntity");

if (user != null)
{
    Console.WriteLine("User entity already exists");
}
```

### Generic Types

Use backtick notation for generic types:

```csharp
// Find ICommand and ICommand<T>
var commands = scanner.FindTypesImplementing(new[] { "ICommand", "ICommand`1" });

// Find IHandler<TRequest, TResponse>
var handlers = scanner.FindTypesImplementing("IHandler`2");
```

## Configuration

### Build Options

```csharp
var result = await ProjectDllResolver.PrepareAssemblyAsync(
    csprojPath,
    new ProjectDllResolverOptions
    {
        BuildStrategy = BuildOptions.AutoBuild,  // AutoBuild | NoBuild | AlwaysBuild
        Configuration = "Debug",
        PathsToCheck = new[] { "Commands", "Handlers" },  // Faster staleness checks
        OnBuildStart = () => Console.WriteLine("Building...")
    }
);
```

| Strategy | Behavior |
|----------|----------|
| `AutoBuild` | Builds if DLL is missing or stale (default) |
| `NoBuild` | Throws if DLL is missing or stale |
| `AlwaysBuild` | Always rebuilds before scanning |

### Scan Options

```csharp
var types = scanner.FindTypesImplementing(
    "IHandler",
    new ScanOptions
    {
        MatchFullName = false,      // Match simple name (default) or full namespace
        IncludeAbstract = false,    // Include abstract classes
        IncludeNonPublic = false,   // Include internal/private types
        IncludeStructs = false,     // Include struct implementations
        IncludeNestedTypes = false  // Include nested types
    }
);
```

## How It Works

1. **Resolve** — Parses `.csproj` for target framework, output path, and assembly name
2. **Stale Check** — Compares DLL timestamp against source files
3. **Build** — Runs `dotnet build` if needed (when using `AutoBuild`)
4. **Load** — Creates `MetadataLoadContext` with runtime assemblies from `.deps.json`
5. **Scan** — Searches types by interface or base class

## Requirements

- .NET 8.0 or higher
- `dotnet` CLI available in PATH (for build functionality)

## Documentation

- [Usage Guide](docs/USAGE.md) — Patterns, examples, and best practices
- [API Reference](docs/API.md) — Complete API documentation

## License

MIT License — see [LICENSE](LICENSE) for details.

---

Part of the [Forlet](https://github.com/whifort/forlet) framework.