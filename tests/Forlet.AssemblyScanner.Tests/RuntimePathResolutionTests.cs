using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using Forlet.AssemblyScanner.Tests.Helpers;
using Xunit;

namespace Forlet.AssemblyScanner.Tests;

/// <summary>
/// Tests for runtime assembly path resolution exercised through the internal
/// CollectAssemblyPaths method. Covers: FORLET_ASSEMBLY_SCANNER_RUNTIME_DIR override,
/// DOTNET_ROOT probing, and platform-agnostic BCL discovery (regression guard).
/// </summary>
[Collection("TestProjects")]
public class RuntimePathResolutionTests
{
    private readonly TestProjectFixture _fixture;

    // The test assembly itself is a real DLL that exists on disk — all we need for CollectAssemblyPaths
    private static readonly string ExistingDllPath = typeof(RuntimePathResolutionTests).Assembly.Location;

    private const string OverrideEnvVar = "FORLET_ASSEMBLY_SCANNER_RUNTIME_DIR";

    public RuntimePathResolutionTests(TestProjectFixture fixture)
    {
        _fixture = fixture;
    }

    #region FORLET_ASSEMBLY_SCANNER_RUNTIME_DIR override

    [Fact]
    public void CollectAssemblyPaths_WithOverrideEnvVar_IncludesDllsFromOverrideDirectory()
    {
        // Arrange
        var overrideDir = _fixture.GetTestDirectory();
        var fakeDll1 = Path.Combine(overrideDir, "FakeRuntime.dll");
        var fakeDll2 = Path.Combine(overrideDir, "FakeCollections.dll");
        File.WriteAllBytes(fakeDll1, Array.Empty<byte>());
        File.WriteAllBytes(fakeDll2, Array.Empty<byte>());

        var original = Environment.GetEnvironmentVariable(OverrideEnvVar);
        Environment.SetEnvironmentVariable(OverrideEnvVar, overrideDir);
        try
        {
            // Act
            var paths = MetadataScanner.CollectAssemblyPaths(ExistingDllPath).ToList();

            // Assert
            paths.Should().Contain(fakeDll1);
            paths.Should().Contain(fakeDll2);
        }
        finally
        {
            Environment.SetEnvironmentVariable(OverrideEnvVar, original);
        }
    }

    [Fact]
    public void CollectAssemblyPaths_WithOverrideEnvVar_EmptyDirectory_FallsThroughToSystemDiscovery()
    {
        // Arrange — override dir exists but has no DLLs, so Strategy 0 yields nothing and falls through
        var emptyOverrideDir = _fixture.GetTestDirectory();
        var original = Environment.GetEnvironmentVariable(OverrideEnvVar);
        Environment.SetEnvironmentVariable(OverrideEnvVar, emptyOverrideDir);
        try
        {
            // Act
            var paths = MetadataScanner.CollectAssemblyPaths(ExistingDllPath).ToList();

            // Assert — system discovery should still populate results
            paths.Should().NotBeEmpty();
            paths.Should().Contain(
                p => Path.GetFileName(p).StartsWith("System.", StringComparison.Ordinal),
                "system discovery must succeed when the override directory is empty");
        }
        finally
        {
            Environment.SetEnvironmentVariable(OverrideEnvVar, original);
        }
    }

    [Fact]
    public void CollectAssemblyPaths_WithOverrideEnvVar_NonExistentPath_FallsThroughToSystemDiscovery()
    {
        // Arrange
        var original = Environment.GetEnvironmentVariable(OverrideEnvVar);
        Environment.SetEnvironmentVariable(OverrideEnvVar, "/nonexistent/path/that/does/not/exist");
        try
        {
            // Act
            var paths = MetadataScanner.CollectAssemblyPaths(ExistingDllPath).ToList();

            // Assert — graceful fallthrough to OS path discovery
            paths.Should().NotBeEmpty();
            paths.Should().Contain(
                p => Path.GetFileName(p).StartsWith("System.", StringComparison.Ordinal),
                "system discovery must succeed when the override path does not exist");
        }
        finally
        {
            Environment.SetEnvironmentVariable(OverrideEnvVar, original);
        }
    }

    #endregion

    #region DOTNET_ROOT probing

