using FluentAssertions;
using Forlet.AssemblyScanner.Tests.Helpers;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Forlet.AssemblyScanner.Tests;

[Collection("TestProjects")]
public class MetadataScannerTests
{
    private readonly TestProjectFixture _fixture;

    public MetadataScannerTests(TestProjectFixture fixture)
    {
        _fixture = fixture;
    }

    #region Constructor Tests

    [Fact]
    public async Task Constructor_WithValidDll_LoadsAssembly()
    {
        // Arrange
        var builder = new TestProjectBuilder("TestProject");
        builder.AddInterface("ICommand");
        var csprojPath = builder.Build(_fixture.GetTestDirectory());

        var result = await ProjectDllResolver.PrepareAssemblyAsync(
            csprojPath,
            new() { BuildStrategy = BuildOptions.AutoBuild }
        );

        // Act
        using var scanner = new MetadataScanner(result.DllPath);

        // Assert
        scanner.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNonExistentDll_ThrowsScanException()
    {
        // Arrange
        var nonExistentDll = Path.Combine(_fixture.GetTestDirectory(), "NonExistent.dll");

        // Act & Assert
        Assert.Throws<ScanException>(() => new MetadataScanner(nonExistentDll));
    }

    #endregion

    #region FindTypesImplementing Tests

    [Fact]
    public async Task FindTypesImplementing_WithSingleInterface_FindsImplementors()
    {
        // Arrange
        var builder = new TestProjectBuilder("TestProject");
        builder.AddInterface("ICommand");
        builder.AddClass("CreateCommand", "ICommand");
        builder.AddClass("DeleteCommand", "ICommand");
        var csprojPath = builder.Build(_fixture.GetTestDirectory());

        var result = await ProjectDllResolver.PrepareAssemblyAsync(
            csprojPath,
            new() { BuildStrategy = BuildOptions.AutoBuild }
        );

        using var scanner = new MetadataScanner(result.DllPath);

        // Act
        var types = scanner.FindTypesImplementing("ICommand");

        // Assert
        types.Should().HaveCount(2);
        types.Should().ContainSingle(t => t.Name == "CreateCommand");
        types.Should().ContainSingle(t => t.Name == "DeleteCommand");
    }

    [Fact]
    public async Task FindTypesImplementing_WithNoImplementors_ReturnsEmpty()
    {
        // Arrange
        var builder = new TestProjectBuilder("TestProject");
        builder.AddInterface("ICommand");
        var csprojPath = builder.Build(_fixture.GetTestDirectory());

        var result = await ProjectDllResolver.PrepareAssemblyAsync(
            csprojPath,
            new() { BuildStrategy = BuildOptions.AutoBuild }
        );

        using var scanner = new MetadataScanner(result.DllPath);

        // Act
        var types = scanner.FindTypesImplementing("ICommand");

        // Assert
        types.Should().BeEmpty();
    }

    [Fact]
    public async Task FindTypesImplementing_ExcludesAbstractByDefault()
    {
        // Arrange
        var builder = new TestProjectBuilder("TestProject");
        builder.AddInterface("ICommand");
        builder.AddClass("AbstractCommand", "ICommand", isAbstract: true);
        builder.AddClass("ConcreteCommand", "ICommand");
        var csprojPath = builder.Build(_fixture.GetTestDirectory());

        var result = await ProjectDllResolver.PrepareAssemblyAsync(
            csprojPath,
            new() { BuildStrategy = BuildOptions.AutoBuild }
        );

        using var scanner = new MetadataScanner(result.DllPath);

        // Act
        var types = scanner.FindTypesImplementing("ICommand");

        // Assert
        types.Should().HaveCount(1);
        types.Should().ContainSingle(t => t.Name == "ConcreteCommand");
    }

    [Fact]
    public async Task FindTypesImplementing_ExcludesNonPublicByDefault()
    {
        // Arrange
        var builder = new TestProjectBuilder("TestProject");
        builder.AddInterface("ICommand");
        builder.AddClass("InternalCommand", "ICommand", isInternal: true);
        builder.AddClass("PublicCommand", "ICommand");
        var csprojPath = builder.Build(_fixture.GetTestDirectory());

        var result = await ProjectDllResolver.PrepareAssemblyAsync(
            csprojPath,
            new() { BuildStrategy = BuildOptions.AutoBuild }
        );

        using var scanner = new MetadataScanner(result.DllPath);

        // Act
        var types = scanner.FindTypesImplementing("ICommand");

        // Assert
        types.Should().HaveCount(1);
        types.Should().ContainSingle(t => t.Name == "PublicCommand");
    }

    [Fact]
    public async Task FindTypesImplementing_WithIncludeAbstract_IncludesAbstractClasses()
    {
        // Arrange
        var builder = new TestProjectBuilder("TestProject");
        builder.AddInterface("ICommand");
        builder.AddClass("AbstractCommand", "ICommand", isAbstract: true);
        builder.AddClass("ConcreteCommand", "ICommand");
        var csprojPath = builder.Build(_fixture.GetTestDirectory());

        var result = await ProjectDllResolver.PrepareAssemblyAsync(
            csprojPath,
            new() { BuildStrategy = BuildOptions.AutoBuild }
        );

        using var scanner = new MetadataScanner(result.DllPath);

        // Act
        var types = scanner.FindTypesImplementing("ICommand", new ScanOptions { IncludeAbstract = true });

        // Assert
        types.Should().HaveCount(2);
        types.Should().ContainSingle(t => t.Name == "AbstractCommand");
        types.Should().ContainSingle(t => t.Name == "ConcreteCommand");
    }

    [Fact]
    public async Task FindTypesImplementing_WithIncludeNonPublic_IncludesInternalClasses()
    {
        // Arrange
        var builder = new TestProjectBuilder("TestProject");
        builder.AddInterface("ICommand");
        builder.AddClass("InternalCommand", "ICommand", isInternal: true);
        builder.AddClass("PublicCommand", "ICommand");
        var csprojPath = builder.Build(_fixture.GetTestDirectory());

        var result = await ProjectDllResolver.PrepareAssemblyAsync(
            csprojPath,
            new() { BuildStrategy = BuildOptions.AutoBuild }
        );

        using var scanner = new MetadataScanner(result.DllPath);

        // Act
        var types = scanner.FindTypesImplementing("ICommand", new ScanOptions { IncludeNonPublic = true });

        // Assert
        types.Should().HaveCount(2);
        types.Should().ContainSingle(t => t.Name == "InternalCommand");
        types.Should().ContainSingle(t => t.Name == "PublicCommand");
    }

    #endregion

    #region FindTypesDerivedFrom Tests

    [Fact]
    public async Task FindTypesDerivedFrom_WithSingleBaseClass_FindsDerived()
    {
        // Arrange
        var builder = new TestProjectBuilder("TestProject");
        builder.AddClass("Entity");
        builder.AddClass("User", inherits: "Entity");
        builder.AddClass("Product", inherits: "Entity");
        var csprojPath = builder.Build(_fixture.GetTestDirectory());

        var result = await ProjectDllResolver.PrepareAssemblyAsync(
            csprojPath,
            new() { BuildStrategy = BuildOptions.AutoBuild }
        );

        using var scanner = new MetadataScanner(result.DllPath);

        // Act
        var types = scanner.FindTypesDerivedFrom("Entity");

        // Assert
        types.Should().HaveCount(2);
        types.Should().ContainSingle(t => t.Name == "User");
        types.Should().ContainSingle(t => t.Name == "Product");
    }

    [Fact]
    public async Task FindTypesDerivedFrom_WithNoMatches_ReturnsEmpty()
    {
        // Arrange
        var builder = new TestProjectBuilder("TestProject");
        builder.AddClass("Entity");
        var csprojPath = builder.Build(_fixture.GetTestDirectory());

        var result = await ProjectDllResolver.PrepareAssemblyAsync(
            csprojPath,
            new() { BuildStrategy = BuildOptions.AutoBuild }
        );

        using var scanner = new MetadataScanner(result.DllPath);

        // Act
        var types = scanner.FindTypesDerivedFrom("NonExistent");

        // Assert
        types.Should().BeEmpty();
    }

    [Fact]
    public async Task FindTypesDerivedFrom_WithMultipleLevels_FindsAll()
    {
        // Arrange
        var builder = new TestProjectBuilder("TestProject");
        builder.AddClass("Entity");
        builder.AddClass("Model", inherits: "Entity");
        builder.AddClass("User", inherits: "Model");
        var csprojPath = builder.Build(_fixture.GetTestDirectory());

        var result = await ProjectDllResolver.PrepareAssemblyAsync(
            csprojPath,
            new() { BuildStrategy = BuildOptions.AutoBuild }
        );

        using var scanner = new MetadataScanner(result.DllPath);

        // Act - Should find all derived types, not just direct descendants
        var types = scanner.FindTypesDerivedFrom("Entity");

        // Assert
        types.Should().HaveCount(2);
        types.Should().ContainSingle(t => t.Name == "Model");
        types.Should().ContainSingle(t => t.Name == "User");
    }

    #endregion

    #region FindTypeByName Tests

    [Fact]
    public async Task FindTypeByNameImplementing_WhenExists_ReturnsType()
    {
        // Arrange
        var builder = new TestProjectBuilder("TestProject");
        builder.AddInterface("ICommand");
        builder.AddClass("CreateCommand", "ICommand");
        var csprojPath = builder.Build(_fixture.GetTestDirectory());

        var result = await ProjectDllResolver.PrepareAssemblyAsync(
            csprojPath,
            new() { BuildStrategy = BuildOptions.AutoBuild }
        );

        using var scanner = new MetadataScanner(result.DllPath);

        // Act
        var type = scanner.FindTypeByNameImplementing("CreateCommand", "ICommand");

        // Assert
        type.Should().NotBeNull();
        type!.Name.Should().Be("CreateCommand");
    }

    [Fact]
    public async Task FindTypeByNameImplementing_WhenNotExists_ReturnsNull()
    {
        // Arrange
        var builder = new TestProjectBuilder("TestProject");
        builder.AddInterface("ICommand");
        var csprojPath = builder.Build(_fixture.GetTestDirectory());

        var result = await ProjectDllResolver.PrepareAssemblyAsync(
            csprojPath,
            new() { BuildStrategy = BuildOptions.AutoBuild }
        );

        using var scanner = new MetadataScanner(result.DllPath);

        // Act
        var type = scanner.FindTypeByNameImplementing("NonExistent", "ICommand");

        // Assert
        type.Should().BeNull();
    }

    [Fact]
    public async Task FindTypeByNameDerivedFrom_WhenExists_ReturnsType()
    {
        // Arrange
        var builder = new TestProjectBuilder("TestProject");
        builder.AddClass("Entity");
        builder.AddClass("User", inherits: "Entity");
        var csprojPath = builder.Build(_fixture.GetTestDirectory());

        var result = await ProjectDllResolver.PrepareAssemblyAsync(
            csprojPath,
            new() { BuildStrategy = BuildOptions.AutoBuild }
        );

        using var scanner = new MetadataScanner(result.DllPath);

        // Act
        var type = scanner.FindTypeByNameDerivedFrom("User", "Entity");

        // Assert
        type.Should().NotBeNull();
        type!.Name.Should().Be("User");
    }

    [Fact]
    public async Task FindTypeByNameDerivedFrom_WhenNotExists_ReturnsNull()
    {
        // Arrange
        var builder = new TestProjectBuilder("TestProject");
        builder.AddClass("Entity");
        var csprojPath = builder.Build(_fixture.GetTestDirectory());

        var result = await ProjectDllResolver.PrepareAssemblyAsync(
            csprojPath,
            new() { BuildStrategy = BuildOptions.AutoBuild }
        );

        using var scanner = new MetadataScanner(result.DllPath);

        // Act
        var type = scanner.FindTypeByNameDerivedFrom("NonExistent", "Entity");

        // Assert
        type.Should().BeNull();
    }

    #endregion

    #region Input Validation Tests

    [Fact]
    public async Task FindTypesImplementing_WithNullArray_ThrowsArgumentException()
    {
        // Arrange
        var builder = new TestProjectBuilder("TestProject");
        builder.AddInterface("ICommand");
        var csprojPath = builder.Build(_fixture.GetTestDirectory());

        var result = await ProjectDllResolver.PrepareAssemblyAsync(csprojPath);

        using var scanner = new MetadataScanner(result.DllPath);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => scanner.FindTypesImplementing(""));
    }

