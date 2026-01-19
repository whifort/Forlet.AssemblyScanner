# Forlet.AssemblyScanner - API Reference

Complete API reference for Forlet.AssemblyScanner package.

## Overview

The API consists of two main classes:
1. **ProjectDllResolver** (static) - Resolves and optionally builds DLL
2. **MetadataScanner** (instance) - Scans for types using metadata

## Minimal Usage Pattern

```csharp
// Step 1: Resolve DLL (uses defaults: AutoBuild, Debug)
var result = await ProjectDllResolver.PrepareAssemblyAsync(csprojPath);

// Step 2: Create scanner
using var scanner = new MetadataScanner(result.DllPath);

// Step 3: Search for types
var types = scanner.FindTypesImplementing("ICommand");
```

## With Custom Options

```csharp
// Resolve with custom options
var result = await ProjectDllResolver.PrepareAssemblyAsync(
    csprojPath,
    new() { BuildStrategy = BuildOptions.AutoBuild }
);

// Create scanner and search
using var scanner = new MetadataScanner(result.DllPath);
var types = scanner.FindTypesImplementing(
    "ICommand",
    new ScanOptions { IncludeAbstract = true }
);
```

---

## ProjectDllResolver (Static Class)

Resolves project DLL paths, handles building, and detects staleness. The resolver reads `.csproj` settings like `AssemblyName`, `OutputPath`, and `BaseOutputPath` to determine the final DLL location when they are defined.

### PrepareAssemblyAsync

```csharp
public static Task<DllResolverResult> PrepareAssemblyAsync(
    string csprojPath,
    ProjectDllResolverOptions? options = null,
    CancellationToken cancellationToken = default)
```

**Parameters:**
- `csprojPath` - Path to .csproj file (absolute or relative)
- `options` - Optional configuration (null = uses defaults)
- `cancellationToken` - Optional cancellation token

**Returns:** `DllResolverResult` containing:
- `DllPath` - Absolute path to compiled DLL
- `BuiltAutomatically` - Whether build was triggered
- `BuildOutput` - Build stdout if built

**Throws:**
- `ArgumentException` - Invalid path
- `ScanException` - Build failed, DLL not found, or invalid project

**Defaults (when options is null):**
- BuildStrategy: `AutoBuild`
- Configuration: `Debug`
- PathsToCheck: `null` (checks entire project)

**Example - Minimal Usage:**
```csharp
var result = await ProjectDllResolver.PrepareAssemblyAsync(csprojPath);
Console.WriteLine($"DLL: {result.DllPath}");
```

**Example - With Options:**
```csharp
var result = await ProjectDllResolver.PrepareAssemblyAsync(
    csprojPath,
    new()
    {
        BuildStrategy = BuildOptions.AutoBuild,
        Configuration = "Debug",
        PathsToCheck = new[] { "Commands", "Handlers" },
        CheckForEdit = false,
        OnBuildStart = () => Console.WriteLine("Building...")
    }
);
```

---

## MetadataScanner (Class)

Scans assemblies for types using `MetadataLoadContext` for safe, metadata-only inspection.

### Constructor

```csharp
public MetadataScanner(string dllPath)
```

**Parameters:**
- `dllPath` - Path to the DLL to scan

**Throws:**
- `ArgumentException` - Invalid path
- `ScanException` - DLL cannot be loaded

**Example:**
```csharp
using var scanner = new MetadataScanner(dllPath);
// Use scanner methods...
```

**Assembly Resolution Notes:**
- `MetadataScanner` reads the `.deps.json` next to the DLL to determine the target framework/runtime version and prioritizes assemblies from that runtime.
- If target runtime data cannot be determined, it falls back to the current runtime directories.

### FindTypesImplementing (Single Interface)

```csharp
public List<Type> FindTypesImplementing(
    string interfaceName,
    ScanOptions? options = null)
```

**Parameters:**
- `interfaceName` - Interface name (use backtick notation for generics: "ICommand`1")
- `options` - Optional scanning configuration

**Returns:** List of types implementing the interface

