using FluentAssertions;
using Forlet.AssemblyScanner.Internal;
using Forlet.AssemblyScanner.Tests.Helpers;
using System.IO;
using System.Threading;
using Xunit;

namespace Forlet.AssemblyScanner.Tests;

[Collection("TestProjects")]
public class StaleCheckerTests
{
    private readonly TestProjectFixture _fixture;

    public StaleCheckerTests(TestProjectFixture fixture)
    {
        _fixture = fixture;
    }

    #region DLL Existence Tests

    [Fact]
    public void IsStale_WithMissingDll_ReturnsTrue()
    {
        // Arrange
        var testDir = _fixture.GetTestDirectory();
        var csprojPath = Path.Combine(testDir, "Test.csproj");
        var dllPath = Path.Combine(testDir, "Test.dll");
        File.WriteAllText(csprojPath, "<Project></Project>");

        // Act
        var isStale = StaleChecker.IsStale(csprojPath, dllPath, null, checkForEdit: true);

        // Assert
        isStale.Should().BeTrue();
    }

    #endregion

    #region Csproj Modification Tests

    [Fact]
    public void IsStale_WithNewerCsproj_ReturnsTrue()
    {
        // Arrange
        var testDir = _fixture.GetTestDirectory();
        var csprojPath = Path.Combine(testDir, "Test.csproj");
        var dllPath = Path.Combine(testDir, "Test.dll");

        // Create DLL first
        File.WriteAllBytes(dllPath, new byte[] { 0 });

        // Wait to ensure different timestamp
        Thread.Sleep(10);

        // Create csproj after DLL (so it's newer)
        File.WriteAllText(csprojPath, "<Project></Project>");

        // Act - csproj is now newer than dll
        var isStale = StaleChecker.IsStale(csprojPath, dllPath, null, checkForEdit: true);

        // Assert
        isStale.Should().BeTrue();
    }

    [Fact]
    public void IsStale_WithOlderCsproj_ReturnsFalse()
    {
        // Arrange
        var testDir = _fixture.GetTestDirectory();
        var csprojPath = Path.Combine(testDir, "Test.csproj");
        var dllPath = Path.Combine(testDir, "Test.dll");

        // Create csproj first
        File.WriteAllText(csprojPath, "<Project></Project>");

        // Wait to ensure different timestamp
        Thread.Sleep(10);

        // Create DLL after csproj (so DLL is newer)
        File.WriteAllBytes(dllPath, new byte[] { 0 });

        // Act - DLL is newer than csproj
        var isStale = StaleChecker.IsStale(csprojPath, dllPath, null, checkForEdit: false);

        // Assert - checkForEdit is false, so only csproj check matters
        isStale.Should().BeFalse();
    }

    #endregion

    #region Source File Tests

    [Fact]
    public void IsStale_WithNewerSourceFile_ReturnsTrue()
    {
        // Arrange
        var testDir = _fixture.GetTestDirectory();
        var csprojPath = Path.Combine(testDir, "Test.csproj");
        var sourceFile = Path.Combine(testDir, "Class.cs");
        var dllPath = Path.Combine(testDir, "Test.dll");

        // Create csproj and dll
        File.WriteAllText(csprojPath, "<Project></Project>");
        File.WriteAllBytes(dllPath, new byte[] { 0 });

        // Wait to ensure different timestamp
        Thread.Sleep(10);

        // Create source file after DLL (newer)
        File.WriteAllText(sourceFile, "public class Test { }");

        // Act
        var isStale = StaleChecker.IsStale(csprojPath, dllPath, null, checkForEdit: true);

        // Assert
        isStale.Should().BeTrue();
    }

