using CacheSourceGenerator.Tests.Assertions;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CacheSourceGenerator.Tests;

public abstract class SourceGeneratorTests
{
    /// <summary>
    /// Evaluates the generated method given the generator driver run result, class name, and method name.
    /// </summary>
    /// <param name="result">The generator driver run result.</param>
    /// <param name="className">The name of the class.</param>
    /// <param name="methodName">The name of the method.</param>
    /// <returns>The MethodDeclarationSyntax object representing the generated method.</returns>
    protected MethodDeclarationSyntax AssertAndRetrieveGeneratedMethod(GeneratorDriverRunResult result, string className,
        string methodName)
    {
        result.Diagnostics.Should().NotContain(x => x.Severity == DiagnosticSeverity.Error);
        return AssertAndRetrieveClass(result, className).Should().ContainMethod(methodName).Which;
    }

    protected ClassDeclarationSyntax AssertAndRetrieveClass(GeneratorDriverRunResult result,string className)
    {
        return result.Should().ContainFile($"{className}.g.cs").Which.Should()
            .ContainClass(className).Which;
    }
}