**Example:**
```csharp
var commands = scanner.FindTypesImplementing("ICommand");

var genericAndNonGeneric = scanner.FindTypesImplementing(
    "ICommand`1",
    new ScanOptions { IncludeAbstract = true }
);
```

---

### FindTypesImplementing (Multiple Interfaces)

```csharp
public List<Type> FindTypesImplementing(
    string[] interfaceNames,
    ScanOptions? options = null)
```

**Parameters:**
- `interfaceNames` - Array of interface names
- `options` - Optional scanning configuration

**Returns:** List of types implementing any of the specified interfaces

**Example:**
```csharp
var handlers = scanner.FindTypesImplementing(
    new[] { "ICommand", "ICommand`1", "IQuery" }
);
```

---

### FindTypesDerivedFrom (Single Base Class)

```csharp
public List<Type> FindTypesDerivedFrom(
    string baseTypeName,
    ScanOptions? options = null)
```

**Parameters:**
- `baseTypeName` - Base class name (use backtick notation for generics)
- `options` - Optional scanning configuration

**Returns:** List of types derived from the base class

**Example:**
```csharp
var models = scanner.FindTypesDerivedFrom("Model");
```

---

### FindTypesDerivedFrom (Multiple Base Classes)

```csharp
public List<Type> FindTypesDerivedFrom(
    string[] baseTypeNames,
    ScanOptions? options = null)
```

**Parameters:**
- `baseTypeNames` - Array of base class names
- `options` - Optional scanning configuration

**Returns:** List of types derived from any of the specified base classes

**Example:**
```csharp
var entities = scanner.FindTypesDerivedFrom(
    new[] { "Entity", "AggregateRoot" }
);
```

---

### FindTypeByNameImplementing (Single Interface)

```csharp
public Type? FindTypeByNameImplementing(
    string targetTypeName,
    string interfaceName,
    ScanOptions? options = null)
```

**Parameters:**
- `targetTypeName` - Specific type name to find
- `interfaceName` - Interface name the type must implement
- `options` - Optional scanning configuration

**Returns:** The found type, or null if not found

**Performance:** Uses early exit optimization - stops as soon as type is found

**Example:**
```csharp
var createUserCommand = scanner.FindTypeByNameImplementing(
    "CreateUserCommand",
    "ICommand"
);

if (createUserCommand != null)
{
    Console.WriteLine("Type exists");
}
```

---

### FindTypeByNameImplementing (Multiple Interfaces)

```csharp
public Type? FindTypeByNameImplementing(
    string targetTypeName,
    string[] interfaceNames,
    ScanOptions? options = null)
```

**Parameters:**
- `targetTypeName` - Specific type name to find
- `interfaceNames` - Array of interface names
- `options` - Optional scanning configuration

**Returns:** The found type, or null if not found

**Example:**
```csharp
var handler = scanner.FindTypeByNameImplementing(
    "UserCommandHandler",
    new[] { "ICommandHandler", "ICommandHandler`1" }
);
```

---

### FindTypeByNameDerivedFrom (Single Base Class)

```csharp
public Type? FindTypeByNameDerivedFrom(
    string targetTypeName,
    string baseTypeName,
    ScanOptions? options = null)
```

**Parameters:**
- `targetTypeName` - Specific type name to find
- `baseTypeName` - Base class name the type must derive from
- `options` - Optional scanning configuration

**Returns:** The found type, or null if not found

**Example:**
```csharp
var user = scanner.FindTypeByNameDerivedFrom("User", "Model");
```

---

### FindTypeByNameDerivedFrom (Multiple Base Classes)

```csharp
public Type? FindTypeByNameDerivedFrom(
    string targetTypeName,
    string[] baseTypeNames,
    ScanOptions? options = null)