    [Fact]
    public void CollectAssemblyPaths_WithDotnetRootEnvVar_IncludesRuntimeDllsFromThatRoot()
    {
        // Arrange — build a valid dotnet root structure in a temp directory
        var dotnetRoot = _fixture.GetTestDirectory();
        var runtimeVersion = $"{Environment.Version.Major}.0.99";
        var runtimeDir = Path.Combine(dotnetRoot, "shared", "Microsoft.NETCore.App", runtimeVersion);
        Directory.CreateDirectory(runtimeDir);

        var fakeBclDll = Path.Combine(runtimeDir, "FakeBclAssembly.dll");
        File.WriteAllBytes(fakeBclDll, Array.Empty<byte>());

        var originalDotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT");
        var originalOverride = Environment.GetEnvironmentVariable(OverrideEnvVar);
        Environment.SetEnvironmentVariable("DOTNET_ROOT", dotnetRoot);
        Environment.SetEnvironmentVariable(OverrideEnvVar, null);
        try
        {
            // Act
            var paths = MetadataScanner.CollectAssemblyPaths(ExistingDllPath).ToList();

            // Assert — our fake DLL placed under DOTNET_ROOT must appear in the resolved paths
            paths.Should().Contain(fakeBclDll,
                "DOTNET_ROOT is checked before OS-specific paths and must be probed for runtime assemblies");
        }
        finally
        {
            Environment.SetEnvironmentVariable("DOTNET_ROOT", originalDotnetRoot);
            Environment.SetEnvironmentVariable(OverrideEnvVar, originalOverride);
        }
    }

    [Fact]
    public void CollectAssemblyPaths_WithDotnetRootEnvVar_NoMatchingVersion_StillFindsSystemRuntime()
    {
        // Arrange — DOTNET_ROOT points to a dir with a version that won't match the current runtime,
        // so the method must continue to the OS-path and RuntimeEnvironment fallbacks
        var dotnetRoot = _fixture.GetTestDirectory();
        var unmatchedVersionDir = Path.Combine(dotnetRoot, "shared", "Microsoft.NETCore.App", "1.0.0");
        Directory.CreateDirectory(unmatchedVersionDir);
        File.WriteAllBytes(Path.Combine(unmatchedVersionDir, "Unmatched.dll"), Array.Empty<byte>());

        var originalDotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT");
        var originalOverride = Environment.GetEnvironmentVariable(OverrideEnvVar);
        Environment.SetEnvironmentVariable("DOTNET_ROOT", dotnetRoot);
        Environment.SetEnvironmentVariable(OverrideEnvVar, null);
        try
        {
            // Act
            var paths = MetadataScanner.CollectAssemblyPaths(ExistingDllPath).ToList();

            // Assert — real BCL DLLs must still be found via OS path probing / RuntimeEnvironment fallback
            paths.Should().Contain(
                p => Path.GetFileName(p).Equals("System.Runtime.dll", StringComparison.OrdinalIgnoreCase),
                "runtime discovery must fall through to OS paths when DOTNET_ROOT has no matching version");
        }
        finally
        {
            Environment.SetEnvironmentVariable("DOTNET_ROOT", originalDotnetRoot);
            Environment.SetEnvironmentVariable(OverrideEnvVar, originalOverride);
        }
    }

    #endregion

    #region Regression guard — BCL discovery without any overrides

    [Fact]
    public void CollectAssemblyPaths_WithoutEnvVarOverrides_FindsBclAssemblies()
    {
        // Arrange — clear override so natural OS discovery and RuntimeEnvironment fallbacks run
        var original = Environment.GetEnvironmentVariable(OverrideEnvVar);
        Environment.SetEnvironmentVariable(OverrideEnvVar, null);
        try
        {
            // Act
            var paths = MetadataScanner.CollectAssemblyPaths(ExistingDllPath).ToList();

            // Assert — must find BCL assemblies on every supported platform
            paths.Should().NotBeEmpty(
                "runtime assembly discovery must succeed on all supported platforms");
            paths.Should().Contain(
                p => Path.GetFileName(p).Equals("System.Runtime.dll", StringComparison.OrdinalIgnoreCase),
                "System.Runtime.dll must be discoverable on all platforms without any env var override");
        }
        finally
        {
            Environment.SetEnvironmentVariable(OverrideEnvVar, original);
        }
    }

    #endregion
}
