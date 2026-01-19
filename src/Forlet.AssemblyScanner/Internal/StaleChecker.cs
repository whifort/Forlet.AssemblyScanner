using System;
using System.IO;
using System.Linq;

namespace Forlet.AssemblyScanner.Internal;

/// <summary>
/// Checks if a project's compiled DLL is stale and needs rebuilding.
/// </summary>
internal static class StaleChecker
{
    /// <summary>
    /// Determines if the DLL is stale (missing or older than source files).
    /// Detects file deletions via directory timestamp changes at any nesting level.
    /// </summary>
    /// <param name="csprojPath">Path to the .csproj file.</param>
    /// <param name="dllPath">Path to the compiled DLL.</param>
    /// <param name="pathsToCheck">
    /// Optional array of relative paths to check for staleness.
    /// If null or empty, checks the entire project directory.
    /// </param>
    /// <param name="checkForEdit">
    /// If true, checks file timestamps when directory timestamps haven't changed.
    /// If false, skips file enumeration when directory timestamps haven't changed.
    /// Default: false
    /// </param>
    /// <returns>True if the DLL is missing or stale, false otherwise.</returns>
    /// <remarks>
    /// Detects: <br/>
    /// - Missing DLL <br/>
    /// - Modified .csproj file <br/>
    /// - Modified .cs files (via file timestamps) <br/>
    /// - File deletions at any nesting level (via directory timestamps) <br/> <br/>
    /// 
    /// Uses recursive directory enumeration to catch deletions in nested directories.
    /// When CheckForEdit is false, relies on directory timestamps for faster performance.
    /// </remarks>
    public static bool IsStale(string csprojPath, string dllPath, string[]? pathsToCheck, bool checkForEdit = false)
    {
        // DLL doesn't exist - definitely stale
        if (!File.Exists(dllPath))
            return true;

        var dllTime = File.GetLastWriteTimeUtc(dllPath);
        var projectDir = Path.GetDirectoryName(csprojPath)!;

        // Always check if .csproj file is newer than DLL
        if (File.GetLastWriteTimeUtc(csprojPath) > dllTime)
            return true;

        // Check source files based on pathsToCheck
        if (pathsToCheck == null || pathsToCheck.Length == 0)
        {
            // Check entire project directory
            return HasChangesInProject(projectDir, dllTime, checkForEdit);
        }
        else
        {
            // Check only specified paths
            return HasChangesInPaths(projectDir, pathsToCheck, dllTime, checkForEdit);
        }
    }

    /// <summary>
    /// Checks if any .cs files in the entire project directory are newer than the DLL.
    /// Also recursively checks all subdirectory timestamps to detect file deletions.
    /// Excludes obj/ and bin/ directories.
    /// </summary>
    private static bool HasChangesInProject(string projectDir, DateTime dllTime, bool checkForEdit)
    {
        try
        {
            // First check directory timestamps (catches file deletions)
            var dirChanged = HasModifiedDirectories(projectDir, dllTime);
            if (dirChanged)
            {
                return true;
            }

            // If directories haven't changed, check files only if CheckForEdit is true
            if (!checkForEdit)
            {
                return false;
            }

            // Check individual file timestamps as backup
            return Directory.EnumerateFiles(projectDir, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                         && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                .Any(f => File.GetLastWriteTimeUtc(f) > dllTime);
        }
        catch (Exception ex)
        {
            throw new ScanException($"Failed to check for stale files in {projectDir}", ex);
        }
    }

    /// <summary>
    /// Checks if any .cs files in the specified paths are newer than the DLL.
    /// Also recursively checks all subdirectory timestamps to detect file deletions within those paths.
    /// </summary>
    private static bool HasChangesInPaths(string projectDir, string[] paths, DateTime dllTime, bool checkForEdit)
    {
        try
        {
            foreach (var relativePath in paths)
            {
                var fullPath = Path.Combine(projectDir, relativePath);

                // If the path points to a specific file, compare its timestamp directly.
                if (File.Exists(fullPath))
                {
                    if (File.GetLastWriteTimeUtc(fullPath) > dllTime)
                    {
                        return true;
                    }

                    continue;
                }

                // Skip if directory doesn't exist
                if (!Directory.Exists(fullPath))
                    continue;

                // Check directory timestamps (catches file deletions)
                var dirChanged = HasModifiedDirectories(fullPath, dllTime);
                if (dirChanged)
                {
                    return true;
                }

                // If directory hasn't changed, check files only if CheckForEdit is true
                if (!checkForEdit)
                {
                    continue;
                }

                // Check individual file timestamps as backup
                var hasNewer = Directory.EnumerateFiles(fullPath, "*.cs", SearchOption.AllDirectories)
                    .Any(f => File.GetLastWriteTimeUtc(f) > dllTime);

                if (hasNewer)
                    return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            throw new ScanException($"Failed to check for stale files in specified paths", ex);
        }
    }

    /// <summary>
    /// Recursively checks all subdirectories under the given path for modification times.
    /// Returns true if any directory timestamp is newer than the DLL time.
    /// Skips obj/ and bin/ directories.
    /// </summary>
    private static bool HasModifiedDirectories(string path, DateTime dllTime)
    {
        try
        {
            var dirInfo = new DirectoryInfo(path);

            // Check if this directory was modified
            if (Directory.GetLastWriteTimeUtc(path) > dllTime)
            {
                return true;
            }

            // Recursively check all subdirectories
            foreach (var subDir in dirInfo.EnumerateDirectories())
            {
                // Skip build artifacts
                if (subDir.Name == "obj" || subDir.Name == "bin")
                    continue;

                if (HasModifiedDirectories(subDir.FullName, dllTime))
                {
                    return true;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            throw new ScanException($"Failed to check directory timestamps in {path}", ex);
        }
    }
}
