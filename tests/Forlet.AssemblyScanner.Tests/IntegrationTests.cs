using FluentAssertions;
using Forlet.AssemblyScanner.Tests.Helpers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Forlet.AssemblyScanner.Tests;

[Collection("TestProjects")]
public class IntegrationTests
{
    private readonly TestProjectFixture _fixture;

    public IntegrationTests(TestProjectFixture fixture)
    {
        _fixture = fixture;
    }

    #region Full Workflow Tests

    [Fact]
    public async Task FullWorkflow_Build_Scan_FindTypes_Complete()
    {
        // Arrange - Create project with handler interface and implementations
        var builder = new TestProjectBuilder("TestProject");
        builder.AddInterface("ICommand");
        builder.AddClass("CreateCommand", "ICommand");
        builder.AddClass("DeleteCommand", "ICommand");
        var csprojPath = builder.Build(_fixture.GetTestDirectory());

        // Act 1 - Build the project
        var buildResult = await ProjectDllResolver.PrepareAssemblyAsync(csprojPath);

        // Assert 1 - Build succeeded
        buildResult.Should().NotBeNull();
        buildResult.BuiltAutomatically.Should().BeTrue();
        File.Exists(buildResult.DllPath).Should().BeTrue();

        // Act 2 - Scan for types
        using var scanner = new MetadataScanner(buildResult.DllPath);
        var implementations = scanner.FindTypesImplementing("ICommand");

        // Assert 2 - Found implementations
        implementations.Should().HaveCount(2);
        implementations.Should().ContainSingle(t => t.Name == "CreateCommand");
        implementations.Should().ContainSingle(t => t.Name == "DeleteCommand");
    }

    [Fact]
    public async Task IntegrationTest_ModifySource_TriggersBuild()
    {
        // Arrange
        var testDir = _fixture.GetTestDirectory();
        var builder = new TestProjectBuilder("TestProject");
        builder.AddInterface("ICommand");
        var csprojPath = builder.Build(testDir);

        // Act 1 - Initial build
        var result1 = await ProjectDllResolver.PrepareAssemblyAsync(csprojPath);
        result1.BuiltAutomatically.Should().BeTrue();

        // Act 2 - Modify source
        var csFile = Path.Combine(testDir, "TestProject", "ICommand.cs");
        Thread.Sleep(10);
        File.WriteAllText(csFile, "public interface ICommand { /* v2 */ }");

        // Act 3 - Rebuild with CheckForEdit enabled to detect file changes
        var result2 = await ProjectDllResolver.PrepareAssemblyAsync(
            csprojPath,
            new() { CheckForEdit = true }
        );

        // Assert - Build was triggered
        result2.BuiltAutomatically.Should().BeTrue("source change should trigger rebuild");
    }

    [Fact]
    public async Task IntegrationTest_MultipleInterfaces_ComplexTypes()
    {
        // Arrange - Create complex project structure
        var builder = new TestProjectBuilder("TestProject");
        builder.AddInterface("ICommand");
        builder.AddInterface("IQuery");
        builder.AddClass("CreateUserCommand", "ICommand");
        builder.AddClass("GetUserQuery", "IQuery");
        builder.AddClass("DeleteUserCommand", "ICommand");
        var csprojPath = builder.Build(_fixture.GetTestDirectory());

        // Act
        var buildResult = await ProjectDllResolver.PrepareAssemblyAsync(csprojPath);

        using var scanner = new MetadataScanner(buildResult.DllPath);
        var commands = scanner.FindTypesImplementing("ICommand");
        var queries = scanner.FindTypesImplementing("IQuery");

        // Assert
        commands.Should().HaveCount(2);
        queries.Should().HaveCount(1);
        commands.Should().ContainSingle(t => t.Name == "CreateUserCommand");
        commands.Should().ContainSingle(t => t.Name == "DeleteUserCommand");
        queries.Should().ContainSingle(t => t.Name == "GetUserQuery");
    }

