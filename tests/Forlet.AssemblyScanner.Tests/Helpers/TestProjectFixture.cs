using System;
using System.IO;
using Xunit;

namespace Forlet.AssemblyScanner.Tests.Helpers;

/// <summary>
/// A shared fixture for managing temporary directories for test projects.
/// Creates a root temp directory for the test session.
/// Each test should call GetTestDirectory() to get its own isolated subdirectory.
/// </summary>
public sealed class TestProjectFixture : IDisposable
{
    private readonly string _rootTempDirectory;

    /// <summary>
    /// Gets the root temporary directory path for this test session.
    /// </summary>
    public string RootTempDirectory
    {
        get { return _rootTempDirectory; }
    }

    /// <summary>
    /// Creates a new test project fixture with a unique temporary directory.
    /// </summary>
    public TestProjectFixture()
    {
        _rootTempDirectory = Path.Combine(Path.GetTempPath(), $"forlet_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_rootTempDirectory);
    }

    /// <summary>
    /// Gets a fresh, isolated temporary directory for a specific test.
    /// Each test should call this to ensure it doesn't share directories with other tests.
    /// </summary>
    public string GetTestDirectory()
    {
        var testDir = Path.Combine(_rootTempDirectory, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testDir);
        return testDir;
    }

    /// <summary>
    /// Cleans up the temporary directory and all its contents.
    /// </summary>
    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_rootTempDirectory))
            {
                Directory.Delete(_rootTempDirectory, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
}

/// <summary>
/// xUnit collection definition for tests using TestProjectFixture.
/// </summary>
[CollectionDefinition("TestProjects")]
public class TestProjectCollection : ICollectionFixture<TestProjectFixture>
{
}
