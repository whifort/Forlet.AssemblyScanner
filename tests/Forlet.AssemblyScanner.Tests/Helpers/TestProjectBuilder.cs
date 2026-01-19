using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Forlet.AssemblyScanner.Tests.Helpers;

/// <summary>
/// A fluent builder for creating temporary .NET projects with specific types and interfaces.
/// </summary>
public sealed class TestProjectBuilder
{
    private readonly string _projectName;
    private readonly List<(string FileName, string Content)> _files = new();
    private string _targetFramework = "net8.0";

    /// <summary>
    /// Creates a new test project builder.
    /// </summary>
    /// <param name="projectName">Name of the project (used as assembly name)</param>
    public TestProjectBuilder(string projectName)
    {
        _projectName = projectName;
    }

    /// <summary>
    /// Sets the target framework for the project.
    /// </summary>
    public TestProjectBuilder WithTargetFramework(string framework)
    {
        _targetFramework = framework;
        return this;
    }

    /// <summary>
    /// Adds a simple interface to the project.
    /// </summary>
    public TestProjectBuilder AddInterface(string name, string? namespaceName = null)
    {
        var ns = namespaceName ?? "TestProject";
        var code = $@"namespace {ns};

public interface {name}
{{
}}
";
        _files.Add(($"{name}.cs", code));
        return this;
    }

    /// <summary>
    /// Adds a generic interface to the project.
    /// </summary>
    public TestProjectBuilder AddGenericInterface(string name, int typeParamCount)
    {
        if (typeParamCount <= 0)
            throw new ArgumentException("Type parameter count must be greater than 0", nameof(typeParamCount));

        var typeParams = string.Join(", ", Enumerable.Range(1, typeParamCount).Select(i => $"T{i}"));
        var code = $@"namespace TestProject;

public interface {name}<{typeParams}>
{{
}}
";
        _files.Add(($"{name}.cs", code));
        return this;
    }

    /// <summary>
    /// Adds a class to the project.
    /// </summary>
    public TestProjectBuilder AddClass(string name, string? implements = null, string? inherits = null, bool isAbstract = false, bool isInternal = false)
    {
        var access = isInternal ? "internal" : "public";
        var abstractKeyword = isAbstract ? "abstract " : "";
        var inheritance = "";

        if (inherits != null && implements != null)
        {
            inheritance = $" : {inherits}, {implements}";
        }
        else if (inherits != null)
        {
            inheritance = $" : {inherits}";
        }
        else if (implements != null)
        {
            inheritance = $" : {implements}";
        }

        var code = $@"namespace TestProject;

{access} {abstractKeyword}class {name}{inheritance}
{{
}}
";

        _files.Add(($"{name}.cs", code));
        return this;
    }

    /// <summary>
    /// Adds a struct to the project.
    /// </summary>
    public TestProjectBuilder AddStruct(string name, string? implements = null, bool isInternal = false)
    {
        var access = isInternal ? "internal" : "public";
        var inheritance = implements != null ? $" : {implements}" : "";
        var code = $@"namespace TestProject;

{access} struct {name}{inheritance}
{{
}}
";
        _files.Add(($"{name}.cs", code));
        return this;
    }

    /// <summary>
    /// Adds a generic class to the project.
    /// </summary>
    public TestProjectBuilder AddGenericClass(string name, int typeParamCount, string? implements = null)
    {
        if (typeParamCount <= 0)
            throw new ArgumentException("Type parameter count must be greater than 0", nameof(typeParamCount));

        var typeParams = string.Join(", ", Enumerable.Range(1, typeParamCount).Select(i => $"T{i}"));
        var inheritance = implements != null ? $" : {implements}" : "";

        var code = $@"namespace TestProject;

public class {name}<{typeParams}>{inheritance}
{{
}}
";
        _files.Add(($"{name}.cs", code));
        return this;
    }

    /// <summary>
    /// Adds a nested class (inner class) to the project.
    /// </summary>
    public TestProjectBuilder AddNestedClass(string parentClassName, string nestedClassName, string? implements = null)
    {
        var inheritance = implements != null ? $" : {implements}" : "";
        var code = $@"namespace TestProject;

public class {parentClassName}
{{
    public class {nestedClassName}{inheritance}
    {{
    }}
}}
";
        _files.Add(($"{parentClassName}.cs", code));
        return this;
    }

    /// <summary>
    /// Builds the project and returns the path to the .csproj file.
    /// </summary>
    public string Build(string outputDirectory)
    {
        var projectDir = Path.Combine(outputDirectory, _projectName);
        Directory.CreateDirectory(projectDir);

        // Create .csproj file
        var csprojContent = $@"<Project Sdk=""Microsoft.NET.Sdk"">
    <PropertyGroup>
        <TargetFramework>{_targetFramework}</TargetFramework>
        <LangVersion>latest</LangVersion>
        <Nullable>enable</Nullable>
        <ImplicitUsings>disable</ImplicitUsings>
    </PropertyGroup>
</Project>
";
        var csprojPath = Path.Combine(projectDir, $"{_projectName}.csproj");
        File.WriteAllText(csprojPath, csprojContent);

        // Create source files - properly separated filename and content
        foreach (var (fileName, content) in _files)
        {
            var filePath = Path.Combine(projectDir, fileName);
            var dirPath = Path.GetDirectoryName(filePath);

            if (dirPath != null && !Directory.Exists(dirPath))
            {
                Directory.CreateDirectory(dirPath);
            }

            File.WriteAllText(filePath, content);
        }

        return csprojPath;
    }
}
