using CacheSourceGenerator.Tests.Assertions;
using CacheSourceGenerator.Tests.Utils;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Xunit;

namespace CacheSourceGenerator.Tests;

public class KeyGeneratorTests : SourceGeneratorTests
{
    [Fact]
    public void MethodDecoratedWithValidKeyGeneratorWithNoParametersGeneratedCorrectOutput()
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
                       
                       [CacheSourceGenerator.GenerateMemoryCache(MethodName = "CalculateAge", KeyGenerator=nameof(KeyGenerator)]
                       public int DoCalculateAge()
                       {
                           return 42;
                       }
                       
                       public object KeyGenerator()
                       {
                            return null;
                       }
                  }
                  """;
        var result = Generator.GenerateResult(str);
        var generatedMethod = AssertAndRetrieveGeneratedMethod(result,"TestClass", "CalculateAge");
        generatedMethod.Should().HaveBodyContaining("_key_ = KeyGenerator()");
    }
    
    [Fact]
    public void MethodDecoratedWithValidKeyGeneratorWithMultipleParametersGeneratedCorrectOutput()
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
                       
                       [CacheSourceGenerator.GenerateMemoryCache(MethodName = "CalculateAge", KeyGenerator=nameof(KeyGenerator)]
                       public int DoCalculateAge(int intValue, bool boolValue, string strValue)
                       {
                           return 42;
                       }
                       
                       // the parameters does not have to have the same name
                       public object KeyGenerator(int first, bool second, string? strValue)
                       {
                            return null;
                       }
                  }
                  """;
        var result = Generator.GenerateResult(str);
        var generatedMethod = AssertAndRetrieveGeneratedMethod(result,"TestClass", "CalculateAge");
        generatedMethod.Should().HaveBodyContaining("_key_ = KeyGenerator(intValue, boolValue, strValue)");
    }
    
    [Fact]
    public void AsyncMethodDecoratedWithValidAsyncKeyGeneratorWithMultipleParametersGeneratedCorrectOutput()
    {
        var str = """
                  using Microsoft.Extensions.Caching.Memory;
                  using System.Threading.Tasks;
                  
                  namespace TestNamespace;
                  public partial class TestClass
                  {
                       private readonly IMemoryCache _cache;
                  
                       public TestClass(IMemoryCache cache)
                       {
                           _cache = cache;
                       }
                       
                       [CacheSourceGenerator.GenerateMemoryCache(MethodName = "CalculateAge", KeyGenerator=nameof(KeyGenerator)]
                       public async Task<int> DoCalculateAge(int intValue, bool boolValue, string strValue)
                       {
                           return 42;
                       }
                       
                       // the parameters does not have to have the same name
                       public async Task<object> KeyGenerator(int first, bool second, string? strValue)
                       {
                            return null;
                       }
                  }
                  """;
        var result = Generator.GenerateResult(str);
        var generatedMethod = AssertAndRetrieveGeneratedMethod(result,"TestClass", "CalculateAge");
        generatedMethod.Should().HaveBodyContaining("_key_ = await KeyGenerator(intValue, boolValue, strValue)");
    }
    
    [Fact]
    public void MethodDecoratedWithValidAsyncKeyGeneratorWithMultipleParametersGeneratedCorrectOutput()
    {
        var str = """
                  using Microsoft.Extensions.Caching.Memory;
                  using System.Threading.Tasks;

                  namespace TestNamespace;
                  public partial class TestClass
                  {
                       private readonly IMemoryCache _cache;
                  
                       public TestClass(IMemoryCache cache)
                       {
                           _cache = cache;
                       }
                       
                       [CacheSourceGenerator.GenerateMemoryCache(MethodName = "CalculateAge", KeyGenerator=nameof(KeyGenerator)]
                       public int DoCalculateAge(int intValue, bool boolValue, string strValue)
                       {
                           return 42;
                       }
                       
                       // the parameters does not have to have the same name
                       public async Task<object> KeyGenerator(int first, bool second, string? strValue)
                       {
                            return null;
                       }
                  }
                  """;
        var result = Generator.GenerateResult(str);
        var generatedMethod = AssertAndRetrieveGeneratedMethod(result,"TestClass", "CalculateAge");
        generatedMethod.Should().HaveBodyContaining("_key_ = KeyGenerator(intValue, boolValue, strValue).GetAwaiter().GetResult()");
    }
    
    [Fact]
    public void MethodDecoratedWithKeyGeneratorWithParameterMismatch()
    {
        var str = """
                  using Microsoft.Extensions.Caching.Memory;
                  using System.Threading.Tasks;

                  namespace TestNamespace;
                  public partial class TestClass
                  {
                       private readonly IMemoryCache _cache;
                  
                       public TestClass(IMemoryCache cache)
                       {
                           _cache = cache;
                       }
                       
                       [CacheSourceGenerator.GenerateMemoryCache(MethodName = "CalculateAge", KeyGenerator=nameof(KeyGenerator)]
                       public int DoCalculateAge(int intValue)
                       {
                           return 42;
                       }
                       
                       // the parameters does not have to have the same name
                       public async Task<object> KeyGenerator(string wrongValue)
                       {
                            return null;
                       }
                  }
                  """;
        var result = Generator.GenerateResult(str);
        result.Diagnostics.Should().Contain(x =>
            x.Severity == DiagnosticSeverity.Error && x.Id == DiagnosticIds.Id_006_KeyGeneratorNoParameterMatch);

    }
}