    [Fact]
    public void IsStale_WithOlderSourceFile_ReturnsFalse()
    {
        // Arrange
        var testDir = _fixture.GetTestDirectory();
        var csprojPath = Path.Combine(testDir, "Test.csproj");
        var sourceFile = Path.Combine(testDir, "Class.cs");
        var dllPath = Path.Combine(testDir, "Test.dll");

        // Create source file first
        File.WriteAllText(sourceFile, "public class Test { }");
        File.WriteAllText(csprojPath, "<Project></Project>");

        // Wait to ensure different timestamp
        Thread.Sleep(10);

        // Create DLL after source file (newer)
        File.WriteAllBytes(dllPath, new byte[] { 0 });

        // Act
        var isStale = StaleChecker.IsStale(csprojPath, dllPath, null, checkForEdit: true);

        // Assert
        isStale.Should().BeFalse();
    }

    [Fact]
    public void IsStale_ExcludesObjDirectory()
    {
        // Arrange
        var testDir = _fixture.GetTestDirectory();
        var csprojPath = Path.Combine(testDir, "Test.csproj");
        var objDir = Path.Combine(testDir, "obj");
        var objFile = Path.Combine(objDir, "Class.cs");
        var dllPath = Path.Combine(testDir, "Test.dll");

        Directory.CreateDirectory(objDir);

        // Create csproj and dll
        File.WriteAllText(csprojPath, "<Project></Project>");
        File.WriteAllBytes(dllPath, new byte[] { 0 });

        // Wait to ensure different timestamp
        Thread.Sleep(10);

        // Create file in obj directory (should be ignored)
        File.WriteAllText(objFile, "public class Test { }");

        // Act
        var isStale = StaleChecker.IsStale(csprojPath, dllPath, null, checkForEdit: true);

        // Assert - Should be false because obj/ is ignored
        isStale.Should().BeFalse();
    }

    [Fact]
    public void IsStale_ExcludesBinDirectory()
    {
        // Arrange
        var testDir = _fixture.GetTestDirectory();
        var csprojPath = Path.Combine(testDir, "Test.csproj");
        var binDir = Path.Combine(testDir, "bin");
        var binFile = Path.Combine(binDir, "Class.cs");
        var dllPath = Path.Combine(testDir, "Test.dll");

        Directory.CreateDirectory(binDir);

        // Create csproj and dll
        File.WriteAllText(csprojPath, "<Project></Project>");
        File.WriteAllBytes(dllPath, new byte[] { 0 });

        // Wait to ensure different timestamp
        Thread.Sleep(10);

        // Create file in bin directory (should be ignored)
        File.WriteAllText(binFile, "public class Test { }");

        // Act
        var isStale = StaleChecker.IsStale(csprojPath, dllPath, null, checkForEdit: true);

        // Assert - Should be false because bin/ is ignored
        isStale.Should().BeFalse();
    }

    #endregion

    #region PathsToCheck Tests

    [Fact]
    public void IsStale_WithPathsToCheck_OnlyChecksSpecifiedPaths()
    {
        // Arrange
        var testDir = _fixture.GetTestDirectory();
        var csprojPath = Path.Combine(testDir, "Test.csproj");
        var entitiesDir = Path.Combine(testDir, "Entities");
        var commandsDir = Path.Combine(testDir, "Commands");
        var commandsFile = Path.Combine(commandsDir, "Command.cs");
        var dllPath = Path.Combine(testDir, "Test.dll");

        Directory.CreateDirectory(entitiesDir);
        Directory.CreateDirectory(commandsDir);

        // Create csproj and dll
        File.WriteAllText(csprojPath, "<Project></Project>");
        File.WriteAllBytes(dllPath, new byte[] { 0 });

        // Wait to ensure different timestamp
        Thread.Sleep(10);

        // Create file in Commands directory (not checked)
        File.WriteAllText(commandsFile, "public class Command { }");

        // Act - Only check Entities
        var isStale = StaleChecker.IsStale(csprojPath, dllPath, new[] { "Entities" }, checkForEdit: true);

        // Assert - Should be false because Commands is not checked
        isStale.Should().BeFalse();
    }

