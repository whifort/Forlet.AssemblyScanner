using Forlet.AssemblyScanner.Internal;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Forlet.AssemblyScanner;

/// <summary>
/// Resolves the path to the compiled DLL for a given .csproj file.
/// </summary>
public static class ProjectDllResolver
{
    /// <summary>
    /// Resolves the path to the DLL for the specified .csproj file.
    /// Returns the path based on the specified configuration, or Debug by default.
    /// </summary>
    /// <param name="csprojPath">Absolute path to the .csproj file.</param>
    /// <param name="configuration">Build configuration (Debug or Release). Default is Debug.</param>
    /// <returns>Absolute path to the DLL.</returns>
    /// <exception cref="ScanException">Thrown if target framework cannot be determined.</exception>
    private static string ResolveDllPath(string csprojPath, string configuration = "Debug")
    {
        var projectDir = Path.GetDirectoryName(csprojPath)!;
        var projectName = Path.GetFileNameWithoutExtension(csprojPath);
        var document = LoadProjectDocument(csprojPath);
        var targetFramework = ParseTargetFramework(document);

        if (targetFramework == null)
        {
            throw new ScanException(
                $"Could not determine target framework from {csprojPath}. " +
                "Ensure the project contains <TargetFramework> or <TargetFrameworks> element.");
        }

        var assemblyName = ParseAssemblyName(document) ?? projectName;
        var outputPath = ParseOutputPath(document);
        var baseOutputPath = ParseBaseOutputPath(document);
        var outputDir = ResolveOutputDirectory(projectDir, outputPath, baseOutputPath, configuration, targetFramework);

        return Path.Combine(outputDir, $"{assemblyName}.dll");
    }

    /// <summary>
    /// Parses the target framework from a .csproj file using proper XML parsing.
    /// Handles both single target (TargetFramework) and multi-target (TargetFrameworks) projects.
    /// For multi-target projects, returns the first framework.
    /// </summary>
    /// <param name="document">Parsed .csproj XML document.</param>
    /// <returns>Target framework moniker (e.g., "net8.0"), or null if not found.</returns>
    private static string? ParseTargetFramework(XDocument document)
    {
        // Try TargetFramework first (single target)
        var targetFramework = GetProjectPropertyValue(document, "TargetFramework");
        if (!string.IsNullOrWhiteSpace(targetFramework))
        {
            return targetFramework;
        }

        // Try TargetFrameworks (multi-target) - take first
        var targetFrameworks = GetProjectPropertyValue(document, "TargetFrameworks");
        if (!string.IsNullOrWhiteSpace(targetFrameworks))
        {
            var frameworks = targetFrameworks.Split(';', StringSplitOptions.RemoveEmptyEntries);
            var first = frameworks.FirstOrDefault();
            return first?.Trim();
        }

        return null;
    }
            
    private static string? ParseAssemblyName(XDocument document)
    {
        return GetProjectPropertyValue(document, "AssemblyName");
    }

    private static string? ParseOutputPath(XDocument document)
    {
        return GetProjectPropertyValue(document, "OutputPath");
    }

    private static string? ParseBaseOutputPath(XDocument document)
    {
        return GetProjectPropertyValue(document, "BaseOutputPath");
    }

    private static XDocument LoadProjectDocument(string csprojPath)
    {
        try
        {
            return XDocument.Load(csprojPath);
        }
        catch (Exception ex)
        {
            throw new ScanException($"Failed to parse .csproj file: {csprojPath}", ex);
        }
    }

    private static string? GetProjectPropertyValue(XDocument document, string elementName)
    {
        var element = document
            .Descendants()
            .FirstOrDefault(node => string.Equals(node.Name.LocalName, elementName, StringComparison.Ordinal));
        if (element == null)
        {
            return null;
        }

        var value = element.Value?.Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string ResolveOutputDirectory(
        string projectDir,
        string? outputPath,
        string? baseOutputPath,
        string configuration,
        string targetFramework)
    {
        if (!string.IsNullOrWhiteSpace(outputPath))
        {
            return NormalizePath(projectDir, outputPath);
        }

        var baseOutput = string.IsNullOrWhiteSpace(baseOutputPath) ? "bin" : baseOutputPath;
        var combined = Path.Combine(baseOutput, configuration, targetFramework);
        return NormalizePath(projectDir, combined);
    }

    private static string NormalizePath(string projectDir, string path)
    {
        var trimmed = path.Trim();
        return Path.IsPathRooted(trimmed)
            ? Path.GetFullPath(trimmed)
            : Path.GetFullPath(Path.Combine(projectDir, trimmed));
    }

    /// <summary>
    /// Resolves and prepares the DLL for the specified project, building if necessary.
    /// </summary>
    /// <param name="csprojPath">Absolute or relative path to the .csproj file.</param>
    /// <param name="options">Optional resolution options. Uses defaults if null.</param>
    /// <param name="cancellationToken">Optional cancellation token for the operation.</param>
    /// <returns>Result containing the DLL path and build metadata.</returns>
    /// <exception cref="ScanException">Thrown if the project cannot be resolved or built.</exception>
    /// <remarks>
    /// Minimal usage: await ProjectDllResolver.PrepareAssemblyAsync(csprojPath)
    /// </remarks>
    public static async Task<DllResolverResult> PrepareAssemblyAsync(
        string csprojPath,
        ProjectDllResolverOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(csprojPath))
            throw new ArgumentException("csproj Path cannot be empty", nameof(csprojPath));

        options ??= new ProjectDllResolverOptions();

        // Convert to absolute path if relative
        var absoluteCsprojPath = Path.IsPathRooted(csprojPath)
            ? csprojPath
            : Path.GetFullPath(csprojPath);

        // Validate project file exists
        if (!File.Exists(absoluteCsprojPath))
        {
            throw new ScanException($"Project file not found: {absoluteCsprojPath}");
        }

        var dllPath = ResolveDllPath(absoluteCsprojPath, options.Configuration);

        var isStale = StaleChecker.IsStale(absoluteCsprojPath, dllPath, options.PathsToCheck, options.CheckForEdit);

        if (isStale && options.BuildStrategy == BuildOptions.NoBuild)
        {
            throw new ScanException(
                "Project has not been built and AutoBuild is disabled. " +
                "Build the project first or enable AutoBuild in ScanOptions.");
        }

        var builtAutomatically = false;
        string? buildOutput = null;

        if ((isStale && options.BuildStrategy == BuildOptions.AutoBuild) || options.BuildStrategy == BuildOptions.AlwaysBuild)
        {
            // Call OnBuildStart callback if provided
            options.OnBuildStart?.Invoke();

            var buildResult = await ProjectBuilder.BuildAsync(absoluteCsprojPath, options.Configuration, cancellationToken);

            if (!buildResult.Success)
            {
                throw new ScanException(
                    $"Failed to build project. Errors: {buildResult.Errors}");
            }

            // Verify DLL was created
            if (!File.Exists(dllPath))
            {
                throw new ScanException(
                    $"Build succeeded but DLL not found at expected path: {dllPath}. " +
                    "Project may have custom OutputPath or AssemblyName.");
            }

            builtAutomatically = true;
            buildOutput = buildResult.Output;
        }

        return new()
        {
            DllPath = dllPath,
            BuiltAutomatically = builtAutomatically,
            BuildOutput = buildOutput
        };
    }
}