```

**Parameters:**
- `targetTypeName` - Specific type name to find
- `baseTypeNames` - Array of base class names
- `options` - Optional scanning configuration

**Returns:** The found type, or null if not found

---

### Dispose

```csharp
public void Dispose()
```

Releases resources and unloads the MetadataLoadContext. Should be called when done scanning.

**Example:**
```csharp
using var scanner = new MetadataScanner(dllPath);
// Automatically disposed at end of using block
```

---

## ProjectDllResolverOptions (Class)

Configuration for DLL resolution and building.

```csharp
public class ProjectDllResolverOptions
{
    public BuildOptions BuildStrategy { get; init; } = BuildOptions.AutoBuild;
    public string Configuration { get; init; } = "Debug";
    public string[]? PathsToCheck { get; init; }
    public bool CheckForEdit { get; init; } = false;
    public Action? OnBuildStart { get; init; }
}
```

### BuildStrategy

```csharp
public BuildOptions BuildStrategy { get; init; } = BuildOptions.AutoBuild;
```

Specifies when and how to build the project.

**Values:**
- `BuildOptions.AutoBuild` - Build if DLL is missing or stale (default)
- `BuildOptions.NoBuild` - Throw if DLL is missing or stale
- `BuildOptions.AlwaysBuild` - Always rebuild the project

**Default:** `BuildOptions.AutoBuild`

---

### Configuration

```csharp
public string Configuration { get; init; } = "Debug";
```

Build configuration to use (Debug or Release).

**Default:** `"Debug"`

Used for:
- Invoking `dotnet build --configuration`
- Locating DLL in `bin/{Configuration}/{TargetFramework}/` (or under `BaseOutputPath` when defined)
- Ignored for path resolution when `OutputPath` is explicitly set in the project file

---

### PathsToCheck

```csharp
public string[]? PathsToCheck { get; init; }
```

Specific directories to check for staleness (relative to project directory).

**Default:** `null` (checks entire project)

**When null/empty:**
- All .cs files in project are checked for staleness
- Slower for large projects

**When specified:**
- Only .cs files in specified directories are checked
- Significantly faster for large projects
- Does NOT affect which types are scanned - all types are available

**Example:**
```csharp
new ProjectDllResolverOptions
{
    PathsToCheck = new[] { "Entities", "Commands", "Handlers" }
}
```

---

### CheckForEdit

```csharp
public bool CheckForEdit { get; init; } = false;
```

Controls whether to check file timestamps when directory timestamps haven't changed.

**Default:** `false` (performance-optimized, but may miss edits on filesystems that do not update directory timestamps)

**When true:**
- Checks both directory and file timestamps
- More thorough change detection
- Slightly slower (enumerates files even when directory unchanged)

**When false:**
- Checks directory timestamps only
- Relies on directory timestamp updates (standard OS behavior)
- Faster (skips file enumeration when directory timestamp unchanged)

**Recommendation:**
- Use `false` (default) for most cases when directory timestamps are reliable
- Use `true` if you need to detect in-place file edits without directory modification or if your filesystem does not update directory timestamps

> ⚠️ **Caution:** Some filesystems do not update directory timestamps for in-place edits.  
> If you rely on those edits being detected, prefer `CheckForEdit = true`.

**Example:**
```csharp
// Fast mode (default) - skip file enumeration if dirs unchanged
new ProjectDllResolverOptions
{
    PathsToCheck = new[] { "Entities" },
    CheckForEdit = false  // Default - faster, but may miss edits if directory timestamps don't update
}
```

---

### OnBuildStart

```csharp
public Action? OnBuildStart { get; init; }
```

Optional callback action invoked when a build is about to start.

**Type:** `System.Action?` (parameterless action)

**Default:** `null` (no callback)

**When Called:**
- Before build process starts
- Only if build is actually triggered
- Not called if DLL is already up-to-date

**Use Cases:**
- Logging build events
- Updating UI progress indicators
- Tracking build statistics
- Analytics and monitoring

**Example:**
```csharp
// Track builds with callback
new ProjectDllResolverOptions
{
    OnBuildStart = () => Console.WriteLine("🏗️ Building...")
}

