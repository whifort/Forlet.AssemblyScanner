namespace Forlet.AssemblyScanner;

/// <summary>
/// Specifies the build strategy for DLL resolution.
/// </summary>
public enum BuildOptions
{
    /// <summary>
    /// Never build even if stale
    /// </summary>
    NoBuild,

    /// <summary>
    /// Check if stale and rebuild
    /// </summary>
    AutoBuild,

    /// <summary>
    /// Build each time resolving
    /// </summary>
    AlwaysBuild
}