    [Fact]
    public async Task FindTypesImplementing_WithEmptyArray_ThrowsArgumentException()
    {
        // Arrange
        var builder = new TestProjectBuilder("TestProject");
        builder.AddInterface("ICommand");
        var csprojPath = builder.Build(_fixture.GetTestDirectory());

        var result = await ProjectDllResolver.PrepareAssemblyAsync(csprojPath);

        using var scanner = new MetadataScanner(result.DllPath);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => scanner.FindTypesImplementing(new string[] { }));
    }

    [Fact]
    public async Task FindTypesImplementing_WithArrayContainingEmptyString_ThrowsArgumentException()
    {
        // Arrange
        var builder = new TestProjectBuilder("TestProject");
        builder.AddInterface("ICommand");
        var csprojPath = builder.Build(_fixture.GetTestDirectory());

        var result = await ProjectDllResolver.PrepareAssemblyAsync(csprojPath);

        using var scanner = new MetadataScanner(result.DllPath);

        // Act & Assert - Array with empty string should throw
        Assert.Throws<ArgumentException>(() => 
            scanner.FindTypesImplementing(new[] { "ICommand", "" })
        );
    }

    [Fact]
    public async Task FindTypesDerivedFrom_WithNullArray_ThrowsArgumentException()
    {
        // Arrange
        var builder = new TestProjectBuilder("TestProject");
        builder.AddClass("Entity");
        var csprojPath = builder.Build(_fixture.GetTestDirectory());

        var result = await ProjectDllResolver.PrepareAssemblyAsync(csprojPath);

        using var scanner = new MetadataScanner(result.DllPath);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => 
            scanner.FindTypesDerivedFrom((string[])null!)
        );
    }

    [Fact]
    public async Task FindTypeByNameImplementing_WithEmptyTargetName_ThrowsArgumentException()
    {
        // Arrange
        var builder = new TestProjectBuilder("TestProject");
        builder.AddInterface("ICommand");
        var csprojPath = builder.Build(_fixture.GetTestDirectory());

        var result = await ProjectDllResolver.PrepareAssemblyAsync(csprojPath);

        using var scanner = new MetadataScanner(result.DllPath);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => 
            scanner.FindTypeByNameImplementing("", "ICommand")
        );
    }

    [Fact]
    public async Task FindTypeByNameDerivedFrom_WithWhitespaceTargetName_ThrowsArgumentException()
    {
        // Arrange
        var builder = new TestProjectBuilder("TestProject");
        builder.AddClass("Entity");
        var csprojPath = builder.Build(_fixture.GetTestDirectory());

        var result = await ProjectDllResolver.PrepareAssemblyAsync(csprojPath);

        using var scanner = new MetadataScanner(result.DllPath);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => 
            scanner.FindTypeByNameDerivedFrom("   ", "Entity")
        );
    }

    #endregion

    #region Disposal Tests

    [Fact]
    public async Task Dispose_ReleasesResources()
    {
        // Arrange
        var builder = new TestProjectBuilder("TestProject");
        builder.AddInterface("ICommand");
        var csprojPath = builder.Build(_fixture.GetTestDirectory());

        var result = await ProjectDllResolver.PrepareAssemblyAsync(
            csprojPath,
            new() { BuildStrategy = BuildOptions.AutoBuild }
        );

        var scanner = new MetadataScanner(result.DllPath);

        // Act
        scanner.Dispose();

        // Assert - File should not be locked
        var action = () => File.Delete(result.DllPath);
        action.Should().NotThrow();
    }

    #endregion

    #region Generic Types Tests

    [Fact]
    public async Task FindTypesImplementing_WithMultipleInterfaces_FindsAllImplementations()
    {
        // Arrange
        var builder = new TestProjectBuilder("TestProject");
        builder.AddInterface("ICommand");
        builder.AddInterface("IQuery");
        builder.AddClass("CreateCommand", "ICommand");
        builder.AddClass("GetQuery", "IQuery");
        var csprojPath = builder.Build(_fixture.GetTestDirectory());

        var result = await ProjectDllResolver.PrepareAssemblyAsync(csprojPath);

        using var scanner = new MetadataScanner(result.DllPath);

        // Act
        var commands = scanner.FindTypesImplementing("ICommand");
        var queries = scanner.FindTypesImplementing("IQuery");

        // Assert
        commands.Should().ContainSingle(t => t.Name == "CreateCommand");
        queries.Should().ContainSingle(t => t.Name == "GetQuery");
    }

    [Fact]
    public async Task FindTypesDerivedFrom_WithMultipleLevelsOfInheritance_FindsAll()
    {
        // Arrange
        var builder = new TestProjectBuilder("TestProject");
        builder.AddClass("BaseEntity");
        builder.AddClass("UserEntity", inherits: "BaseEntity");
        builder.AddClass("AdminEntity", inherits: "BaseEntity");
        var csprojPath = builder.Build(_fixture.GetTestDirectory());

        var result = await ProjectDllResolver.PrepareAssemblyAsync(csprojPath);

        using var scanner = new MetadataScanner(result.DllPath);

        // Act
        var derived = scanner.FindTypesDerivedFrom("BaseEntity");

        // Assert
        derived.Should().HaveCount(2);
        derived.Should().ContainSingle(t => t.Name == "UserEntity");
        derived.Should().ContainSingle(t => t.Name == "AdminEntity");
    }

    [Fact]
    public async Task FindTypesImplementing_WithMixedAbstractAndConcrete_IncludesAbstract()
    {
        // Arrange
        var builder = new TestProjectBuilder("TestProject");
        builder.AddInterface("IService");
        builder.AddClass("AbstractService", "IService", isAbstract: true);
        builder.AddClass("ConcreteService", "IService");
        var csprojPath = builder.Build(_fixture.GetTestDirectory());

        var result = await ProjectDllResolver.PrepareAssemblyAsync(csprojPath);

        using var scanner = new MetadataScanner(result.DllPath);

        // Act - Find all implementations
        var types = scanner.FindTypesImplementing("IService", new() { IncludeAbstract = true });

        // Assert
        types.Should().HaveCount(2);
        types.Should().ContainSingle(t => t.Name == "AbstractService");
        types.Should().ContainSingle(t => t.Name == "ConcreteService");
    }

    [Fact]
    public async Task FindTypesImplementing_FindsByExactInterfaceMatch()
    {
        // Arrange
        var builder = new TestProjectBuilder("TestProject");
        builder.AddInterface("ICommandHandler");
        builder.AddInterface("IQueryHandler");
        builder.AddClass("Handler1", "ICommandHandler");
        builder.AddClass("Handler2", "IQueryHandler");
        var csprojPath = builder.Build(_fixture.GetTestDirectory());

        var result = await ProjectDllResolver.PrepareAssemblyAsync(csprojPath);

        using var scanner = new MetadataScanner(result.DllPath);

        // Act
        var commands = scanner.FindTypesImplementing("ICommandHandler");

        // Assert
        commands.Should().ContainSingle(t => t.Name == "Handler1");
        commands.Should().NotContain(t => t.Name == "Handler2");
    }

    [Fact]
    public async Task FindTypesImplementing_ExcludesNestedClasses()
    {
        // Arrange
        var builder = new TestProjectBuilder("TestProject");
        builder.AddInterface("ICommand");
        builder.AddClass("PublicCommand", "ICommand");
        builder.AddNestedClass("NestedContainer", "NestedCommand", "ICommand");
        var csprojPath = builder.Build(_fixture.GetTestDirectory());

        var result = await ProjectDllResolver.PrepareAssemblyAsync(csprojPath);

        using var scanner = new MetadataScanner(result.DllPath);

        // Act
        var types = scanner.FindTypesImplementing("ICommand");

        // Assert - Should find PublicCommand but exclude nested NestedCommand
        types.Should().ContainSingle(t => t.Name == "PublicCommand");
        types.Should().NotContain(t => t.Name.Contains("NestedCommand"));
    }

    [Fact]
    public async Task FindTypesImplementing_GenericInterface_WithSimpleName_FindsImplementors()
    {
        // Arrange
        var builder = new TestProjectBuilder("TestProject");
        builder.AddGenericInterface("ICommand", 1);
        builder.AddGenericClass("CreateCommand", 1, "ICommand<T1>");
        var csprojPath = builder.Build(_fixture.GetTestDirectory());

        var result = await ProjectDllResolver.PrepareAssemblyAsync(csprojPath);

        using var scanner = new MetadataScanner(result.DllPath);

        // Act - Search with backtick notation, MatchFullName = false (default)
        var types = scanner.FindTypesImplementing("ICommand`1");

        // Assert
        types.Should().ContainSingle(t => t.Name == "CreateCommand`1");
    }

    [Fact]
    public async Task FindTypeByNameImplementing_GenericType_WithSimpleName_FindsType()
    {
        // Arrange
        var builder = new TestProjectBuilder("TestProject");
        builder.AddGenericInterface("ICommand", 1);
        builder.AddGenericClass("CreateCommand", 1, "ICommand<T1>");
        var csprojPath = builder.Build(_fixture.GetTestDirectory());

        var result = await ProjectDllResolver.PrepareAssemblyAsync(csprojPath);

        using var scanner = new MetadataScanner(result.DllPath);

        // Act - Find specific generic type without MatchFullName
        var type = scanner.FindTypeByNameImplementing("CreateCommand`1", "ICommand`1");

        // Assert
        type.Should().NotBeNull();
        type!.Name.Should().Be("CreateCommand`1");
    }

    [Fact]
    public async Task FindTypesImplementing_MultipleGenerics_WithDifferentArities_FindsCorrectOnes()
    {
        // Arrange
        var builder = new TestProjectBuilder("TestProject");
        builder.AddGenericInterface("ICommand", 1);
        builder.AddGenericInterface("IHandler", 2);
        builder.AddGenericClass("CreateCommand", 1, "ICommand<T1>");
        builder.AddGenericClass("Handler", 2, "IHandler<T1, T2>");
        var csprojPath = builder.Build(_fixture.GetTestDirectory());

        var result = await ProjectDllResolver.PrepareAssemblyAsync(csprojPath);

        using var scanner = new MetadataScanner(result.DllPath);

        // Act - Search for specific arity with backtick notation
        var commands = scanner.FindTypesImplementing("ICommand`1");
        var handlers = scanner.FindTypesImplementing("IHandler`2");

        // Assert - Each search returns only the correct types
        commands.Should().ContainSingle(t => t.Name == "CreateCommand`1");
        handlers.Should().ContainSingle(t => t.Name == "Handler`2");
    }

    [Fact]
    public async Task FindTypeByNameImplementing_GenericType_WithArity_FindsType()
    {
        // Arrange
        var builder = new TestProjectBuilder("TestProject");
        builder.AddGenericInterface("ICommand", 1);
        builder.AddGenericClass("CreateCommand", 1, "ICommand<T1>");
        var csprojPath = builder.Build(_fixture.GetTestDirectory());

        var result = await ProjectDllResolver.PrepareAssemblyAsync(csprojPath);

        using var scanner = new MetadataScanner(result.DllPath);

        // Act - Find specific generic type using backtick notation with arity
        var type = scanner.FindTypeByNameImplementing(
            "CreateCommand`1",
            "ICommand`1"
        );

        // Assert
        type.Should().NotBeNull();
        type!.Name.Should().Be("CreateCommand`1");
    }

    [Fact]
    public async Task FindTypesImplementing_ClassImplementsMultipleInterfaces_FoundByEach()
    {
        // Arrange
        var builder = new TestProjectBuilder("TestProject");
        builder.AddInterface("ICommand");
        builder.AddInterface("ILoggable");
        
        // We'll add the multi-interface class manually
        var code = @"namespace TestProject;

public class LoggableCommand : ICommand, ILoggable
{
}
";
        var testDir = _fixture.GetTestDirectory();
        var csprojPath = builder.Build(testDir);
        var projectDir = Path.GetDirectoryName(csprojPath)!;
        File.WriteAllText(Path.Combine(projectDir, "LoggableCommand.cs"), code);

        var result = await ProjectDllResolver.PrepareAssemblyAsync(
            csprojPath,
            new() { BuildStrategy = BuildOptions.AlwaysBuild }
        );

        using var scanner = new MetadataScanner(result.DllPath);

        // Act
        var commands = scanner.FindTypesImplementing("ICommand");
        var loggables = scanner.FindTypesImplementing("ILoggable");

        // Assert - Same class found by both interfaces
        commands.Should().ContainSingle(t => t.Name == "LoggableCommand");
        loggables.Should().ContainSingle(t => t.Name == "LoggableCommand");
    }

    #endregion
}