// With tracking variable
var buildCount = 0;
new ProjectDllResolverOptions
{
    OnBuildStart = () => buildCount++
}
```

---

## ScanOptions (Class)

Configuration for type scanning.

```csharp
public sealed class ScanOptions
{
    public bool MatchFullName { get; init; } = false;
    public bool IncludeAbstract { get; init; } = false;
    public bool IncludeNonPublic { get; init; } = false;
    public bool IncludeStructs { get; init; } = false;
    public bool IncludeNestedTypes { get; init; } = false;
}
```

### MatchFullName

```csharp
public bool MatchFullName { get; init; } = false;
```

Match full type names including namespace.

**Default:** `false` (matches simple name only)

**When false:**
- "ICommand" matches any ICommand in any namespace
- `MyApp.ICommand` and `ThirdParty.ICommand` both match

**When true:**
- Must specify full namespace in type name
- "MyApp.ICommand" matches only ICommand in MyApp namespace

---

### IncludeAbstract

```csharp
public bool IncludeAbstract { get; init; } = false;
```

Include abstract classes in scan results.

**Default:** `false` (concrete classes only)

**When false:**
- Only concrete classes returned

**When true:**
- Both abstract and concrete classes returned

---

### IncludeNonPublic

```csharp
public bool IncludeNonPublic { get; init; } = false;
```

Include non-public (internal/private) types.

**Default:** `false` (public types only)

**When false:**
- Only public types returned

**When true:**
- Public, internal, and private types returned

---

### IncludeStructs

```csharp
public bool IncludeStructs { get; init; } = false;
```

Include structs (value types) in scan results.

**Default:** `false` (classes only)

**When false:**
- Only classes returned

**When true:**
- Classes and structs returned

---

### IncludeNestedTypes

```csharp
public bool IncludeNestedTypes { get; init; } = false;
```

Include nested types (types declared inside other types).

**Default:** `false` (top-level types only)

**When false:**
- Nested types excluded

**When true:**
- Nested and top-level types included

---

## BuildOptions (Enum)

Specifies build strategy for DLL resolution.

```csharp
public enum BuildOptions
{
    NoBuild,      // Never build
    AutoBuild,    // Build if needed (default)
    AlwaysBuild   // Always build
}
```

### NoBuild

Never build the project. Throws `ScanException` if DLL is missing or stale.

**Use case:** CI/CD environments where builds are managed externally.

### AutoBuild

Build if needed (DLL missing or stale). Checks staleness by:
- DLL doesn't exist
- .csproj is newer than DLL
- Source files (in PathsToCheck or entire project) are newer than DLL

**Use case:** Development and CLI tools (default behavior).

### AlwaysBuild

Always rebuild the project before scanning.

**Use case:** Ensuring absolute freshness when code may have been modified.

---

## DllResolverResult (Class)

Result of DLL resolution operation.

```csharp
public sealed class DllResolverResult
{
    public required string DllPath { get; init; }
    public required bool BuiltAutomatically { get; init; }
    public string? BuildOutput { get; init; }
}
```

### DllPath

```csharp
public required string DllPath { get; init; }
```

Absolute path to the compiled DLL. Ready to use with `MetadataScanner`.

---

### BuiltAutomatically

```csharp
public required bool BuiltAutomatically { get; init; }
```

Whether the project was built during this resolution.

**true** - Scanner detected stale/missing DLL and invoked `dotnet build`  
**false** - Up-to-date DLL was found and used directly

---

### BuildOutput

```csharp
public string? BuildOutput { get; init; }
```

Build output (stdout from `dotnet build`) if project was built, otherwise null.

Useful for diagnostics and logging.

---

## ScanException (Class)

Exception thrown when scan operations fail.

```csharp
public class ScanException : Exception
{
    public ScanException(string message);
    public ScanException(string message, Exception innerException);
}
```

**Common scenarios:**
- Build failure
- Invalid .csproj
- Missing target framework
- DLL not found and NoBuild enabled
- Assembly load failure

---

## Generic Type Notation

Use backtick notation for generic types:

| Type | Notation | Example |
|------|----------|---------|
| `ICommand` | `"ICommand"` | Non-generic interface |
| `ICommand<T>` | `"ICommand`1"` | Single type parameter |
| `ICommand<T1, T2>` | `"ICommand`2"` | Two type parameters |
| `Entity<TId>` | `"Entity`1"` | Generic base class |

**Example:**
```csharp
var allCommands = scanner.FindTypesImplementing(
    new[] { "ICommand", "ICommand`1" }
);
```

---

## Thread Safety

- `ProjectDllResolver` is **not thread-safe** for concurrent operations on the same project
- `MetadataScanner` is **thread-safe** for reading
- Use different projects or implement external locking for concurrent access

---

## Requirements

- .NET 8.0 or higher
- `dotnet` CLI available in PATH (for build functionality)

---

## See Also

- [README](../README.md) - Quick start guide
- [Usage Guide](USAGE.md) - Common patterns and scenarios
