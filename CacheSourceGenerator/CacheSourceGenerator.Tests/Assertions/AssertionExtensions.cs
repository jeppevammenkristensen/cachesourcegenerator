using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CacheSourceGenerator.Tests.Assertions;

public static class AssertionExtensions
{
    public static GeneratedResultAssertions Should(this GeneratorDriverRunResult source)
    {
        return new GeneratedResultAssertions(source);
    }

    public static SyntaxTreeAssertion Should(this SyntaxTree source)
    {
        return new SyntaxTreeAssertion(source);
    }


    public static ClassSyntaxAssertion Should(this ClassDeclarationSyntax source)
    {
        return new ClassSyntaxAssertion(source, source.Ancestors().OfType<CompilationUnitSyntax>().First());
    }

    public static MethodSyntaxAssertion Should(this MethodDeclarationSyntax source)
    {   
        return new MethodSyntaxAssertion(source);
    }
}