    [Fact]
    public void IsStale_WithNonExistentPath_IgnoresPath()
    {
        // Arrange
        var testDir = _fixture.GetTestDirectory();
        var csprojPath = Path.Combine(testDir, "Test.csproj");
        var dllPath = Path.Combine(testDir, "Test.dll");

        // Create csproj and dll
        File.WriteAllText(csprojPath, "<Project></Project>");
        File.WriteAllBytes(dllPath, new byte[] { 0 });

        // Act - Check non-existent path
        var isStale = StaleChecker.IsStale(csprojPath, dllPath, new[] { "NonExistent" }, checkForEdit: true);

        // Assert - Should be false because path doesn't exist
        isStale.Should().BeFalse();
    }

    [Fact]
    public void IsStale_WithPathsToCheck_FilePathNewer_ReturnsTrue()
    {
        // Arrange
        var testDir = _fixture.GetTestDirectory();
        var csprojPath = Path.Combine(testDir, "Test.csproj");
        var dllPath = Path.Combine(testDir, "Test.dll");
        var sourceFile = Path.Combine(testDir, "Class.cs");

        File.WriteAllText(csprojPath, "<Project></Project>");
        File.WriteAllBytes(dllPath, new byte[] { 0 });

        Thread.Sleep(10);

        File.WriteAllText(sourceFile, "public class Test { }");

        // Act
        var isStale = StaleChecker.IsStale(csprojPath, dllPath, new[] { "Class.cs" }, checkForEdit: false);

        // Assert
        isStale.Should().BeTrue();
    }

    [Fact]
    public void IsStale_WithPathsToCheck_FilePathOlder_ReturnsFalse()
    {
        // Arrange
        var testDir = _fixture.GetTestDirectory();
        var csprojPath = Path.Combine(testDir, "Test.csproj");
        var dllPath = Path.Combine(testDir, "Test.dll");
        var sourceFile = Path.Combine(testDir, "Class.cs");

        File.WriteAllText(sourceFile, "public class Test { }");
        File.WriteAllText(csprojPath, "<Project></Project>");

        Thread.Sleep(10);

        File.WriteAllBytes(dllPath, new byte[] { 0 });

        // Act
        var isStale = StaleChecker.IsStale(csprojPath, dllPath, new[] { "Class.cs" }, checkForEdit: true);

        // Assert
        isStale.Should().BeFalse();
    }

    #endregion

    #region CheckForEdit Tests

    [Fact]
    public void IsStale_WithCheckForEditFalse_SkipsFileCheck()
    {
        // Arrange
        var testDir = _fixture.GetTestDirectory();
        var csprojPath = Path.Combine(testDir, "Test.csproj");
        var sourceFile = Path.Combine(testDir, "Class.cs");
        var dllPath = Path.Combine(testDir, "Test.dll");

        // Create source file first (before dll)
        File.WriteAllText(sourceFile, "public class Test { }");

        // Wait to ensure different timestamp
        Thread.Sleep(10);

        // Create csproj and dll after source file
        File.WriteAllText(csprojPath, "<Project></Project>");
        File.WriteAllBytes(dllPath, new byte[] { 0 });

        // Act - CheckForEdit is false, so file check is skipped
        var isStale = StaleChecker.IsStale(csprojPath, dllPath, null, checkForEdit: false);

        // Assert - Should be false because CheckForEdit is false (file check skipped)
        isStale.Should().BeFalse();
    }

    [Fact]
    public void IsStale_WithCheckForEditTrue_ChecksFileTimestamps()
    {
        // Arrange
        var testDir = _fixture.GetTestDirectory();
        var csprojPath = Path.Combine(testDir, "Test.csproj");
        var sourceFile = Path.Combine(testDir, "Class.cs");
        var dllPath = Path.Combine(testDir, "Test.dll");

        // Create csproj and dll
        File.WriteAllText(csprojPath, "<Project></Project>");
        File.WriteAllBytes(dllPath, new byte[] { 0 });

        // Wait to ensure different timestamp
        Thread.Sleep(10);

        // Create newer source file
        File.WriteAllText(sourceFile, "public class Test { }");

        // Act - CheckForEdit is true, so file check is performed
        var isStale = StaleChecker.IsStale(csprojPath, dllPath, null, checkForEdit: true);

        // Assert - Should be true because file is newer
        isStale.Should().BeTrue();
    }