    [Fact]
    public async Task IntegrationTest_WithCallbacks_TrackingBuildProcess()
    {
        // Arrange
        var builder = new TestProjectBuilder("TestProject");
        builder.AddInterface("ICommand");
        var csprojPath = builder.Build(_fixture.GetTestDirectory());

        var buildStarted = false;
        var buildSucceeded = false;

        // Act - Build with callback
        var result = await ProjectDllResolver.PrepareAssemblyAsync(
            csprojPath,
            new()
            {
                OnBuildStart = () => { buildStarted = true; }
            }
        );

        buildSucceeded = result.BuiltAutomatically && File.Exists(result.DllPath);

        // Assert - Tracked build process
        buildStarted.Should().BeTrue();
        buildSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task IntegrationTest_PathsToCheck_PartialScanning()
    {
        // Arrange
        var testDir = _fixture.GetTestDirectory();
        var builder = new TestProjectBuilder("TestProject");
        builder.AddInterface("ICommand");
        var csprojPath = builder.Build(testDir);

        // Create specific directory structure
        var commandsDir = Path.Combine(testDir, "TestProject", "Commands");
        var queriesDir = Path.Combine(testDir, "TestProject", "Queries");
        Directory.CreateDirectory(commandsDir);
        Directory.CreateDirectory(queriesDir);

        var cmdFile = Path.Combine(commandsDir, "Command.cs");
        var queryFile = Path.Combine(queriesDir, "Query.cs");
        File.WriteAllText(cmdFile, "public class Command { }");
        File.WriteAllText(queryFile, "public class Query { }");

        // Act 1 - Initial build
        var result1 = await ProjectDllResolver.PrepareAssemblyAsync(csprojPath);
        result1.BuiltAutomatically.Should().BeTrue();

        // Act 2 - Modify Queries directory (but check only Commands)
        Thread.Sleep(10);
        File.WriteAllText(queryFile, "public class Query { /* modified */ }");

        var result2 = await ProjectDllResolver.PrepareAssemblyAsync(
            csprojPath,
            new()
            {
                PathsToCheck = new[] { "Commands" }
            }
        );

        // Assert - Should not rebuild since only Queries changed
        result2.BuiltAutomatically.Should().BeFalse();
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task IntegrationTest_InvalidProjectPath_ThrowsException()
    {
        // Arrange
        var invalidPath = Path.Combine(_fixture.GetTestDirectory(), "NonExistent", "Project.csproj");

        // Act & Assert
        await Assert.ThrowsAsync<ScanException>(() =>
            ProjectDllResolver.PrepareAssemblyAsync(invalidPath)
        );
    }

    [Fact]
    public async Task IntegrationTest_MultipleBuildAttempts_ConsistentResults()
    {
        // Arrange
        var builder = new TestProjectBuilder("TestProject");
        builder.AddInterface("ICommand");
        builder.AddClass("MyCommand", "ICommand");
        var csprojPath = builder.Build(_fixture.GetTestDirectory());

        // Act - Multiple builds
        var result1 = await ProjectDllResolver.PrepareAssemblyAsync(csprojPath);
        var result2 = await ProjectDllResolver.PrepareAssemblyAsync(csprojPath);
        var result3 = await ProjectDllResolver.PrepareAssemblyAsync(csprojPath);

        using var scanner = new MetadataScanner(result3.DllPath);
        var types = scanner.FindTypesImplementing("ICommand");

        // Assert - Consistent results across builds
        result1.DllPath.Should().Be(result2.DllPath).And.Be(result3.DllPath);
        types.Should().HaveCount(1);
        result1.BuiltAutomatically.Should().BeTrue();
        result2.BuiltAutomatically.Should().BeFalse();
        result3.BuiltAutomatically.Should().BeFalse();
    }

    #endregion

    #region MatchTargetFullName Tests

    [Fact]
    public async Task FindTypeByNameImplementing_MatchTargetFullNameTrue_MatchesByFullName()
    {
        // Arrange - Create project with namespaced type
        var builder = new TestProjectBuilder("TestProject");
        builder.AddInterface("ICommand");
        builder.AddClass("CreateCommand", "ICommand");
        var csprojPath = builder.Build(_fixture.GetTestDirectory());

        // Act - Build and scan
        var buildResult = await ProjectDllResolver.PrepareAssemblyAsync(csprojPath);
        using var scanner = new MetadataScanner(buildResult.DllPath);

        // Assert - Full name match should find the type
        var found = scanner.FindTypeByNameImplementing(
            "TestProject.CreateCommand",
            "ICommand",
            matchTargetFullName: true
        );

        found.Should().NotBeNull();
        found!.Name.Should().Be("CreateCommand");
        found.FullName.Should().Be("TestProject.CreateCommand");
    }

    [Fact]
    public async Task FindTypeByNameImplementing_MatchTargetFullNameFalse_MatchesBySimpleName()
    {
        // Arrange - Create project with namespaced type
        var builder = new TestProjectBuilder("TestProject");
        builder.AddInterface("ICommand");
        builder.AddClass("CreateCommand", "ICommand");
        var csprojPath = builder.Build(_fixture.GetTestDirectory());

        // Act - Build and scan
        var buildResult = await ProjectDllResolver.PrepareAssemblyAsync(csprojPath);
        using var scanner = new MetadataScanner(buildResult.DllPath);

        // Assert - Simple name match should find the type (default behavior)
        var found = scanner.FindTypeByNameImplementing(
            "CreateCommand",
            "ICommand",
            matchTargetFullName: false
        );

        found.Should().NotBeNull();
        found!.Name.Should().Be("CreateCommand");
    }

    [Fact]
    public async Task FindTypeByNameImplementing_MatchTargetFullNameFalse_DoesNotMatchFullName()
    {
        // Arrange - Create project with namespaced type
        var builder = new TestProjectBuilder("TestProject");
        builder.AddInterface("ICommand");
        builder.AddClass("CreateCommand", "ICommand");
        var csprojPath = builder.Build(_fixture.GetTestDirectory());

        // Act - Build and scan
        var buildResult = await ProjectDllResolver.PrepareAssemblyAsync(csprojPath);
        using var scanner = new MetadataScanner(buildResult.DllPath);

        // Assert - Simple name matching should NOT match when looking for full name
        var found = scanner.FindTypeByNameImplementing(
            "TestProject.CreateCommand",  // Full name with matchTargetFullName=false
            "ICommand",
            matchTargetFullName: false
        );

        found.Should().BeNull();
    }

    [Fact]
    public async Task FindTypeByNameImplementing_MatchTargetFullNameTrue_DoesNotMatchSimpleName()
    {
        // Arrange - Create project with namespaced type
        var builder = new TestProjectBuilder("TestProject");
        builder.AddInterface("ICommand");
        builder.AddClass("CreateCommand", "ICommand");
        var csprojPath = builder.Build(_fixture.GetTestDirectory());

        // Act - Build and scan
        var buildResult = await ProjectDllResolver.PrepareAssemblyAsync(csprojPath);
        using var scanner = new MetadataScanner(buildResult.DllPath);

        // Assert - Full name matching should NOT match when looking for simple name
        var found = scanner.FindTypeByNameImplementing(
            "CreateCommand",  // Simple name with matchTargetFullName=true
            "ICommand",
            matchTargetFullName: true
        );

        found.Should().BeNull();
    }

    [Fact]
    public async Task FindTypeByNameDerivedFrom_MatchTargetFullNameTrue_MatchesByFullName()
    {
        // Arrange - Create project with base class and derived class
        var builder = new TestProjectBuilder("TestProject");
        builder.AddClass("Entity");
        builder.AddClass("User", inherits: "Entity");
        var csprojPath = builder.Build(_fixture.GetTestDirectory());

        // Act - Build and scan
        var buildResult = await ProjectDllResolver.PrepareAssemblyAsync(csprojPath);
        using var scanner = new MetadataScanner(buildResult.DllPath);

        // Assert - Full name match should find the type
        var found = scanner.FindTypeByNameDerivedFrom(
            "TestProject.User",
            "Entity",
            matchTargetFullName: true
        );

        found.Should().NotBeNull();
        found!.Name.Should().Be("User");
        found.FullName.Should().Be("TestProject.User");
    }

    [Fact]
    public async Task FindTypeByNameDerivedFrom_MatchTargetFullNameFalse_MatchesBySimpleName()
    {
        // Arrange - Create project with base class and derived class
        var builder = new TestProjectBuilder("TestProject");
        builder.AddClass("Entity");
        builder.AddClass("User", inherits: "Entity");
        var csprojPath = builder.Build(_fixture.GetTestDirectory());

        // Act - Build and scan
        var buildResult = await ProjectDllResolver.PrepareAssemblyAsync(csprojPath);
        using var scanner = new MetadataScanner(buildResult.DllPath);

        // Assert - Simple name match should find the type (default behavior)
        var found = scanner.FindTypeByNameDerivedFrom(
            "User",
            "Entity",
            matchTargetFullName: false
        );

        found.Should().NotBeNull();
        found!.Name.Should().Be("User");
    }

    [Fact]
    public async Task FindTypeByNameImplementing_MatchTargetFullNameIndependentFromMatchFullName()
    {
        // Arrange - Create project with types in different namespaces
        var builder = new TestProjectBuilder("TestProject");
        builder.AddInterface("ICommand");
        builder.AddClass("CreateCommand", "ICommand");
        var csprojPath = builder.Build(_fixture.GetTestDirectory());

        // Act - Build and scan
        var buildResult = await ProjectDllResolver.PrepareAssemblyAsync(csprojPath);
        using var scanner = new MetadataScanner(buildResult.DllPath);

        // Assert - matchTargetFullName=false + options.MatchFullName=true
        // Target type matched by short name, interface matched by full name
        var found = scanner.FindTypeByNameImplementing(
            "CreateCommand",  // Simple name for target type
            "TestProject.ICommand",  // Full name for interface
            new ScanOptions { MatchFullName = true },
            matchTargetFullName: false  // Use simple name matching for target
        );

        found.Should().NotBeNull();
        found!.Name.Should().Be("CreateCommand");
    }

    [Fact]
    public async Task FindTypeByNameImplementing_MatchTargetFullNameTrue_WithInterfaceSimpleName()
    {
        // Arrange - Create project with namespaced type
        var builder = new TestProjectBuilder("TestProject");
        builder.AddInterface("ICommand");
        builder.AddClass("CreateCommand", "ICommand");
        var csprojPath = builder.Build(_fixture.GetTestDirectory());

        // Act - Build and scan
        var buildResult = await ProjectDllResolver.PrepareAssemblyAsync(csprojPath);
        using var scanner = new MetadataScanner(buildResult.DllPath);

        // Assert - matchTargetFullName=true for target, simple name for interface
        var found = scanner.FindTypeByNameImplementing(
            "TestProject.CreateCommand",  // Full name for target type
            "ICommand",  // Simple name for interface
            matchTargetFullName: true
        );

        found.Should().NotBeNull();
        found!.Name.Should().Be("CreateCommand");
    }

    #endregion
}
