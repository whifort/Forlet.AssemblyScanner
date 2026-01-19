# Forlet.AssemblyScanner - Usage Guide

Comprehensive guide with practical examples for using Forlet.AssemblyScanner.

## Table of Contents

1. [Basic Usage](#basic-usage)
2. [Advanced Scenarios](#advanced-scenarios)
3. [Performance Optimization](#performance-optimization)
4. [Error Handling](#error-handling)
5. [Common Patterns](#common-patterns)
6. [Best Practices](#best-practices)

---

## Basic Usage

### Scanning for Interface Implementations

Find all types implementing a specific interface:

```csharp
using Forlet.AssemblyScanner;

// Minimal usage - just the path (uses AutoBuild, Debug configuration)
var result = await ProjectDllResolver.PrepareAssemblyAsync("/path/to/Application.csproj");

using var scanner = new MetadataScanner(result.DllPath);
var commands = scanner.FindTypesImplementing("ICommand");

Console.WriteLine($"Found {commands.Count} command(s)");
foreach (var type in commands)
{
    Console.WriteLine($"  - {type.Name}");
}
```

### Scanning for Base Class Inheritance

Find all types derived from a base class:

```csharp
// Just the path - uses default options
var result = await ProjectDllResolver.PrepareAssemblyAsync("/path/to/Domain.csproj");

using var scanner = new MetadataScanner(result.DllPath);
var models = scanner.FindTypesDerivedFrom("Model");

foreach (var type in models)
{
    Console.WriteLine($"Model: {type.FullName}");
}
```

### Checking Type Existence

Check if a specific type exists before generating it:

```csharp
var result = await ProjectDllResolver.PrepareAssemblyAsync(domainProjectPath);

using var scanner = new MetadataScanner(result.DllPath);

// Check if User model exists
var user = scanner.FindTypeByNameDerivedFrom("User", "Model");

if (user != null)
{
    Console.WriteLine("User model already exists");
    // Don't generate duplicate
}
else
{
    // Safe to generate
    GenerateUserModel();
}
```

---

## Advanced Scenarios

### Working with Generic Types

Use backtick notation for generic types:

```csharp
using var scanner = new MetadataScanner(dllPath);

// Find all command types - both generic and non-generic
var allCommands = scanner.FindTypesImplementing(
    new[] 
    { 
        "ICommand",      // Matches: public class MyCommand : ICommand
        "ICommand`1"     // Matches: public class MyCommand<T> : ICommand<T>
    }
);

// Find commands with two type parameters
var complexCommands = scanner.FindTypesImplementing("ICommand`2");
```

### Full Namespace Matching

When you have types with the same name in different namespaces:

```csharp
using var scanner = new MetadataScanner(dllPath);

// Without MatchFullName (default) - matches any ICommand
var anyCommands = scanner.FindTypesImplementing("ICommand");
// Finds: MyApp.ICommand, ThirdParty.ICommand, etc.

// With MatchFullName - specific namespace only
var myAppCommands = scanner.FindTypesImplementing(
    "MyApp.Commands.ICommand",
    new ScanOptions { MatchFullName = true }
);
// Finds only: MyApp.Commands.ICommand
```

### Including Abstract Classes

By default, abstract classes are excluded:

```csharp
using var scanner = new MetadataScanner(dllPath);

// Only concrete classes
var concreteModels = scanner.FindTypesDerivedFrom("Entity");

// Include abstract classes
var allModels = scanner.FindTypesDerivedFrom(
    "Entity",
    new ScanOptions { IncludeAbstract = true }
);
```

### Scanning Non-Public Types

Include internal and private types:

```csharp
using var scanner = new MetadataScanner(dllPath);

// Only public types (default)
var publicServices = scanner.FindTypesImplementing("IService");

// Include internal types
var allServices = scanner.FindTypesImplementing(
    "IService",
    new ScanOptions { IncludeNonPublic = true }
);
```

### Including Structs

By default, only classes are returned:

```csharp
using var scanner = new MetadataScanner(dllPath);

// Classes only (default)
var handlers = scanner.FindTypesImplementing("IHandler");

// Include structs (value types)
var allHandlers = scanner.FindTypesImplementing(
    "IHandler",
    new ScanOptions { IncludeStructs = true }
);
```

### Including Nested Types

Include types declared inside other types:

```csharp
using var scanner = new MetadataScanner(dllPath);

// Top-level types only (default)
var topLevelHandlers = scanner.FindTypesImplementing("IHandler");

// Include nested classes/structs
var nestedAndTopLevel = scanner.FindTypesImplementing(
    "IHandler",
    new ScanOptions { IncludeNestedTypes = true }
);
```

---

## Performance Optimization

### Use PathsToCheck for Large Projects

For large projects, specify only directories containing your types:

```csharp
// Slow: checks all .cs files in entire project
var slow = await ProjectDllResolver.PrepareAssemblyAsync(
    csprojPath,
    new()
);

// Fast: checks only Commands directory
var fast = await ProjectDllResolver.PrepareAssemblyAsync(
    csprojPath,
    new()
    {
        BuildStrategy = BuildOptions.AutoBuild,
        PathsToCheck = new[] { "Commands" },  // 10x faster!
        CheckForEdit = false  // Default - faster, but may miss edits if directory timestamps don't update
    }
);

using var scanner = new MetadataScanner(fast.DllPath);
var commands = scanner.FindTypesImplementing("ICommand");
```

> ⚠️ **Caution:** Some filesystems do not update directory timestamps for in-place edits.  
> If you need those edits detected, set `CheckForEdit = true` instead of relying on directory timestamps alone.

### Use FindTypeByName for Existence Checks

When you only need to check if a type exists:

```csharp
using var scanner = new MetadataScanner(dllPath);

// Inefficient: scans all types
var allModels = scanner.FindTypesDerivedFrom("Model");
var exists = allModels.Any(t => t.Name == "User");

// Efficient: early exit on first match
var user = scanner.FindTypeByNameDerivedFrom("User", "Model");
var exists = user != null;
```

### Reuse Scanner Instance

Scan multiple times with the same instance:

```csharp
var resolveResult = await ProjectDllResolver.PrepareAssemblyAsync(csprojPath, new());

using var scanner = new MetadataScanner(resolveResult.DllPath);

// Multiple searches with one scanner instance
var commands = scanner.FindTypesImplementing("ICommand");
var queries = scanner.FindTypesImplementing("IQuery");
var models = scanner.FindTypesDerivedFrom("Model");

// More efficient than creating multiple scanners
```

### Disable Auto-Build in CI/CD

In CI/CD environments where builds are managed externally:

```csharp
var resolveResult = await ProjectDllResolver.PrepareAssemblyAsync(
    csprojPath,
    new()
    {
        BuildStrategy = BuildOptions.NoBuild  // Fail if DLL is missing/stale
    }
);

// Will throw if DLL not found or is stale
```

---

## Error Handling

### Handling Build Failures

```csharp
try
{
    var resolveResult = await ProjectDllResolver.PrepareAssemblyAsync(
        csprojPath,
        new()
    );
}
catch (ScanException ex) when (ex.Message.Contains("Failed to build"))
{
    Console.WriteLine("Build failed. Run 'dotnet build' manually to see details.");
    // Handle build failure
}
```

### Handling Missing Projects

```csharp
try
{
    var resolveResult = await ProjectDllResolver.PrepareAssemblyAsync(
        csprojPath,
        new()
    );
}
catch (ScanException ex) when (ex.Message.Contains("target framework"))
{
    Console.WriteLine("Invalid or missing target framework in .csproj");
    // Handle configuration error
}
catch (ScanException ex) when (ex.Message.Contains("not found"))
{
    Console.WriteLine("Project file not found");
    // Handle missing file
}
```

### Handling DLL Load Failures

```csharp
try
{
    using var scanner = new MetadataScanner(dllPath);
    var types = scanner.FindTypesImplementing("ICommand");
}
catch (ScanException ex)
{
    Console.WriteLine($"Failed to load assembly: {ex.Message}");
    // Handle load failure
}
```

### Cancellation Support

```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

try
{
    var resolveResult = await ProjectDllResolver.PrepareAssemblyAsync(
        csprojPath,
        new(),
        cancellationToken: cts.Token
    );
}
catch (OperationCanceledException)
{
    Console.WriteLine("Resolve operation timed out");
}
```

---

## Common Patterns

### Pattern: Validation Before Generation

```csharp
public async Task<bool> CanGenerateAsync(string typeName, string csprojPath)
{
    var resolveResult = await ProjectDllResolver.PrepareAssemblyAsync(
        csprojPath,
        new() { PathsToCheck = new[] { "Entities" } }
    );
    
    using var scanner = new MetadataScanner(resolveResult.DllPath);
    var existing = scanner.FindTypeByNameDerivedFrom(typeName, "Model");
    
    return existing == null;  // Can generate if doesn't exist
}
```

### Pattern: Listing Available Types

```csharp
public async Task ListCommandsAsync(string appProjectPath)
{
    var resolveResult = await ProjectDllResolver.PrepareAssemblyAsync(
        appProjectPath,
        new() { PathsToCheck = new[] { "Commands" } }
    );
    
    using var scanner = new MetadataScanner(resolveResult.DllPath);
    var commands = scanner.FindTypesImplementing(
        new[] { "ICommand", "ICommand`1" }
    );
    
    Console.WriteLine($"\nFound {commands.Count} command(s):");
    foreach (var type in commands.OrderBy(t => t.Name))
    {
        Console.WriteLine($"  • {type.Name}");
    }
}
```

### Pattern: Batch Processing

```csharp
public async Task ProcessAllModelsAsync(string domainPath)
{
    var resolveResult = await ProjectDllResolver.PrepareAssemblyAsync(
        domainPath,
        new() { PathsToCheck = new[] { "Entities" } }
    );
    
    using var scanner = new MetadataScanner(resolveResult.DllPath);
    var models = scanner.FindTypesDerivedFrom("Model");
    
    var tasks = models.Select(type => ProcessModelAsync(type));
    await Task.WhenAll(tasks);
}
```

### Pattern: With Build Feedback

```csharp
public async Task ScanWithFeedbackAsync(string csprojPath)
{
    Console.WriteLine("Preparing project...");
    
    var resolveResult = await ProjectDllResolver.PrepareAssemblyAsync(
        csprojPath,
        new() { BuildStrategy = BuildOptions.AutoBuild }
    );
    
    if (resolveResult.BuiltAutomatically)
    {
        Console.WriteLine("✓ Project was built automatically");
    }
    else
    {
        Console.WriteLine("✓ Using up-to-date DLL");
    }
    
    Console.WriteLine("Scanning for types...");
    using var scanner = new MetadataScanner(resolveResult.DllPath);
    var types = scanner.FindTypesImplementing("ICommand");
    
    Console.WriteLine($"✓ Found {types.Count} types");
}
```

---

## Best Practices

### 1. Use PathsToCheck for Large Projects

Always specify `PathsToCheck` when you know where your types are located:

```csharp
// ✅ Good - fast staleness check
new ProjectDllResolverOptions 
{ 
    PathsToCheck = new[] { "Commands", "Handlers" } 
}

// ❌ Slow - checks entire project
new ProjectDllResolverOptions 
{ 
    PathsToCheck = null 
}
```

### 2. Use FindTypeByName for Existence Checks

Don't scan all types just to check if one exists:

```csharp
// ❌ Bad - scans everything
var all = scanner.FindTypesDerivedFrom("Model");
var exists = all.Any(t => t.Name == "User");

// ✅ Good - early exit
var user = scanner.FindTypeByNameDerivedFrom("User", "Model");
var exists = user != null;
```

### 3. Use `using` for Scanner Disposal

Always dispose the scanner to release resources:

```csharp
// ✅ Good - automatic disposal
using var scanner = new MetadataScanner(dllPath);
var types = scanner.FindTypesImplementing("ICommand");

// ❌ Avoid - manual disposal
var scanner = new MetadataScanner(dllPath);
try
{
    var types = scanner.FindTypesImplementing("ICommand");
}
finally
{
    scanner.Dispose();
}
```

### 4. Handle Build Metadata

Use the `BuiltAutomatically` flag for user feedback:

```csharp
var resolveResult = await ProjectDllResolver.PrepareAssemblyAsync(csprojPath, new());

if (resolveResult.BuiltAutomatically)
{
    Console.WriteLine("ℹ️  Project was built automatically");
}

Console.WriteLine($"DLL: {resolveResult.DllPath}");
```

### 5. Validate Paths Before Scanning

```csharp
if (!File.Exists(csprojPath))
{
    throw new InvalidOperationException($"Project not found: {csprojPath}");
}

var resolveResult = await ProjectDllResolver.PrepareAssemblyAsync(csprojPath, new());
```

### 6. Use Appropriate Build Strategies

Choose the right build strategy for your scenario:

```csharp
// ✅ Development - smart build
BuildStrategy = BuildOptions.AutoBuild

// ✅ CI/CD - fail if not built
BuildStrategy = BuildOptions.NoBuild

// ✅ Analysis tools - always fresh
BuildStrategy = BuildOptions.AlwaysBuild
```

### 7. Reuse Scanner for Multiple Searches

If scanning the same project multiple times, reuse the scanner:

```csharp
var resolveResult = await ProjectDllResolver.PrepareAssemblyAsync(csprojPath, new());

using var scanner = new MetadataScanner(resolveResult.DllPath);

var commands = scanner.FindTypesImplementing("ICommand");
var queries = scanner.FindTypesImplementing("IQuery");
var models = scanner.FindTypesDerivedFrom("Model");

// More efficient than multiple PrepareAssemblyAsync calls
```

---

## Troubleshooting

### Issue: "Could not determine target framework"

**Solution:** Ensure your `.csproj` contains `<TargetFramework>` element:

```xml
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFramework>net8.0</TargetFramework>
    </PropertyGroup>
</Project>
```

### Issue: Staleness check is slow on large project

**Solution:** Use `PathsToCheck` option:

```csharp
new ProjectDllResolverOptions 
{ 
    PathsToCheck = new[] { "Commands", "Handlers" } 
}
```

### Issue: Build fails during preparation

**Solution:** Check build manually and use NoBuild:

```bash
dotnet build YourProject.csproj
```

Then scan with:
```csharp
new ProjectDllResolverOptions 
{ 
    BuildStrategy = BuildOptions.NoBuild 
}
```

### Issue: DLL not found at expected path

**Solution:** Verify target framework and configuration match, and account for any `.csproj` overrides like `AssemblyName`, `OutputPath`, or `BaseOutputPath` that move or rename the output:

```csharp
var resolveResult = await ProjectDllResolver.PrepareAssemblyAsync(
    csprojPath,
    new()
    {
        Configuration = "Debug",  // Must match what's built
        BuildStrategy = BuildOptions.AlwaysBuild
    }
);
```

### Issue: Runtime assemblies are resolved from the wrong framework

**Solution:** Ensure the `.deps.json` file exists next to the DLL and matches the intended target framework/runtime version. `MetadataScanner` uses the `.deps.json` runtime target to prioritize assemblies from the matching runtime before falling back to the current runtime.

---

## See Also

- [API Reference](API.md) - Complete API documentation
- [README](../README.md) - Quick start guide
- [GitHub Issues](https://github.com/whifort/Forlet.AssemblyScanner/issues) - Support
