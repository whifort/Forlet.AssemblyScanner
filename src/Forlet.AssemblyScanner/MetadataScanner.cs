using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace Forlet.AssemblyScanner;

/// <summary>
/// Scans assemblies using MetadataLoadContext for metadata-only inspection.
/// </summary>
public sealed class MetadataScanner : IDisposable
{
    private readonly MetadataLoadContext _context;
    private readonly Assembly _assembly;

    /// <summary>
    /// Initializes a new instance of the MetadataScanner class.
    /// </summary>
    /// <param name="dllPath">Path to the assembly DLL to scan.</param>
    /// <exception cref="ScanException">Thrown if the assembly cannot be loaded.</exception>
    public MetadataScanner(string dllPath)
    {
        try
        {
            var assemblyPaths = CollectAssemblyPaths(dllPath);
            var resolver = new PathAssemblyResolver(assemblyPaths);

            // Explicitly specify the core assembly name for MetadataLoadContext
            // This is required to properly resolve fundamental types like System.Object
            _context = new MetadataLoadContext(resolver, coreAssemblyName: "System.Runtime");
            _assembly = _context.LoadFromAssemblyPath(dllPath);
        }
        catch (ScanException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Include inner exception details for better diagnostics
            var message = $"Failed to load assembly from {dllPath}";
            if (ex.InnerException != null)
            {
                message += $". Inner error: {ex.InnerException.Message}";
            }
            message += $". Error type: {ex.GetType().Name}. Details: {ex.Message}";
            throw new ScanException(message, ex);
        }
    }

    /// <summary>
    /// Collects all assembly paths needed for MetadataLoadContext resolution.
    /// Includes: BCL assemblies, project bin directory, project references, and NuGet packages.
    /// </summary>
    internal static IEnumerable<string> CollectAssemblyPaths(
        string dllPath,
        string? targetFrameworkMoniker = null,
        string? runtimeVersion = null)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1. Parse .deps.json for project references and NuGet packages + target runtime info
        DepsJsonInfo? depsJsonInfo = null;
        var depsJsonPath = Path.ChangeExtension(dllPath, ".deps.json");
        if (File.Exists(depsJsonPath))
        {
            depsJsonInfo = ParseDepsJson(depsJsonPath);
        }

        var effectiveTargetFramework = targetFrameworkMoniker ?? depsJsonInfo?.TargetFrameworkMoniker;
        var effectiveRuntimeVersion = runtimeVersion ?? depsJsonInfo?.RuntimeVersion;

        // 2. Add BCL/runtime assemblies - prefer target runtime when available
        var runtimeAssemblies = GetRuntimeAssemblyPaths(effectiveTargetFramework, effectiveRuntimeVersion);
        foreach (var dll in runtimeAssemblies)
        {
            paths.Add(dll);
        }

        // 3. Add assemblies from project bin directory
        var projectBinDir = Path.GetDirectoryName(dllPath)!;
        foreach (var dll in Directory.GetFiles(projectBinDir, "*.dll"))
        {
            paths.Add(dll);
        }

        // 4. Add project references and NuGet packages from deps.json
        if (depsJsonInfo != null)
        {
            foreach (var assemblyPath in depsJsonInfo.AssemblyPaths)
            {
                if (File.Exists(assemblyPath))
                {
                    paths.Add(assemblyPath);
                }
            }
        }

