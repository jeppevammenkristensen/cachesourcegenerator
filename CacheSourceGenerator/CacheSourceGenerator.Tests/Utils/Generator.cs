using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.Caching.Memory;

namespace CacheSourceGenerator.Tests.Utils;

public static class Generator
{
    public static GeneratorDriverRunResult GenerateResult(string input)
    {
        // Create an instance of the source generator.
        var generator = new CachoIncrementalSourceGenerator();

        // Source generators should be tested using 'GeneratorDriver'.
        var driver = CSharpGeneratorDriver.Create(generator);

        // We need to create a compilation with the required source code.
        var compilation = CSharpCompilation.Create(nameof(CachoIncrementalSourceGenerator),
            new[] { CSharpSyntaxTree.ParseText(input) },
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