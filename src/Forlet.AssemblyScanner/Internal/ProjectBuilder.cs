using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Forlet.AssemblyScanner.Internal;

/// <summary>
/// Handles building .NET projects using the dotnet CLI.
/// </summary>
internal static class ProjectBuilder
{
    /// <summary>
    /// Builds the specified project using 'dotnet build'.
    /// </summary>
    /// <param name="csprojPath">Absolute path to the .csproj file to build.</param>
    /// <param name="configuration">Build configuration (Debug or Release). Default is Debug.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A BuildResult indicating success/failure and output.</returns>
    public static async Task<BuildResult> BuildAsync(
        string csprojPath,
        string configuration = "Debug",
        CancellationToken cancellationToken = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"build \"{csprojPath}\" --configuration {configuration} --verbosity quiet --nologo",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        try
        {
            using var process = new Process { StartInfo = psi };

            var output = new StringBuilder();
            var errors = new StringBuilder();

            // Capture output and error streams asynchronously
            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null)
                    output.AppendLine(e.Data);
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null)
                    errors.AppendLine(e.Data);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync(cancellationToken);

            return new BuildResult
            {
                Success = process.ExitCode == 0,
                Output = output.ToString(),
                Errors = errors.ToString()
            };
        }
        catch (Exception ex)
        {
            throw new ScanException($"Failed to execute build command for {csprojPath}", ex);
        }
    }
}