        return paths;
    }

    /// <summary>
    /// Gets the paths to runtime assemblies using multiple strategies.
    /// </summary>
    private static IEnumerable<string> GetRuntimeAssemblyPaths(string? targetFrameworkMoniker, string? runtimeVersion)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Strategy 1: Use target runtime data when provided
        if (!string.IsNullOrWhiteSpace(targetFrameworkMoniker) || !string.IsNullOrWhiteSpace(runtimeVersion))
        {
            foreach (var dll in FindDotNetRuntimePaths(targetFrameworkMoniker, runtimeVersion))
            {
                paths.Add(dll);
            }

            if (paths.Count > 0)
            {
                return paths;
            }
        }

        // Strategy 2: Use the directory containing the core library from current runtime
        var coreLibPath = typeof(object).Assembly.Location;
        if (!string.IsNullOrEmpty(coreLibPath) && File.Exists(coreLibPath))
        {
            var runtimeDir = Path.GetDirectoryName(coreLibPath);
            if (!string.IsNullOrEmpty(runtimeDir) && Directory.Exists(runtimeDir))
            {
                foreach (var dll in Directory.GetFiles(runtimeDir, "*.dll"))
                {
                    paths.Add(dll);
                }
            }
        }

        // Strategy 3: Use RuntimeEnvironment.GetRuntimeDirectory()
        try
        {
            var runtimeEnvDir = RuntimeEnvironment.GetRuntimeDirectory();
            if (!string.IsNullOrEmpty(runtimeEnvDir) && Directory.Exists(runtimeEnvDir))
            {
                foreach (var dll in Directory.GetFiles(runtimeEnvDir, "*.dll"))
                {
                    paths.Add(dll);
                }
            }
        }
        catch
        {
            // RuntimeEnvironment.GetRuntimeDirectory() is not available on all platforms; skip gracefully
        }

        // Strategy 4: Find .NET runtime from DOTNET_ROOT or default installation paths
        var dotnetRuntimePaths = FindDotNetRuntimePaths(null, null);
        foreach (var dll in dotnetRuntimePaths)
        {
            paths.Add(dll);
        }

        // Strategy 5: Use AppContext.BaseDirectory as another fallback
        try
        {
            var baseDir = AppContext.BaseDirectory;
            if (!string.IsNullOrEmpty(baseDir) && Directory.Exists(baseDir))
            {
                foreach (var dll in Directory.GetFiles(baseDir, "*.dll"))
                {
                    paths.Add(dll);
                }
            }
        }
        catch
        {
            // AppContext.BaseDirectory may not be available in all contexts; skip gracefully
        }

        return paths;
    }

    /// <summary>
    /// Finds .NET runtime assemblies from standard installation locations.
    /// </summary>
    private static IEnumerable<string> FindDotNetRuntimePaths(string? targetFrameworkMoniker, string? runtimeVersion)
    {
        // 1. Get the directory of the ACTIVE runtime (Resilient & Cross-platform)
        string runtimeDir = RuntimeEnvironment.GetRuntimeDirectory();

        // 2. Return all DLLs in that folder
        return Directory.EnumerateFiles(runtimeDir, "*.dll");
    }

    /// <summary>
    /// Parses the .deps.json file to extract paths to all runtime dependencies.
    /// </summary>
    private static DepsJsonInfo ParseDepsJson(string depsJsonPath)
    {
        var results = new List<string>();
        string? targetFrameworkMoniker = null;
        string? runtimeVersion = null;

        try
        {
            var jsonContent = File.ReadAllText(depsJsonPath);
            using var doc = JsonDocument.Parse(jsonContent);
            var root = doc.RootElement;

            // Get the runtime target (e.g., ".NETCoreApp,Version=v8.0")
            if (!root.TryGetProperty("runtimeTarget", out var runtimeTarget))
                return new DepsJsonInfo(results, targetFrameworkMoniker, runtimeVersion);

            if (!runtimeTarget.TryGetProperty("name", out var targetNameElement))
                return new DepsJsonInfo(results, targetFrameworkMoniker, runtimeVersion);

            var targetName = targetNameElement.GetString();
            if (string.IsNullOrEmpty(targetName))
                return new DepsJsonInfo(results, targetFrameworkMoniker, runtimeVersion);

            targetFrameworkMoniker = TryGetTargetFrameworkMoniker(targetName);

            // Get the targets section
            if (!root.TryGetProperty("targets", out var targets))
                return new DepsJsonInfo(results, targetFrameworkMoniker, runtimeVersion);

            if (!targets.TryGetProperty(targetName, out var targetLibraries))
                return new DepsJsonInfo(results, targetFrameworkMoniker, runtimeVersion);

            // Get the libraries section (contains path info)
            if (!root.TryGetProperty("libraries", out var libraries))
                return new DepsJsonInfo(results, targetFrameworkMoniker, runtimeVersion);

            // Get NuGet packages path
            var nugetPackagesPath = GetNuGetPackagesPath();

            // Iterate through all libraries in the target
            foreach (var library in targetLibraries.EnumerateObject())
            {
                var libraryName = library.Name; // e.g., "MyProject/1.0.0" or "Newtonsoft.Json/13.0.1"

                if (runtimeVersion == null)
                {
                    runtimeVersion = TryGetRuntimeVersion(libraryName);
                }

                // Get library metadata
                if (!libraries.TryGetProperty(libraryName, out var libraryMeta))
                    continue;

                if (!libraryMeta.TryGetProperty("type", out var typeElement))
                    continue;

                var libraryType = typeElement.GetString();

                // Get the path from library metadata
                string? libraryBasePath = null;
                if (libraryMeta.TryGetProperty("path", out var pathElement))
                {
                    libraryBasePath = pathElement.GetString();
                }

                // Get runtime assemblies
                if (!library.Value.TryGetProperty("runtime", out var runtime))
                    continue;

                foreach (var runtimeAsm in runtime.EnumerateObject())
                {
                    var relativeDllPath = runtimeAsm.Name; // e.g., "lib/net8.0/MyLibrary.dll"

                    string? fullPath = null;

                    if (libraryType == "package" && !string.IsNullOrEmpty(libraryBasePath))
                    {
                        // NuGet package - resolve from packages folder
                        fullPath = Path.Combine(nugetPackagesPath, libraryBasePath, relativeDllPath);
                    }
                    else if (libraryType == "project" && !string.IsNullOrEmpty(libraryBasePath))
                    {
                        // Project reference - the path in deps.json is relative to the output
                        // But the DLL should already be in bin directory (copied on build)
                        // So we primarily rely on bin directory scan, but try to resolve anyway
                        var depsDir = Path.GetDirectoryName(depsJsonPath)!;
                        fullPath = Path.Combine(depsDir, Path.GetFileName(relativeDllPath));
                    }

                    if (!string.IsNullOrEmpty(fullPath))
                    {
                        // Normalize path separators
                        fullPath = fullPath.Replace('/', Path.DirectorySeparatorChar);
                        results.Add(fullPath);
                    }
                }
            }
        }
        catch (JsonException)
        {
            // If we can't parse deps.json, just continue without those dependencies
            // The assemblies in bin directory might be sufficient
        }
        catch (Exception)
        {
            // Silently ignore parsing errors - fall back to bin directory assemblies
        }

        return new DepsJsonInfo(results, targetFrameworkMoniker, runtimeVersion);
    }

    private static string? TryGetTargetFrameworkMoniker(string targetName)
    {
        var frameworkPart = targetName.Split('/')[0];

        if (frameworkPart.StartsWith(".NETCoreApp,Version=v", StringComparison.OrdinalIgnoreCase))
        {
            var version = frameworkPart.Replace(".NETCoreApp,Version=v", string.Empty, StringComparison.OrdinalIgnoreCase);
            return $"net{version}";
        }

        if (frameworkPart.StartsWith(".NETStandard,Version=v", StringComparison.OrdinalIgnoreCase))
        {
            var version = frameworkPart.Replace(".NETStandard,Version=v", string.Empty, StringComparison.OrdinalIgnoreCase);
            return $"netstandard{version}";
        }

        if (frameworkPart.StartsWith(".NETFramework,Version=v", StringComparison.OrdinalIgnoreCase))
        {
            var version = frameworkPart.Replace(".NETFramework,Version=v", string.Empty, StringComparison.OrdinalIgnoreCase);
            return $"net{version.Replace(".", string.Empty)}";
        }

        return null;
    }

    private static string? TryGetRuntimeVersion(string libraryName)
    {
        if (libraryName.StartsWith("Microsoft.NETCore.App/", StringComparison.OrdinalIgnoreCase) ||
            libraryName.StartsWith("Microsoft.NETCore.App.Runtime.", StringComparison.OrdinalIgnoreCase))
        {
            var lastSlashIndex = libraryName.LastIndexOf('/');
            if (lastSlashIndex >= 0 && lastSlashIndex < libraryName.Length - 1)
            {
                return libraryName[(lastSlashIndex + 1)..];
            }
        }

        return null;
    }

    private static string? GetTargetFrameworkVersion(string? targetFrameworkMoniker)
    {
        if (string.IsNullOrWhiteSpace(targetFrameworkMoniker))
        {
            return null;
        }

        if (targetFrameworkMoniker.StartsWith("netstandard", StringComparison.OrdinalIgnoreCase))
        {
            return targetFrameworkMoniker["netstandard".Length..];
        }

        if (targetFrameworkMoniker.StartsWith("net", StringComparison.OrdinalIgnoreCase) &&
            targetFrameworkMoniker.Length > 3 &&
            char.IsDigit(targetFrameworkMoniker[3]))
        {
            return targetFrameworkMoniker["net".Length..];
        }

        return null;
    }

    private sealed record DepsJsonInfo(
        IReadOnlyList<string> AssemblyPaths,
        string? TargetFrameworkMoniker,
        string? RuntimeVersion);

    /// <summary>
    /// Gets the NuGet packages folder path.
    /// </summary>
    private static string GetNuGetPackagesPath()
    {
        // Check NUGET_PACKAGES environment variable first
        var envPath = Environment.GetEnvironmentVariable("NUGET_PACKAGES");
        if (!string.IsNullOrEmpty(envPath) && Directory.Exists(envPath))
        {
            return envPath;
        }

        // Default location: ~/.nuget/packages
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(userProfile, ".nuget", "packages");
    }

    /// <summary>
    /// Finds types implementing the specified interface.
    /// </summary>
    /// <param name="interfaceName">Interface name to search for.</param>
    /// <param name="options">Scanning options.</param>
    /// <returns>List of matching types.</returns>
    public List<Type> FindTypesImplementing(string interfaceName, ScanOptions? options = null)
    {
        return FindTypesImplementing([interfaceName], options);
    }

    /// <summary>
    /// Finds types implementing any of the specified interfaces.
    /// </summary>
    /// <param name="interfaceNames">Interface names to search for.</param>
    /// <param name="options">Scanning options.</param>
    /// <returns>List of matching types.</returns>
    public List<Type> FindTypesImplementing(string[] interfaceNames, ScanOptions? options = null)
    {
        ValidateInputs(interfaceNames);
        var results = new List<Type>();

        foreach (var type in GetLoadableTypes())
        {
            if (!ShouldIncludeType(type, options))
                continue;

            if (interfaceNames.Any(interfaceName => ImplementsInterface(type, interfaceName, options)))
            {
                results.Add(type);
            }
        }

        return results;
    }

    /// <summary>
    /// Finds types derived from the specified base class.
    /// </summary>
    /// <param name="baseTypeName">Base class name to search for.</param>
    /// <param name="options">Scanning options.</param>
    /// <returns>List of matching types.</returns>
    public List<Type> FindTypesDerivedFrom(string baseTypeName, ScanOptions? options = null)
    {
        return FindTypesDerivedFrom([baseTypeName], options);
    }

    /// <summary>
    /// Finds types derived from any of the specified base classes.
    /// </summary>
    /// <param name="baseTypeNames">Base class names to search for.</param>
    /// <param name="options">Scanning options.</param>
    /// <returns>List of matching types.</returns>
    public List<Type> FindTypesDerivedFrom(string[] baseTypeNames, ScanOptions? options = null)
    {
        ValidateInputs(baseTypeNames);

        var results = new List<Type>();

        foreach (var type in GetLoadableTypes())
        {
            if (!ShouldIncludeType(type, options))
                continue;

            if (baseTypeNames.Any(baseTypeName => InheritsFrom(type, baseTypeName, options)))
            {
                results.Add(type);
            }
        }

        return results;
    }

    /// <summary>
    /// Finds a specific type by name that implements the specified interface.
    /// </summary>
    /// <param name="targetTypeName">Type name to find.</param>
    /// <param name="interfaceName">Interface name the type must implement.</param>
    /// <param name="options">Scanning options.</param>
    /// <param name="matchTargetFullName">If true, matches target type by full name including namespace; otherwise matches by simple name only.</param>
    /// <returns>The found type, or null if not found.</returns>
    public Type? FindTypeByNameImplementing(string targetTypeName, string interfaceName, ScanOptions? options = null, bool matchTargetFullName = false)
    {
        return FindTypeByNameImplementing(targetTypeName, [interfaceName], options, matchTargetFullName);
    }

    /// <summary>
    /// Finds a specific type by name that implements any of the specified interfaces.
    /// </summary>
    /// <param name="targetTypeName">Type name to find.</param>
    /// <param name="interfaceNames">Interface names the type must implement.</param>
    /// <param name="options">Scanning options.</param>
    /// <param name="matchTargetFullName">If true, matches target type by full name including namespace; otherwise matches by simple name only.</param>
    /// <returns>The found type, or null if not found.</returns>
    public Type? FindTypeByNameImplementing(string targetTypeName, string[] interfaceNames, ScanOptions? options = null, bool matchTargetFullName = false)
    {
        ValidateInputs(interfaceNames);

        if (string.IsNullOrWhiteSpace(targetTypeName))
            throw new ArgumentException("Target type name cannot be empty", nameof(targetTypeName));

        foreach (var type in GetLoadableTypes())
        {
            // Must match the target type name
            if (!MatchesTypeName(type, targetTypeName, matchTargetFullName))
                continue;

            // Must pass inclusion filters
            if (!ShouldIncludeType(type, options))
                continue;

            // Must implement one of the interfaces
            if (interfaceNames.Any(interfaceName => ImplementsInterface(type, interfaceName, options)))
            {
                return type; // Early exit - found it!
            }
        }

        return null;
    }

    /// <summary>
    /// Finds a specific type by name that derives from the specified base class.
    /// </summary>
    /// <param name="targetTypeName">Type name to find.</param>
    /// <param name="baseTypeName">Base class the type must derive from.</param>
    /// <param name="options">Scanning options.</param>
    /// <param name="matchTargetFullName">If true, matches target type by full name including namespace; otherwise matches by simple name only.</param>
    /// <returns>The found type, or null if not found.</returns>
    public Type? FindTypeByNameDerivedFrom(string targetTypeName, string baseTypeName, ScanOptions? options = null, bool matchTargetFullName = false)
    {
        return FindTypeByNameDerivedFrom(targetTypeName, [baseTypeName], options, matchTargetFullName);
    }

    /// <summary>
    /// Finds a specific type by name that derives from any of the specified base classes.
    /// </summary>
    /// <param name="targetTypeName">Type name to find.</param>
    /// <param name="baseTypeNames">Base class names the type must derive from.</param>
    /// <param name="options">Scanning options.</param>
    /// <param name="matchTargetFullName">If true, matches target type by full name including namespace; otherwise matches by simple name only.</param>
    /// <returns>The found type, or null if not found.</returns>
    public Type? FindTypeByNameDerivedFrom(string targetTypeName, string[] baseTypeNames, ScanOptions? options = null, bool matchTargetFullName = false)
    {
        ValidateInputs(baseTypeNames);

        if (string.IsNullOrWhiteSpace(targetTypeName))
            throw new ArgumentException("Target type name cannot be empty", nameof(targetTypeName));

        foreach (var type in GetLoadableTypes())
        {
            // Must match the target type name
            if (!MatchesTypeName(type, targetTypeName, matchTargetFullName))
                continue;

            // Must pass inclusion filters
            if (!ShouldIncludeType(type, options))
                continue;

            // Must derive from one of the base classes
            if (baseTypeNames.Any(baseTypeName => InheritsFrom(type, baseTypeName, options)))
            {
                return type; // Early exit - found it!
            }
        }

        return null;
    }

    /// <summary>
    /// Gets all loadable types from the assembly, handling ReflectionTypeLoadException gracefully.
    /// </summary>
    /// <returns>Array of types that could be loaded.</returns>
    private Type[] GetLoadableTypes()
    {
        try
        {
            return _assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            // Return only the types that could be loaded, filtering out nulls
            return ex.Types.Where(t => t != null).ToArray()!;
        }
    }

    /// <summary>
    /// Determines if a type should be included in scan results based on options.
    /// </summary>
    private static bool ShouldIncludeType(Type type, ScanOptions? options)
    {
        var includeStructs = options?.IncludeStructs == true;

        // Must be a class (or struct if enabled)
        if (!type.IsClass && (!includeStructs || !type.IsValueType))
            return false;

        // Skip abstract unless explicitly included
        if (type.IsAbstract && options?.IncludeAbstract != true)
            return false;

        // Skip non-public unless explicitly included
        var isPublic = type.IsPublic || type.IsNestedPublic;
        if (!isPublic && options?.IncludeNonPublic != true)
            return false;

        // Skip nested types (classes inside classes)
        if (type.IsNested && options?.IncludeNestedTypes != true)
            return false;

        return true;
    }

    /// <summary>
    /// Checks if a type's name matches the target name, respecting the matchFullName flag.
    /// For generic types with matchFullName=true, handles the mangled FullName format.
    /// </summary>
    private static bool MatchesTypeName(Type type, string typeName, bool matchFullName)
    {
        if (matchFullName)
        {
            var fullName = type.FullName ?? type.Name;

            // For generic types, FullName includes type arguments like:
            // "Namespace.ICommand`1[[Namespace.Dto, Assembly, ...]]"
            // We need to compare against "Namespace.ICommand`1"
            if (type.IsGenericType)
            {
                var bracketIndex = fullName.IndexOf('[');
                if (bracketIndex > 0)
                {
                    fullName = fullName.Substring(0, bracketIndex);
                }
            }

            return fullName == typeName;
        }

        return type.Name == typeName;
    }

    /// <summary>
    /// Checks if a type's name matches the target name, respecting the MatchFullName option.
    /// For generic types with MatchFullName, handles the mangled FullName format.
    /// </summary>
    private static bool MatchesTypeName(Type type, string typeName, ScanOptions? options)
        => MatchesTypeName(type, typeName, options?.MatchFullName == true);

    /// <summary>
    /// Checks if a type implements the specified interface (by name).
    /// </summary>
    private static bool ImplementsInterface(Type type, string interfaceName, ScanOptions? options)
    {
        return type.GetInterfaces().Any(i => MatchesTypeName(i, interfaceName, options));
    }

    /// <summary>
    /// Checks if a type inherits from the specified base class (by name).
    /// </summary>
    private static bool InheritsFrom(Type type, string baseTypeName, ScanOptions? options)
    {
        var current = type.BaseType;

        while (current != null)
        {
            if (MatchesTypeName(current, baseTypeName, options))
                return true;

            current = current.BaseType;
        }

        return false;
    }

    /// <summary>
    /// Disposes the MetadataLoadContext, unloading all inspected assemblies.
    /// </summary>
    public void Dispose()
    {
        _context.Dispose();
    }

    /// <summary>
    /// Validates that type names array is not null or empty.
    /// </summary>
    private static void ValidateInputs(string[] typeNames)
    {
        if (typeNames == null || typeNames.Length == 0)
            throw new ArgumentException("At least one type name must be specified", nameof(typeNames));

        if (typeNames.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Type names cannot be empty", nameof(typeNames));
    }
}
