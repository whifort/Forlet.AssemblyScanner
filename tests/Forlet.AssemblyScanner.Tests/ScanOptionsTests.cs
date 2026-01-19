using FluentAssertions;
using Forlet.AssemblyScanner.Tests.Helpers;
using System.Threading.Tasks;
using Xunit;

namespace Forlet.AssemblyScanner.Tests;

[Collection("TestProjects")]
public class ScanOptionsTests
{
    private readonly TestProjectFixture _fixture;

    public ScanOptionsTests(TestProjectFixture fixture)
    {
        _fixture = fixture;
    }

    #region IncludeAbstract Tests

    // Tests for IncludeAbstract are in MetadataScannerTests
    
    #endregion

    #region IncludeNonPublic Tests

    // Tests for IncludeNonPublic are in MetadataScannerTests
    
    #endregion

    #region IncludeStructs Tests

    [Fact]
    public async Task FindTypes_WithIncludeStructs_IncludesStructs()
    {
        // Arrange
        var builder = new TestProjectBuilder("TestProject");
        builder.AddInterface("IHandler");
        builder.AddClass("ClassHandler", "IHandler");
        builder.AddStruct("StructHandler", "IHandler");
        var csprojPath = builder.Build(_fixture.GetTestDirectory());

        var result = await ProjectDllResolver.PrepareAssemblyAsync(csprojPath);

        using var scanner = new MetadataScanner(result.DllPath);

        // Act
        var types = scanner.FindTypesImplementing("IHandler", new ScanOptions { IncludeStructs = true });

        // Assert
        types.Should().HaveCount(2);
        types.Should().ContainSingle(t => t.Name == "ClassHandler");
        types.Should().ContainSingle(t => t.Name == "StructHandler");
    }

    #endregion

    #region IncludeNestedTypes Tests

    [Fact]
    public async Task FindTypes_WithIncludeNestedTypes_IncludesNestedClasses()
    {
        // Arrange
        var builder = new TestProjectBuilder("TestProject");
        builder.AddInterface("IHandler");
        builder.AddClass("TopLevelHandler", "IHandler");
        builder.AddNestedClass("Container", "NestedHandler", "IHandler");
        var csprojPath = builder.Build(_fixture.GetTestDirectory());

        var result = await ProjectDllResolver.PrepareAssemblyAsync(csprojPath);

        using var scanner = new MetadataScanner(result.DllPath);

        // Act
        var types = scanner.FindTypesImplementing("IHandler", new ScanOptions { IncludeNestedTypes = true });

        // Assert
        types.Should().HaveCount(2);
        types.Should().ContainSingle(t => t.Name == "TopLevelHandler");
        types.Should().ContainSingle(t => t.Name == "NestedHandler");
    }

    #endregion

    #region MatchFullName Tests

    [Fact]
    public async Task FindTypes_WithMatchFullNameTrue_RequiresFullNameMatch()
    {
        // Arrange
        var builder = new TestProjectBuilder("TestProject");
        builder.AddInterface("ICommand");
        builder.AddClass("Command", "ICommand");
        var csprojPath = builder.Build(_fixture.GetTestDirectory());

        var result = await ProjectDllResolver.PrepareAssemblyAsync(csprojPath);

        using var scanner = new MetadataScanner(result.DllPath);

        // Act - Search with full namespace
        var types = scanner.FindTypesImplementing("TestProject.ICommand", new ScanOptions { MatchFullName = true });

        // Assert
        types.Should().ContainSingle(t => t.Name == "Command");
    }

    [Fact]
    public async Task FindTypes_WithMatchFullNameFalse_AllowsPartialNameMatch()
    {
        // Arrange
        var builder = new TestProjectBuilder("TestProject");
        builder.AddInterface("ICommand");
        builder.AddClass("Command", "ICommand");
        var csprojPath = builder.Build(_fixture.GetTestDirectory());

        var result = await ProjectDllResolver.PrepareAssemblyAsync(csprojPath);

        using var scanner = new MetadataScanner(result.DllPath);

        // Act - Search with just interface name
        var types = scanner.FindTypesImplementing("ICommand", new ScanOptions { MatchFullName = false });

        // Assert
        types.Should().ContainSingle(t => t.Name == "Command");
    }

    #endregion

    #region Combined Options Tests

    [Fact]
    public async Task FindTypes_WithMultipleOptionsEnabled_CombinesFilters()
    {
        // Arrange
        var builder = new TestProjectBuilder("TestProject");
        builder.AddInterface("IHandler");
        builder.AddClass("AbstractHandler", "IHandler", isAbstract: true);
        builder.AddClass("InternalHandler", "IHandler", isInternal: true);
        builder.AddClass("ConcretePublicHandler", "IHandler");
        var csprojPath = builder.Build(_fixture.GetTestDirectory());

        var result = await ProjectDllResolver.PrepareAssemblyAsync(csprojPath);

        using var scanner = new MetadataScanner(result.DllPath);

        // Act - Enable all options
        var types = scanner.FindTypesImplementing("IHandler", new ScanOptions
        {
            IncludeAbstract = true,
            IncludeNonPublic = true,
            MatchFullName = false
        });

        // Assert - Should find all three
        types.Should().HaveCount(3);
    }

    #endregion

    #region Default Options Tests

    [Fact]
    public async Task ScanOptions_DefaultValues_ExcludesAbstractAndInternal()
    {
        // Arrange
        var builder = new TestProjectBuilder("TestProject");
        builder.AddInterface("IHandler");
        builder.AddClass("AbstractHandler", "IHandler", isAbstract: true);
        builder.AddClass("InternalHandler", "IHandler", isInternal: true);
        builder.AddClass("ConcretePublicHandler", "IHandler");
        var csprojPath = builder.Build(_fixture.GetTestDirectory());

        var result = await ProjectDllResolver.PrepareAssemblyAsync(csprojPath);

        using var scanner = new MetadataScanner(result.DllPath);

        // Act - Use default options
        var types = scanner.FindTypesImplementing("IHandler", new ScanOptions());

        // Assert - Should find only concrete public handler
        types.Should().HaveCount(1);
        types.Should().ContainSingle(t => t.Name == "ConcretePublicHandler");
    }

    #endregion
}