    [Fact]
    public void IsStale_FileContentChangedButDirectoryUnchanged_WithCheckForEditTrue_ReturnsTrue()
    {
        // Arrange
        var testDir = _fixture.GetTestDirectory();
        var csprojPath = Path.Combine(testDir, "Test.csproj");
        var sourceFile = Path.Combine(testDir, "Class.cs");
        var dllPath = Path.Combine(testDir, "Test.dll");

        // Create source file
        File.WriteAllText(sourceFile, "public class Test { }");
        File.WriteAllText(csprojPath, "<Project></Project>");
        
        Thread.Sleep(10);
        
        // Create DLL after source
        File.WriteAllBytes(dllPath, new byte[] { 0 });

        Thread.Sleep(10);

        // Modify file content (updates file timestamp but not directory timestamp)
        File.WriteAllText(sourceFile, "public class Test { /* modified */ }");

        // Act - With CheckForEdit = true, should detect file change
        var isStale = StaleChecker.IsStale(csprojPath, dllPath, null, checkForEdit: true);

        // Assert
        isStale.Should().BeTrue();
    }

    [Fact]
    public void IsStale_FileContentChangedButDirectoryUnchanged_WithCheckForEditFalse_ReturnsFalse()
    {
        // Arrange
        var testDir = _fixture.GetTestDirectory();
        var csprojPath = Path.Combine(testDir, "Test.csproj");
        var sourceFile = Path.Combine(testDir, "Class.cs");
        var dllPath = Path.Combine(testDir, "Test.dll");

        // Create source file
        File.WriteAllText(sourceFile, "public class Test { }");
        File.WriteAllText(csprojPath, "<Project></Project>");
        
        Thread.Sleep(10);
        
        // Create DLL after source
        File.WriteAllBytes(dllPath, new byte[] { 0 });

        Thread.Sleep(10);

        // Modify file content only (not creating/deleting files)
        File.WriteAllText(sourceFile, "public class Test { /* modified */ }");

        // Act - With CheckForEdit = false, should NOT detect content-only change
        var isStale = StaleChecker.IsStale(csprojPath, dllPath, null, checkForEdit: false);

        // Assert - Directory timestamp unchanged, so not stale
        isStale.Should().BeFalse();
    }

    #endregion

    #region Directory Timestamp Tests

    [Fact]
    public void IsStale_WithModifiedDirectory_ReturnsTrue()
    {
        // Arrange
        var testDir = _fixture.GetTestDirectory();
        var csprojPath = Path.Combine(testDir, "Test.csproj");
        var entitiesDir = Path.Combine(testDir, "Entities");
        var dllPath = Path.Combine(testDir, "Test.dll");

        Directory.CreateDirectory(entitiesDir);

        // Create csproj and dll
        File.WriteAllText(csprojPath, "<Project></Project>");
        File.WriteAllBytes(dllPath, new byte[] { 0 });

        // Wait to ensure different timestamp
        Thread.Sleep(10);

        // Modify directory by creating a file in it (updates directory timestamp)
        var newFile = Path.Combine(entitiesDir, "Entity.cs");
        File.WriteAllText(newFile, "public class Entity { }");

        // Act
        var isStale = StaleChecker.IsStale(csprojPath, dllPath, new[] { "Entities" }, checkForEdit: false);

        // Assert - Should be true because directory timestamp changed
        isStale.Should().BeTrue();
    }

    [Fact]
    public void IsStale_WithNestedDirectoryDeletion_ReturnsTrue()
    {
        // Arrange
        var testDir = _fixture.GetTestDirectory();
        var csprojPath = Path.Combine(testDir, "Test.csproj");
        var nestedDir = Path.Combine(testDir, "Entities", "Models", "Nested");
        var nestedFile = Path.Combine(nestedDir, "NestedEntity.cs");
        var dllPath = Path.Combine(testDir, "Test.dll");

        Directory.CreateDirectory(nestedDir);
        File.WriteAllText(nestedFile, "public class NestedEntity { }");

        File.WriteAllText(csprojPath, "<Project></Project>");
        File.WriteAllBytes(dllPath, new byte[] { 0 });

        Thread.Sleep(10);

        // Delete nested file (modifies multiple directory timestamps)
        File.Delete(nestedFile);

        // Act
        var isStale = StaleChecker.IsStale(csprojPath, dllPath, new[] { "Entities" }, checkForEdit: false);

        // Assert
        isStale.Should().BeTrue();
    }

