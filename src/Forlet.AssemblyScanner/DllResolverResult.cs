namespace Forlet.AssemblyScanner;

/// <summary>
/// Result of a project DLL resolution operation.
/// </summary>
public sealed class DllResolverResult
{
    /// <summary>
    /// Valid DLL path
    /// </summary>
    public required string DllPath { get; init; }

    /// <summary>
    /// Whether the project was automatically built during this operation.
    /// </summary>
    public required bool BuiltAutomatically { get; init; }

    /// <summary>
    /// Build output if the project was built automatically, otherwise null.
    /// </summary>
    public string? BuildOutput { get; init; }
}
