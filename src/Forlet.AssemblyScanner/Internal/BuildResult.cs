namespace Forlet.AssemblyScanner.Internal;

/// <summary>
/// Result of a project build operation.
/// </summary>
internal sealed class BuildResult
{
    /// <summary>
    /// Gets a value indicating whether the build succeeded.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Gets the standard output from the build process.
    /// </summary>
    public required string Output { get; init; }

    /// <summary>
    /// Gets the standard error from the build process.
    /// </summary>
    public required string Errors { get; init; }
}
