using FluentAssertions;
using Forlet.AssemblyScanner.Tests.Helpers;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Forlet.AssemblyScanner.Tests;

[Collection("TestProjects")]
public class ProjectDllResolverTests
{
    private readonly TestProjectFixture _fixture;

    public ProjectDllResolverTests(TestProjectFixture fixture)
    {
        _fixture = fixture;
    }

    #region Input Validation Tests

    [Fact]
    public async Task PrepareAssemblyAsync_WithNullPath_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            ProjectDllResolver.PrepareAssemblyAsync(null!)
        );
    }

    [Fact]
    public async Task PrepareAssemblyAsync_WithEmptyPath_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            ProjectDllResolver.PrepareAssemblyAsync("")
        );
    }

    [Fact]
    public async Task PrepareAssemblyAsync_WithWhitespacePath_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            ProjectDllResolver.PrepareAssemblyAsync("   ")
        );
    }

    [Fact]
    public async Task PrepareAssemblyAsync_WithNonExistentFile_ThrowsScanException()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_fixture.GetTestDirectory(), "NonExistent.csproj");

        // Act & Assert
        await Assert.ThrowsAsync<ScanException>(() =>
            ProjectDllResolver.PrepareAssemblyAsync(nonExistentPath)
        );
    }

    [Fact]
    public async Task PrepareAssemblyAsync_WithInvalidCsproj_ThrowsScanException()
    {
        // Arrange
        var invalidCsproјPath = Path.Combine(_fixture.GetTestDirectory(), "Invalid.csproj");
        File.WriteAllText(invalidCsproјPath, "not valid xml");

        // Act & Assert
        await Assert.ThrowsAsync<ScanException>(() =>
            ProjectDllResolver.PrepareAssemblyAsync(invalidCsproјPath)
        );
    }

    #endregion

    #region Path Resolution Tests

    [Fact]
    public async Task PrepareAssemblyAsync_WithRelativePath_ResolvesToAbsolute()
    {
        // Arrange
        var builder = new TestProjectBuilder("TestProject");
        builder.AddInterface("ICommand");
        var csprojPath = builder.Build(_fixture.GetTestDirectory());

        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(Path.GetDirectoryName(csprojPath)!);
            var relativePath = Path.GetFileName(csprojPath);

            // Act
            var result = await ProjectDllResolver.PrepareAssemblyAsync(relativePath);

            // Assert
            Path.IsPathRooted(result.DllPath).Should().BeTrue();
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    [Fact]
    public async Task PrepareAssemblyAsync_WithAssemblyName_UsesCustomAssemblyName()
    {
        // Arrange
        var testDir = _fixture.GetTestDirectory();
        var projectDir = Path.Combine(testDir, "CustomAssemblyName");
        Directory.CreateDirectory(projectDir);

        var csprojContent = @"<Project Sdk=""Microsoft.NET.Sdk"">
    <PropertyGroup>
        <TargetFramework>net8.0</TargetFramework>
        <AssemblyName>Custom.Assembly</AssemblyName>
    </PropertyGroup>
</Project>";
        var csprojPath = Path.Combine(projectDir, "CustomAssemblyName.csproj");
        File.WriteAllText(csprojPath, csprojContent);
        File.WriteAllText(Path.Combine(projectDir, "Class.cs"), "public class TestClass { }");

        // Act
        var result = await ProjectDllResolver.PrepareAssemblyAsync(csprojPath);

        // Assert
        var expectedPath = Path.Combine(projectDir, "bin", "Debug", "net8.0", "Custom.Assembly.dll");
        result.DllPath.Should().Be(expectedPath);
        File.Exists(result.DllPath).Should().BeTrue();
    }

    [Fact]
    public async Task PrepareAssemblyAsync_WithBaseOutputPath_UsesCustomOutputDirectory()
    {
        // Arrange
        var testDir = _fixture.GetTestDirectory();
        var projectDir = Path.Combine(testDir, "CustomOutputPath");
        Directory.CreateDirectory(projectDir);

        var csprojContent = @"<Project Sdk=""Microsoft.NET.Sdk"">
    <PropertyGroup>
        <TargetFramework>net8.0</TargetFramework>
        <BaseOutputPath>artifacts</BaseOutputPath>
    </PropertyGroup>
</Project>";
        var csprojPath = Path.Combine(projectDir, "CustomOutputPath.csproj");
        File.WriteAllText(csprojPath, csprojContent);
        File.WriteAllText(Path.Combine(projectDir, "Class.cs"), "public class TestClass { }");

        // Act
        var result = await ProjectDllResolver.PrepareAssemblyAsync(csprojPath);

        // Assert
        var expectedPath = Path.Combine(projectDir, "artifacts", "Debug", "net8.0", "CustomOutputPath.dll");
        result.DllPath.Should().Be(expectedPath);
        File.Exists(result.DllPath).Should().BeTrue();
    }

    #endregion

    #region Target Framework Tests

    [Fact]
    public async Task PrepareAssemblyAsync_WithNoTargetFramework_ThrowsScanException()
    {
        // Arrange
        var csprojPath = Path.Combine(_fixture.GetTestDirectory(), "NoFramework.csproj");
        File.WriteAllText(csprojPath, @"<Project Sdk=""Microsoft.NET.Sdk""></Project>");

        // Act & Assert
        await Assert.ThrowsAsync<ScanException>(() =>
            ProjectDllResolver.PrepareAssemblyAsync(csprojPath)
        );
    }

    #endregion

    #region Build Strategy Tests

    [Fact]
    public async Task PrepareAssemblyAsync_WithNoBuild_AndExistingDll_ReturnsDll()
    {
        // Arrange
        var builder = new TestProjectBuilder("TestProject");
        builder.AddInterface("ICommand");
        var csprojPath = builder.Build(_fixture.GetTestDirectory());

        // First build to create DLL
        var result1 = await ProjectDllResolver.PrepareAssemblyAsync(csprojPath);

        // Act - NoBuild should work since DLL exists
        var result2 = await ProjectDllResolver.PrepareAssemblyAsync(
            csprojPath,
            new() { BuildStrategy = BuildOptions.NoBuild }
        );

        // Assert
        result2.DllPath.Should().Be(result1.DllPath);
        File.Exists(result2.DllPath).Should().BeTrue();
    }

    #endregion

    #region OnBuildStart Callback Tests

    [Fact]
    public async Task PrepareAssemblyAsync_WithOnBuildStart_CallsCallbackWhenBuildStarts()
    {
        // Arrange
        var builder = new TestProjectBuilder("TestProject");
        builder.AddInterface("ICommand");
        var csprojPath = builder.Build(_fixture.GetTestDirectory());

        var callbackInvoked = false;

        // Act
        var result = await ProjectDllResolver.PrepareAssemblyAsync(
            csprojPath,
            new()
            {
                BuildStrategy = BuildOptions.AlwaysBuild,
                OnBuildStart = () => { callbackInvoked = true; }
            }
        );

        // Assert
        callbackInvoked.Should().BeTrue();
        result.BuiltAutomatically.Should().BeTrue();
    }

    [Fact]
    public async Task PrepareAssemblyAsync_WithNoBuild_DoesNotCallOnBuildStart()
    {
        // Arrange
        var builder = new TestProjectBuilder("TestProject");
        builder.AddInterface("ICommand");
        var csprojPath = builder.Build(_fixture.GetTestDirectory());

        // First build to create DLL
        await ProjectDllResolver.PrepareAssemblyAsync(
            csprojPath,
            new() { BuildStrategy = BuildOptions.AutoBuild }
        );

        var callbackInvoked = false;

        // Act - NoBuild should not trigger callback
        var result = await ProjectDllResolver.PrepareAssemblyAsync(
            csprojPath,
            new()
            {
                BuildStrategy = BuildOptions.NoBuild,
                OnBuildStart = () => { callbackInvoked = true; }
            }
        );

        // Assert
        callbackInvoked.Should().BeFalse();
    }

    #endregion

    #region CheckForEdit Tests

    [Fact]
    public async Task PrepareAssemblyAsync_WithCheckForEditFalse_SkipsFileEnumeration()
    {
        // Arrange
        var builder = new TestProjectBuilder("TestProject");
        builder.AddInterface("ICommand");
        builder.AddClass("CreateCommand", "ICommand");
        var csprojPath = builder.Build(_fixture.GetTestDirectory());

        // Initial build
        var result1 = await ProjectDllResolver.PrepareAssemblyAsync(
            csprojPath,
            new() { BuildStrategy = BuildOptions.AutoBuild }
        );
        result1.BuiltAutomatically.Should().BeTrue();

        // Act - Check with CheckForEdit = false (should not rebuild)
        var result2 = await ProjectDllResolver.PrepareAssemblyAsync(
            csprojPath,
            new()
            {
                BuildStrategy = BuildOptions.AutoBuild,
                CheckForEdit = false
            }
        );

        // Assert - Should not rebuild since CheckForEdit is false and dirs haven't changed
        result2.BuiltAutomatically.Should().BeFalse();
    }

    [Fact]
    public async Task PrepareAssemblyAsync_WithCheckForEditTrue_ChecksFileTimestamps()
    {
        // Arrange
        var builder = new TestProjectBuilder("TestProject");
        builder.AddInterface("ICommand");
        var csprojPath = builder.Build(_fixture.GetTestDirectory());

        // Initial build
        var result1 = await ProjectDllResolver.PrepareAssemblyAsync(
            csprojPath,
            new() { BuildStrategy = BuildOptions.AutoBuild }
        );
        result1.BuiltAutomatically.Should().BeTrue();

        // Wait a bit then modify a file
        await Task.Delay(100);
        var csFile = Path.Combine(Path.GetDirectoryName(csprojPath)!, "ICommand.cs");
        File.SetLastWriteTimeUtc(csFile, DateTime.UtcNow);

        // Act - Check with CheckForEdit = true (should rebuild)
        var result2 = await ProjectDllResolver.PrepareAssemblyAsync(
            csprojPath,
            new()
            {
                BuildStrategy = BuildOptions.AutoBuild,
                CheckForEdit = true
            }
        );

        // Assert - Should rebuild due to file modification
        result2.BuiltAutomatically.Should().BeTrue();
    }

    [Fact]
    public async Task PrepareAssemblyAsync_WithCheckForEditFalse_AndPathsToCheck_SkipsFileEnumeration()
    {
        // Arrange
        var testDir = _fixture.GetTestDirectory();
        var builder = new TestProjectBuilder("TestProject");
        builder.AddInterface("ICommand");
        var csprojPath = builder.Build(testDir);

        // Initial build
        var result1 = await ProjectDllResolver.PrepareAssemblyAsync(csprojPath);
        result1.BuiltAutomatically.Should().BeTrue();

        // Create Entities directory and file BEFORE initial build
        var entitiesDir = Path.Combine(testDir, "TestProject", "Entities");
        Directory.CreateDirectory(entitiesDir);
        var entityFile = Path.Combine(entitiesDir, "Entity.cs");
        File.WriteAllText(entityFile, "public class Entity { }");

        // Wait for directory timestamp to settle
        Thread.Sleep(100);

        // Now build again so directory timestamp is before DLL
        var result1b = await ProjectDllResolver.PrepareAssemblyAsync(csprojPath);

        // Modify ONLY the file content (not creating new files, so directory timestamp unchanged)
        Thread.Sleep(10);
        File.WriteAllText(entityFile, "public class Entity { /* modified */ }");

        // Act - Check with CheckForEdit = false (skips file content checks)
        var result2 = await ProjectDllResolver.PrepareAssemblyAsync(
            csprojPath,
            new()
            {
                CheckForEdit = false,
                PathsToCheck = new[] { "Entities" }
            }
        );

        // Assert - Should not rebuild since CheckForEdit=false skips file timestamp check
        // and directory timestamp is unchanged (we only modified file content)
        result2.BuiltAutomatically.Should().BeFalse();
    }

    [Fact]
    public async Task PrepareAssemblyAsync_WithCheckForEditTrue_AndPathsToCheck_DetectsFileChanges()
    {
        // Arrange
        var testDir = _fixture.GetTestDirectory();
        var builder = new TestProjectBuilder("TestProject");
        builder.AddInterface("ICommand");
        var csprojPath = builder.Build(testDir);

        // Initial build
        var result1 = await ProjectDllResolver.PrepareAssemblyAsync(csprojPath);
        result1.BuiltAutomatically.Should().BeTrue();

        // Create entity file before DLL
        var entitiesDir = Path.Combine(testDir, "TestProject", "Entities");
        Directory.CreateDirectory(entitiesDir);
        var entityFile = Path.Combine(entitiesDir, "Entity.cs");
        File.WriteAllText(entityFile, "public class Entity { }");
        
        // Wait then modify the file to be newer
        System.Threading.Thread.Sleep(10);
        File.WriteAllText(entityFile, "public class Entity { /* modified */ }");

        // Act - Check with CheckForEdit = true
        var result2 = await ProjectDllResolver.PrepareAssemblyAsync(
            csprojPath,
            new()
            {
                CheckForEdit = true,
                PathsToCheck = new[] { "Entities" }
            }
        );

        // Assert - Should rebuild due to file modification with CheckForEdit = true
        result2.BuiltAutomatically.Should().BeTrue();
    }

    [Fact]
    public async Task PrepareAssemblyAsync_WithCheckForEditFalse_AndFreshDll_ReturnsFalse()
    {
        // Arrange - Build once
        var builder = new TestProjectBuilder("TestProject");
        builder.AddInterface("ICommand");
        var csprojPath = builder.Build(_fixture.GetTestDirectory());

        var result1 = await ProjectDllResolver.PrepareAssemblyAsync(csprojPath);
        result1.BuiltAutomatically.Should().BeTrue();

        // Act - Check immediately with CheckForEdit = false
        var result2 = await ProjectDllResolver.PrepareAssemblyAsync(
            csprojPath,
            new()
            {
                CheckForEdit = false,
                BuildStrategy = BuildOptions.AutoBuild
            }
        );

        // Assert - Should be false (DLL is fresh, no rebuild)
        result2.BuiltAutomatically.Should().BeFalse();
    }

    [Fact]
    public async Task PrepareAssemblyAsync_WithCheckForEditTrue_DetectsDeepNestedFileModifications()
    {
        // Arrange
        var testDir = _fixture.GetTestDirectory();
        var builder = new TestProjectBuilder("TestProject");
        builder.AddInterface("ICommand");
        var csprojPath = builder.Build(testDir);

        // Initial build
        var result1 = await ProjectDllResolver.PrepareAssemblyAsync(csprojPath);
        result1.BuiltAutomatically.Should().BeTrue();

        // Create deep nested structure
        var deepDir = Path.Combine(testDir, "TestProject", "Entities", "Models", "Nested");
        Directory.CreateDirectory(deepDir);
        var nestedFile = Path.Combine(deepDir, "NestedEntity.cs");
        File.WriteAllText(nestedFile, "public class NestedEntity { }");

        // Wait then modify deeply nested file
        System.Threading.Thread.Sleep(10);
        File.WriteAllText(nestedFile, "public class NestedEntity { /* modified */ }");

        // Act
        var result2 = await ProjectDllResolver.PrepareAssemblyAsync(
            csprojPath,
            new()
            {
                CheckForEdit = true
            }
        );

        // Assert - Should detect deeply nested file changes
        result2.BuiltAutomatically.Should().BeTrue();
    }

    #endregion

    #region OnBuildStart Additional Tests

    [Fact]
    public async Task PrepareAssemblyAsync_OnBuildStart_WithException_PropagatesToCaller()
    {
        // Arrange
        var builder = new TestProjectBuilder("TestProject");
        builder.AddInterface("ICommand");
        var csprojPath = builder.Build(_fixture.GetTestDirectory());

        // Act & Assert - Exception in callback should propagate
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ProjectDllResolver.PrepareAssemblyAsync(
                csprojPath,
                new()
                {
                    OnBuildStart = () => throw new InvalidOperationException("Callback error")
                }
            )
        );
    }

    [Fact]
    public async Task PrepareAssemblyAsync_OnBuildStart_CalledOncePerBuild()
    {
        // Arrange
        var testDir = _fixture.GetTestDirectory();
        var builder = new TestProjectBuilder("TestProject");
        builder.AddInterface("ICommand");
        var csprojPath = builder.Build(testDir);

        var callCount = 0;

        // First build with AlwaysBuild to force rebuild
        await ProjectDllResolver.PrepareAssemblyAsync(
            csprojPath,
            new()
            {
                BuildStrategy = BuildOptions.AlwaysBuild,
                OnBuildStart = () => { callCount++; }
            }
        );

        callCount.Should().Be(1);

        // Modify a file
        var csFile = Path.Combine(testDir, "TestProject", "ICommand.cs");
        System.Threading.Thread.Sleep(10);
        File.WriteAllText(csFile, "public interface ICommand { /* modified */ }");

        // Second build with AlwaysBuild to force rebuild
        await ProjectDllResolver.PrepareAssemblyAsync(
            csprojPath,
            new()
            {
                BuildStrategy = BuildOptions.AlwaysBuild,
                OnBuildStart = () => { callCount++; }
            }
        );

        // Assert - Called once per build (2 times total)
        callCount.Should().Be(2);
    }

    [Fact]
    public async Task PrepareAssemblyAsync_OnBuildStart_WithNull_DoesNotThrow()
    {
        // Arrange
        var builder = new TestProjectBuilder("TestProject");
        builder.AddInterface("ICommand");
        var csprojPath = builder.Build(_fixture.GetTestDirectory());

        // Act - Should not throw even with null callback
        var result = await ProjectDllResolver.PrepareAssemblyAsync(
            csprojPath,
            new()
            {
                OnBuildStart = null
            }
        );

        // Assert
        result.BuiltAutomatically.Should().BeTrue();
    }

    #endregion

    #region Build Result Tests

    [Fact]
    public async Task PrepareAssemblyAsync_BuiltAutomatically_IsTrueOnFirstBuild()
    {
        // Arrange
        var builder = new TestProjectBuilder("TestProject");
        builder.AddInterface("ICommand");
        var csprojPath = builder.Build(_fixture.GetTestDirectory());

        // Act
        var result = await ProjectDllResolver.PrepareAssemblyAsync(csprojPath);

        // Assert
        result.BuiltAutomatically.Should().BeTrue("first build should build automatically");
        result.DllPath.Should().NotBeNullOrEmpty();
        File.Exists(result.DllPath).Should().BeTrue();
    }

    [Fact]
    public async Task PrepareAssemblyAsync_BuiltAutomatically_IsFalseWhenDllIsFresh()
    {
        // Arrange
        var builder = new TestProjectBuilder("TestProject");
        builder.AddInterface("ICommand");
        var csprojPath = builder.Build(_fixture.GetTestDirectory());

        // First build
        var result1 = await ProjectDllResolver.PrepareAssemblyAsync(csprojPath);
        result1.BuiltAutomatically.Should().BeTrue();

        // Act - Second call immediately
        var result2 = await ProjectDllResolver.PrepareAssemblyAsync(csprojPath);

        // Assert
        result2.BuiltAutomatically.Should().BeFalse("DLL is fresh, should not rebuild");
        result2.DllPath.Should().Be(result1.DllPath);
    }

    [Fact]
    public async Task PrepareAssemblyAsync_WithConfiguration_UsesDebugByDefault()
    {
        // Arrange
        var builder = new TestProjectBuilder("TestProject");
        builder.AddInterface("ICommand");
        var csprojPath = builder.Build(_fixture.GetTestDirectory());

        // Act
        var result = await ProjectDllResolver.PrepareAssemblyAsync(csprojPath);

        // Assert - Default configuration is Debug
        result.DllPath.Should().Contain("Debug", "default configuration should be Debug");
    }

    [Fact]
    public async Task PrepareAssemblyAsync_WithConfigurationRelease_UsesReleasePath()
    {
        // Arrange
        var builder = new TestProjectBuilder("TestProject");
        builder.AddInterface("ICommand");
        var csprojPath = builder.Build(_fixture.GetTestDirectory());

        // Act
        var result = await ProjectDllResolver.PrepareAssemblyAsync(
            csprojPath,
            new()
            {
                Configuration = "Release"
            }
        );

        // Assert - Release path should be used
        result.DllPath.Should().Contain("Release", "specified configuration should be Release");
    }

    #endregion

    #region BuildOutput Tests

    [Fact]
    public async Task PrepareAssemblyAsync_WhenBuilt_BuildOutputIsNotEmpty()
    {
        // Arrange
        var builder = new TestProjectBuilder("TestProject");
        builder.AddInterface("ICommand");
        var csprojPath = builder.Build(_fixture.GetTestDirectory());

        // Act
        var result = await ProjectDllResolver.PrepareAssemblyAsync(
            csprojPath,
            new() { BuildStrategy = BuildOptions.AlwaysBuild }
        );

        // Assert - BuildOutput should have content when built
        result.BuiltAutomatically.Should().BeTrue();
        result.BuildOutput.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task PrepareAssemblyAsync_WhenNotBuilt_BuildOutputIsNull()
    {
        // Arrange
        var builder = new TestProjectBuilder("TestProject");
        builder.AddInterface("ICommand");
        var csprojPath = builder.Build(_fixture.GetTestDirectory());

        // First build
        await ProjectDllResolver.PrepareAssemblyAsync(csprojPath);

        // Act - Second call shouldn't build
        var result = await ProjectDllResolver.PrepareAssemblyAsync(csprojPath);

        // Assert - BuildOutput should be null when not built
        result.BuiltAutomatically.Should().BeFalse();
        result.BuildOutput.Should().BeNull();
    }

    #endregion

    #region Target Framework Tests

    [Fact]
    public async Task PrepareAssemblyAsync_WithMultiTargetFramework_UsesFirstFramework()
    {
        // Arrange - Create project with multiple target frameworks
        var testDir = _fixture.GetTestDirectory();
        var projectDir = Path.Combine(testDir, "MultiTarget");
        Directory.CreateDirectory(projectDir);

        var csprojContent = @"<Project Sdk=""Microsoft.NET.Sdk"">
    <PropertyGroup>
        <TargetFrameworks>net8.0;net7.0</TargetFrameworks>
    </PropertyGroup>
</Project>";
        var csprojPath = Path.Combine(projectDir, "MultiTarget.csproj");
        File.WriteAllText(csprojPath, csprojContent);

        // Add a simple class
        File.WriteAllText(
            Path.Combine(projectDir, "Class.cs"),
            "public class TestClass { }"
        );

        // Act
        var result = await ProjectDllResolver.PrepareAssemblyAsync(csprojPath);

        // Assert - Should use first framework (net8.0)
        result.DllPath.Should().Contain("net8.0");
        File.Exists(result.DllPath).Should().BeTrue();
    }

    [Fact]
    public async Task PrepareAssemblyAsync_WithNamespacedTargetFramework_ParsesFramework()
    {
        // Arrange - Create project with default MSBuild namespace
        var testDir = _fixture.GetTestDirectory();
        var projectDir = Path.Combine(testDir, "NamespacedTarget");
        Directory.CreateDirectory(projectDir);

        var csprojContent = @"<Project Sdk=""Microsoft.NET.Sdk"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
    <PropertyGroup>
        <TargetFramework>net8.0</TargetFramework>
    </PropertyGroup>
</Project>";
        var csprojPath = Path.Combine(projectDir, "NamespacedTarget.csproj");
        File.WriteAllText(csprojPath, csprojContent);

        // Add a simple class
        File.WriteAllText(
            Path.Combine(projectDir, "Class.cs"),
            "public class TestClass { }"
        );

        // Act
        var result = await ProjectDllResolver.PrepareAssemblyAsync(csprojPath);

        // Assert - Should parse target framework despite namespace
        result.DllPath.Should().Contain("net8.0");
        File.Exists(result.DllPath).Should().BeTrue();
    }

    #endregion

    #region Build Strategy Tests

    [Fact]
    public async Task PrepareAssemblyAsync_WithAlwaysBuild_AlwaysRebuilds()
    {
        // Arrange
        var builder = new TestProjectBuilder("TestProject");
        builder.AddInterface("ICommand");
        var csprojPath = builder.Build(_fixture.GetTestDirectory());

        // First build
        var result1 = await ProjectDllResolver.PrepareAssemblyAsync(
            csprojPath,
            new() { BuildStrategy = BuildOptions.AlwaysBuild }
        );
        result1.BuiltAutomatically.Should().BeTrue();

        // Act - Second call with AlwaysBuild (should rebuild despite fresh DLL)
        var result2 = await ProjectDllResolver.PrepareAssemblyAsync(
            csprojPath,
            new() { BuildStrategy = BuildOptions.AlwaysBuild }
        );

        // Assert
        result2.BuiltAutomatically.Should().BeTrue("AlwaysBuild should always rebuild");
    }

    [Fact]
    public async Task PrepareAssemblyAsync_WithAutoBuild_SkipsRebuildWhenFresh()
    {
        // Arrange
        var builder = new TestProjectBuilder("TestProject");
        builder.AddInterface("ICommand");
        var csprojPath = builder.Build(_fixture.GetTestDirectory());

        // First build
        var result1 = await ProjectDllResolver.PrepareAssemblyAsync(
            csprojPath,
            new() { BuildStrategy = BuildOptions.AutoBuild }
        );
        result1.BuiltAutomatically.Should().BeTrue();

        // Act - Second call immediately with AutoBuild
        var result2 = await ProjectDllResolver.PrepareAssemblyAsync(
            csprojPath,
            new() { BuildStrategy = BuildOptions.AutoBuild }
        );

        // Assert
        result2.BuiltAutomatically.Should().BeFalse("AutoBuild should skip when DLL is fresh");
    }

    [Fact]
    public async Task PrepareAssemblyAsync_WithAutoBuild_RebuildWhenStale()
    {
        // Arrange
        var testDir = _fixture.GetTestDirectory();
        var builder = new TestProjectBuilder("TestProject");
        builder.AddInterface("ICommand");
        var csprojPath = builder.Build(testDir);

        // First build
        var result1 = await ProjectDllResolver.PrepareAssemblyAsync(
            csprojPath,
            new() { BuildStrategy = BuildOptions.AutoBuild }
        );
        result1.BuiltAutomatically.Should().BeTrue();

        // Modify source file
        var csFile = Path.Combine(testDir, "TestProject", "ICommand.cs");
        System.Threading.Thread.Sleep(10);
        File.WriteAllText(csFile, "public interface ICommand { /* modified */ }");

        // Act - Second call with AutoBuild after modification
        // Must use CheckForEdit=true to detect file changes
        var result2 = await ProjectDllResolver.PrepareAssemblyAsync(
            csprojPath,
            new() 
            { 
                BuildStrategy = BuildOptions.AutoBuild,
                CheckForEdit = true
            }
        );

        // Assert
        result2.BuiltAutomatically.Should().BeTrue("AutoBuild should rebuild when source changed");
    }

    #endregion
}
