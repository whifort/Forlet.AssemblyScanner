# Forlet.AssemblyScanner Tests

Comprehensive test suite for the Forlet.AssemblyScanner package.

## Test Structure

```
tests/Forlet.AssemblyScanner.Tests/
├── Helpers/
│   ├── TestProjectBuilder.cs      # Fluent builder for creating test .NET projects
│   └── TestProjectFixture.cs      # xUnit fixture for temp directory lifecycle
├── MetadataScannerTests.cs        # Core scanning functionality tests
├── ProjectDllResolverTests.cs     # DLL resolution and build strategy tests
├── StaleCheckerTests.cs           # Staleness detection tests
├── ScanOptionsTests.cs            # Scan configuration option tests
└── IntegrationTests.cs            # End-to-end workflow tests
```

## Test Categories

**Total Test Count: 102 tests**

| Test Class | Count |
|-----------|-------|
| MetadataScannerTests | 33 |
| ProjectDllResolverTests | 32 |
| StaleCheckerTests | 24 |
| ScanOptionsTests | 6 |
| IntegrationTests | 7 |
| **Total** | **102** |

### MetadataScannerTests (33 tests)

Tests for the core `MetadataScanner` class:

| Category | Tests | Description |
|----------|-------|-------------|
| Constructor | 2 | Valid/invalid DLL loading |
| Assembly Resolution | 1 | Target runtime selection from deps.json |
| FindTypesImplementing | 8 | Interface implementation scanning |
| FindTypesDerivedFrom | 4 | Base class inheritance scanning |
| FindTypeByName | 4 | Single type lookup with early exit |
| Input Validation | 11 | Null/empty argument handling |
| Generic Types | 6 | Generic interface/class support |
| Disposal | 1 | Resource cleanup verification |

### ProjectDllResolverTests (32 tests)

Tests for `ProjectDllResolver` and build management:

| Category | Tests | Description |
|----------|-------|-------------|
| Input Validation | 5 | Path validation and error handling |
| Path Resolution | 1 | Relative to absolute path conversion |
| Target Framework | 3 | Single and multi-target framework handling |
| Build Strategies | 6 | NoBuild, AutoBuild, AlwaysBuild |
| OnBuildStart Callback | 5 | Callback invocation scenarios |
| CheckForEdit | 5 | File vs directory timestamp checking |
| Build Results | 4 | BuiltAutomatically and BuildOutput |

### StaleCheckerTests (24 tests)

Tests for the internal `StaleChecker` class:

| Category | Tests | Description |
|----------|-------|-------------|
| DLL Existence | 1 | Missing DLL detection |
| Csproj Modification | 2 | Project file change detection |
| Source File Changes | 4 | .cs file modification detection |
| PathsToCheck | 4 | Selective path scanning |
| CheckForEdit | 4 | File enumeration control |
| Directory Timestamps | 9 | Deletion, creation, rename detection |

### ScanOptionsTests (6 tests)

Tests for `ScanOptions` configuration:

| Test | Description |
|------|-------------|
| MatchFullName = true | Full namespace matching required |
| MatchFullName = false | Simple name matching (default) |
| IncludeStructs = true | Struct implementations included |
| IncludeNestedTypes = true | Nested implementations included |
| Combined options | Multiple options work together |
| Default values | Verifies default behavior |

### IntegrationTests (7 tests)

End-to-end workflow tests:

| Test | Description |
|------|-------------|
| Full workflow | Build → Scan → Find types |
| Source modification | Change detection triggers rebuild |
| Multiple interfaces | Complex project structures |
| Callbacks | Build process tracking |
| PathsToCheck | Partial project scanning |
| Invalid project path | Error handling for missing paths |
| Multiple build attempts | Consistency across builds |

## Running Tests

```bash
# Run all tests
dotnet test

# Run with verbose output
dotnet test --verbosity detailed

# Run specific test class
dotnet test --filter "FullyQualifiedName~MetadataScannerTests"

# Run specific test
dotnet test --filter "FullyQualifiedName~FindTypesImplementing_WithSingleInterface"
```

## Test Helpers

### TestProjectBuilder

Fluent API for creating temporary .NET projects:

```csharp
var builder = new TestProjectBuilder("MyProject")
    .WithTargetFramework("net8.0")
    .AddInterface("ICommand")
    .AddInterface("IQuery")
    .AddClass("CreateCommand", implements: "ICommand")
    .AddClass("GetQuery", implements: "IQuery")
    .AddClass("BaseEntity")
    .AddClass("User", inherits: "BaseEntity")
    .AddClass("AbstractHandler", implements: "ICommand", isAbstract: true)
    .AddClass("InternalService", implements: "ICommand", isInternal: true)
    .AddGenericInterface("IHandler", typeParamCount: 1)
    .AddGenericClass("Handler", typeParamCount: 1, implements: "IHandler<T1>")
    .AddNestedClass("Container", "NestedClass", implements: "ICommand");

var csprojPath = builder.Build(tempDirectory);
```

### TestProjectFixture

xUnit collection fixture for managing temporary directories:

```csharp
[Collection("TestProjects")]
public class MyTests
{
    private readonly TestProjectFixture _fixture;

    public MyTests(TestProjectFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task MyTest()
    {
        var testDir = _fixture.GetTestDirectory(); // Isolated per test
        // ...
    }
}
```

## Test Coverage Summary

| Feature | Covered |
|---------|---------|
| Interface scanning | ✅ |
| Base class scanning | ✅ |
| Generic types (backtick notation) | ✅ |
| Generic types (simple name matching) | ✅ |
| Generic types (full name matching) | ✅ |
| MatchFullName option | ✅ |
| IncludeAbstract option | ✅ |
| IncludeNonPublic option | ✅ |
| Nested class exclusion | ✅ |
| Multiple interfaces on same class | ✅ |
| Auto-build on stale | ✅ |
| NoBuild strategy | ✅ |
| AlwaysBuild strategy | ✅ |
| PathsToCheck optimization | ✅ |
| CheckForEdit option | ✅ |
| OnBuildStart callback | ✅ |
| Directory timestamp detection | ✅ |
| File timestamp detection | ✅ |
| File content change detection | ✅ |
| obj/bin exclusion | ✅ |
| Multi-target framework | ✅ |
| BuildOutput verification | ✅ |
| Input validation | ✅ |
| Error handling | ✅ |
| Resource disposal | ✅ |

## Writing New Tests

When adding new functionality:

1. **Unit tests** go in the appropriate test class (e.g., `MetadataScannerTests` for scanning features)
2. **Use TestProjectBuilder** to create realistic test projects
3. **Use descriptive test names**: `MethodName_Scenario_ExpectedResult`
4. **Use FluentAssertions** for readable assertions
5. **Include both positive and negative test cases**
6. **Add integration test** if the feature involves multiple components

### Test Naming Convention

```
{MethodUnderTest}_{Scenario}_{ExpectedBehavior}
```

Examples:
- `FindTypesImplementing_WithSingleInterface_FindsImplementors`
- `PrepareAssemblyAsync_WithNoBuild_AndMissingDll_ThrowsScanException`
- `IsStale_WithNewerSourceFile_ReturnsTrue`

## Performance

Approximate test execution times:
- Full suite: ~45-60 seconds
- MetadataScannerTests: ~20 seconds
- ProjectDllResolverTests: ~15 seconds
- StaleCheckerTests: ~5 seconds
- ScanOptionsTests: ~5 seconds
- IntegrationTests: ~10 seconds

*Note: Times vary based on system performance and disk speed.*