    [Fact]
    public void IsStale_WithMultipleDirectoriesModified_ReturnsTrue()
    {
        // Arrange
        var testDir = _fixture.GetTestDirectory();
        var csprojPath = Path.Combine(testDir, "Test.csproj");
        var entitiesDir = Path.Combine(testDir, "Entities");
        var commandsDir = Path.Combine(testDir, "Commands");
        var dllPath = Path.Combine(testDir, "Test.dll");

        Directory.CreateDirectory(entitiesDir);
        Directory.CreateDirectory(commandsDir);

        File.WriteAllText(csprojPath, "<Project></Project>");
        File.WriteAllBytes(dllPath, new byte[] { 0 });

        Thread.Sleep(10);

        // Modify both directories
        File.WriteAllText(Path.Combine(entitiesDir, "Entity.cs"), "public class Entity { }");
        File.WriteAllText(Path.Combine(commandsDir, "Command.cs"), "public class Command { }");

        // Act - Only check Entities
        var isStale = StaleChecker.IsStale(csprojPath, dllPath, new[] { "Entities" }, checkForEdit: false);

        // Assert
        isStale.Should().BeTrue();
    }

    [Fact]
    public void IsStale_WithEmptyDirectoryCreation_ReturnsTrue()
    {
        // Arrange
        var testDir = _fixture.GetTestDirectory();
        var csprojPath = Path.Combine(testDir, "Test.csproj");
        var entitiesDir = Path.Combine(testDir, "Entities");
        var emptySubDir = Path.Combine(entitiesDir, "Empty");
        var dllPath = Path.Combine(testDir, "Test.dll");

        Directory.CreateDirectory(entitiesDir);

        File.WriteAllText(csprojPath, "<Project></Project>");
        File.WriteAllBytes(dllPath, new byte[] { 0 });

        Thread.Sleep(10);

        // Create empty subdirectory (still modifies directory timestamp)
        Directory.CreateDirectory(emptySubDir);

        // Act
        var isStale = StaleChecker.IsStale(csprojPath, dllPath, new[] { "Entities" }, checkForEdit: false);

        // Assert
        isStale.Should().BeTrue();
    }

    [Fact]
    public void IsStale_WithRapidConsecutiveDirectoryChanges_DetectsAll()
    {
        // Arrange
        var testDir = _fixture.GetTestDirectory();
        var csprojPath = Path.Combine(testDir, "Test.csproj");
        var entitiesDir = Path.Combine(testDir, "Entities");
        var dllPath = Path.Combine(testDir, "Test.dll");

        Directory.CreateDirectory(entitiesDir);

        File.WriteAllText(csprojPath, "<Project></Project>");
        File.WriteAllBytes(dllPath, new byte[] { 0 });

        Thread.Sleep(10);

        // Rapid changes
        File.WriteAllText(Path.Combine(entitiesDir, "Entity1.cs"), "public class Entity1 { }");
        File.WriteAllText(Path.Combine(entitiesDir, "Entity2.cs"), "public class Entity2 { }");
        File.WriteAllText(Path.Combine(entitiesDir, "Entity3.cs"), "public class Entity3 { }");

        // Act
        var isStale = StaleChecker.IsStale(csprojPath, dllPath, new[] { "Entities" }, checkForEdit: false);

        // Assert
        isStale.Should().BeTrue();
    }

