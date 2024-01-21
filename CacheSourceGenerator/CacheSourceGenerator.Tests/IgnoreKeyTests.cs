using System.Linq;
using CacheSourceGenerator.Tests.Assertions;
using CacheSourceGenerator.Tests.Utils;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;
using CSharpExtensions = Microsoft.CodeAnalysis.CSharpExtensions;

namespace CacheSourceGenerator.Tests;

public class IgnoreKeyTests : SourceGeneratorTests
{
    [Fact]
    public void MethodParametersDecoratedWithIgnoreKeyAreNotIncluded()
    {
        var str = """
                  using Microsoft.Extensions.Caching.Memory;
                  namespace TestNamespace;

                  public partial class TestClass
                  {
                       private readonly IMemoryCache _cache;
                  
                       public TestClass(IMemoryCache cache)
                       {
                           _cache = cache;
                       }
                       
                       [CacheSourceGenerator.GenerateMemoryCache(MethodName = "CalculateAge"]
                       public int DoCalculateAge(int id, [CacheSourceGenerator.IgnoreKey]int ignored)
                       {
                           return id;
                       }
                       
                       public object KeyGenerator()
                       {
                            return null;
                       }
                  }
                  """;

        var result = Generator.GenerateResult(str);
        var generatedMethod = AssertAndRetrieveGeneratedMethod(result, "TestClass", "CalculateAge");
        var body = generatedMethod.Should().MethodBody;
        var cacheDeclaration = body.DescendantNodes().Where(x => CSharpExtensions.IsKind((SyntaxNode?) x, SyntaxKind.LocalDeclarationStatement))
            .OfType<LocalDeclarationStatementSyntax>().FirstOrDefault(x => x.Declaration.Variables[0].Identifier.ToString() == "_key_");

        cacheDeclaration.Should().NotBeNull();
        cacheDeclaration!.Declaration.Variables[0].Initializer.Value.Should()
            .BeOfType<AnonymousObjectCreationExpressionSyntax>()
            .Which.Initializers
            .Where(x => x.Expression is IdentifierNameSyntax ins && ins.Identifier.ToString() == "ignored").Should()
            .BeEmpty("Because key intializer should not contain ignored");
    }
    
    [Fact]
    public void IgnoreAttributesOnMethodsAreNotIncludedInOutput()
    {
        var str = """
                  using Microsoft.Extensions.Caching.Memory;
                  namespace TestNamespace;

                  public partial class TestClass
                  {
                       private readonly IMemoryCache _cache;
                  
                       public TestClass(IMemoryCache cache)
                       {
                           _cache = cache;
                       }
                       
                       [CacheSourceGenerator.GenerateMemoryCache(MethodName = "CalculateAge"]
                       public int DoCalculateAge(int id, [CacheSourceGenerator.IgnoreKey]int ignored)
                       {
                           return id;
                       }
                       
                       public object KeyGenerator()
                       {
                            return null;
                       }
                  }
                  """;

        var result = Generator.GenerateResult(str);
        var generatedClass = AssertAndRetrieveClass(result, "TestClass");

        generatedClass.ToString().Should().NotContain("IgnoreKey");


    }
}