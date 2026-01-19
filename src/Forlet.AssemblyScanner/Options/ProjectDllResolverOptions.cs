using System;

namespace Forlet.AssemblyScanner;

/// <summary>
/// Options for resolving project DLL paths and configuring staleness checks.
/// </summary>
public class ProjectDllResolverOptions
{
    /// <summary>
    /// Build strategy to use. Default is AutoBuild.
    /// </summary>
    public BuildOptions BuildStrategy { get; init; } = BuildOptions.AutoBuild;

    /// <summary>
    /// Build configuration (Debug or Release). Default is Debug.
    /// </summary>
    public string Configuration { get; init; } = "Debug";

    /// <summary>
    /// Paths to check for staleness. If null, checks entire project.
    /// When specified, only these paths are monitored for changes.
    /// Example: new[] { "Entities", "Commands", "Handlers" }
    /// </summary>
    public string[]? PathsToCheck { get; init; }

    /// <summary>
    /// If <see langword="true"/>, checks file timestamps when directory timestamps haven't changed. <br/>
    /// If <see langword="false"/>, skips file enumeration when directory timestamps haven't changed. <br/>
    /// Default: <see langword="false"/> (performance-optimized, but may miss edits on filesystems
    /// that do not update directory timestamps for in-place edits).
    /// </summary>
    public bool CheckForEdit { get; init; } = false;

    /// <summary>
    /// Optional callback that fires when a build is about to start. <br/>
    /// Useful for logging, UI updates, or other side effects. <br/>
    /// Default: null (no callback)
    /// </summary>
    public Action? OnBuildStart { get; init; }
}
