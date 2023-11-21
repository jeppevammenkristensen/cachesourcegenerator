using System;
using System.Runtime.CompilerServices;
using CacheSourceGenerator.Tests.Assertions;
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

        var syntaxTree = GenerateReportMethod(code).Should().ContainFile("TestClass.g.cs").Which;
        
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
        var runResult = GenerateReportMethod("""
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
    
     public GeneratorDriverRunResult GenerateReportMethod(string inputText)
    {
        // Create an instance of the source generator.
        var generator = new CachoIncrementalSourceGenerator();

        // Source generators should be tested using 'GeneratorDriver'.
        var driver = CSharpGeneratorDriver.Create(generator);

        // We need to create a compilation with the required source code.
        var compilation = CSharpCompilation.Create(nameof(CachoIncrementalSourceGenerator),
            new[] { CSharpSyntaxTree.ParseText(inputText) },
            new[]
            {
                // To support 'System.Attribute' inheritance, add reference to 'System.Private.CoreLib'.
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(IMemoryCache).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(MemoryCache).Assembly.Location)
            });

        // Run generators and retrieve all results.
        return driver.RunGenerators(compilation).GetRunResult();
        
        //ExpectedGeneratedClassText.Should().BeEquivalentTo(syntaxTree.GetText().ToString());
    }
}