    [Fact]
    public void IsStale_WithLargeDirectoryStructure_DetectsChanges()
    {
        // Arrange
        var testDir = _fixture.GetTestDirectory();
        var csprojPath = Path.Combine(testDir, "Test.csproj");
        var entitiesDir = Path.Combine(testDir, "Entities");
        var dllPath = Path.Combine(testDir, "Test.dll");

        Directory.CreateDirectory(entitiesDir);

        // Create many subdirectories
        for (int i = 0; i < 10; i++)
        {
            Directory.CreateDirectory(Path.Combine(entitiesDir, $"Group{i}"));
        }

        File.WriteAllText(csprojPath, "<Project></Project>");
        File.WriteAllBytes(dllPath, new byte[] { 0 });

        Thread.Sleep(10);

        // Add file to deep structure
        var deepFile = Path.Combine(entitiesDir, "Group9", "Entity.cs");
        File.WriteAllText(deepFile, "public class Entity { }");

        // Act
        var isStale = StaleChecker.IsStale(csprojPath, dllPath, new[] { "Entities" }, checkForEdit: false);

        // Assert
        isStale.Should().BeTrue();
    }

    [Fact]
    public void IsStale_WithDirectoryRename_DetectsAsModification()
    {
        // Arrange
        var testDir = _fixture.GetTestDirectory();
        var csprojPath = Path.Combine(testDir, "Test.csproj");
        var entitiesDir = Path.Combine(testDir, "Entities");
        var dllPath = Path.Combine(testDir, "Test.dll");

        Directory.CreateDirectory(entitiesDir);
        var oldName = Path.Combine(entitiesDir, "OldName.cs");
        File.WriteAllText(oldName, "public class Old { }");

        File.WriteAllText(csprojPath, "<Project></Project>");
        File.WriteAllBytes(dllPath, new byte[] { 0 });

        Thread.Sleep(10);

        // Rename file (modifies directory timestamp)
        var newName = Path.Combine(entitiesDir, "NewName.cs");
        File.Move(oldName, newName);

        // Act
        var isStale = StaleChecker.IsStale(csprojPath, dllPath, new[] { "Entities" }, checkForEdit: false);

        // Assert
        isStale.Should().BeTrue();
    }

    [Fact]
    public void IsStale_WithUnchangedDirectoryTimestamp_ReturnsFalse()
    {
        // Arrange
        var testDir = _fixture.GetTestDirectory();
        var csprojPath = Path.Combine(testDir, "Test.csproj");
        var entitiesDir = Path.Combine(testDir, "Entities");
        var dllPath = Path.Combine(testDir, "Test.dll");

        Directory.CreateDirectory(entitiesDir);

        // Create a file BEFORE dll
        var entityFile = Path.Combine(entitiesDir, "Entity.cs");
        File.WriteAllText(entityFile, "public class Entity { }");

        Thread.Sleep(10);

        // Create csproj and dll AFTER directory was created
        File.WriteAllText(csprojPath, "<Project></Project>");
        File.WriteAllBytes(dllPath, new byte[] { 0 });

        // Act - Directory wasn't modified after DLL
        var isStale = StaleChecker.IsStale(csprojPath, dllPath, new[] { "Entities" }, checkForEdit: false);

        // Assert
        isStale.Should().BeFalse();
    }

    [Fact]
    public void IsStale_DirectoryTimestamp_With_CheckForEditFalse_TriggersRebuild()
    {
        // Arrange
        var testDir = _fixture.GetTestDirectory();
        var csprojPath = Path.Combine(testDir, "Test.csproj");
        var entitiesDir = Path.Combine(testDir, "Entities");
        var dllPath = Path.Combine(testDir, "Test.dll");

        Directory.CreateDirectory(entitiesDir);

        File.WriteAllText(csprojPath, "<Project></Project>");
        File.WriteAllBytes(dllPath, new byte[] { 0 });

        Thread.Sleep(10);

        // Create new file (modifies directory)
        File.WriteAllText(Path.Combine(entitiesDir, "Entity.cs"), "public class Entity { }");

        // Act - Even with CheckForEdit=false, directory changes detected
        var isStale = StaleChecker.IsStale(csprojPath, dllPath, new[] { "Entities" }, checkForEdit: false);

        // Assert - Directory timestamp changes are checked regardless of CheckForEdit
        isStale.Should().BeTrue();
    }

    #endregion
}
