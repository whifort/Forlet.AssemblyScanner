namespace Forlet.AssemblyScanner;

/// <summary>
/// Configuration options for assembly scanning operations.
/// </summary>
public sealed class ScanOptions
{
    /// <summary>
    /// Match full type names including namespace. Default is false.
    /// </summary>
    public bool MatchFullName { get; init; } = false;

    /// <summary>
    /// Include abstract classes in results. Default is false.
    /// </summary>
    public bool IncludeAbstract { get; init; } = false;

    /// <summary>
    /// Include non-public types in results. Default is false.
    /// </summary>
    public bool IncludeNonPublic { get; init; } = false;

    /// <summary>
    /// Include structs (value types) in results. Default is false.
    /// </summary>
    public bool IncludeStructs { get; init; } = false;

    /// <summary>
    /// Include nested types in results. Default is false.
    /// </summary>
    public bool IncludeNestedTypes { get; init; } = false;
}
