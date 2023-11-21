using System;
using System.Runtime.CompilerServices;
using CacheSourceGenerator.Tests.Assertions;
using CacheSourceGenerator.Tests.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.Caching.Memory;
using Xunit;
using Xunit.Abstractions;

namespace CacheSourceGenerator.Tests;

public class SampleIncrementalSourceGeneratorTests
{
    private readonly ITestOutputHelper _testOutputHelper;

    public SampleIncrementalSourceGeneratorTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    [Fact]
    public void ClassWithInjectedMemoryCacheUsesIt()
    {
        var code = """
                   using Microsoft.Extensions.Caching.Memory;
                   namespace TestNamespace;

                   public partial class TestClass
                   {
                        private readonly IMemoryCache _cache;
                   
                        public TestClass(IMemoryCache cache)
                        {
                            _cache = cache;
                        }
                        
                        [CacheSourceGenerator.Cacho(MethodName = "CacheName")]
                        public int CalculateAge()
                        {
                            return 42;
                        }
                   }
                   """;

        var syntaxTree = Generator.GenerateResult(code).Should().ContainFile("TestClass.g.cs").Which;
        
        syntaxTree.Should().ContainClass("TestClass").Which
            .Should().BePartial().And.BePublic().And
            .ContainMethod("CacheName").Which.Should()
            .HaveReturnType("int").And
            .HaveParameters(Array.Empty<(string type, string name)>()).And
            .HaveBodyContaining("IMemoryCache cache = _cache;");
        

        syntaxTree.Should().NotContainClass("CacheInit");
    }
    
    [Fact]
    public void ClassDecoratedWithAttributeAndNoInjectedIMemoryCacheGeneratesExpectedOutput()
    {
        var runResult = Generator.GenerateResult("""
                             namespace TestNamespace;

                             public partial class Vector3
                             {
                                 [CacheSourceGenerator.Cacho(MethodName = "TheName")]
                                 public string GetName(int number, string hector)
                                 {
                                     return string.Empty();
                                 }
                             }
                             """);

        var syntaxTree = runResult.Should()
            .ContainFile("Vector3.g.cs").Which;
        _testOutputHelper.WriteLine(syntaxTree.ToString());
        
        syntaxTree.Should()
            .ContainUsing("Microsoft.Extensions.Caching.Memory").And
            .ContainNamespace("TestNamespace").And
            .ContainClass("Vector3").Which
            .Should().BePublic().And.BePartial().And
            .ContainMethod("TheName").Which.Should()
            .HaveReturnType("string").And.HaveParameters(new (string type, string name)[] {("int", "number"), ("string","hector")}).And
            .HaveBodyContaining("IMemoryCache cache = CacheInit.MemoryCache");

        syntaxTree.Should()
            .ContainClass("CacheInit").Which
            .Should().BeStatic();
    }
    
   
}