using CacheSourceGenerator.Tests.Assertions;
using CacheSourceGenerator.Tests.Utils;
using FluentAssertions;
using Xunit;

namespace CacheSourceGenerator.Tests;

public class CacheEntryProcessingTests
{
    [Fact]
    public void DecoratedMethodWithValidCacheEnricherProcessorGeneratesExpectedCode()
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
                       
                       [CacheSourceGenerator.Cacho(MethodName = "CacheName", CacheEnricherProcessor = nameof(CacheEntryProcessor)]
                       public int CalculateAge()
                       {
                           return 42;
                       }
                       
                       public void CacheEntryProcessor(ICacheEntry entry)
                       {
                            //
                       }
                  }
                  """;
        var classDeclarationSyntax = Generator.GenerateResult(str).Should().ContainFile("TestClass.g.cs").Which.Should().ContainClass("TestClass").Which;
        classDeclarationSyntax.Should().ContainMethod("CacheName").Which.Should().HaveBodyContaining("CacheEntryProcessor(_entry_);");
    }
    
    [Fact]
    public void DecoratedMethodWithValidAsyncCacheEnricherProcessorGeneratesExpectedCode()
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
                       
                       [CacheSourceGenerator.Cacho(MethodName = "CacheName", CacheEnricherProcessor = nameof(CacheEntryProcessor)]
                       public int CalculateAge()
                       {
                           return 42;
                       }
                       
                       public async Task CacheEntryProcessor(ICacheEntry entry)
                       {
                            //
                       }
                  }
                  """;
        var classDeclarationSyntax = Generator.GenerateResult(str).Should()
            .ContainFile("TestClass.g.cs").Which.Should()
            .ContainClass("TestClass").Which;
        
        classDeclarationSyntax.Should()
            .ContainMethod("CacheName").Which.Should()
                .HaveBodyContaining("CacheEntryProcessor(_entry_).GetAwaiter().GetResult();");
    }
    
    [Fact]
    public void DecoratedAsyncMethodWithValidAsyncCacheEnricherProcessorGeneratesExpectedCode()
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
                       
                       [CacheSourceGenerator.Cacho(MethodName = "CacheName", CacheEnricherProcessor = nameof(CacheEntryProcessor)]
                       public async Task<int> CalculateAge()
                       {
                           return 42;
                       }
                       
                       public async Task CacheEntryProcessor(ICacheEntry entry)
                       {
                            //
                       }
                  }
                  """;
        var classDeclarationSyntax = Generator.GenerateResult(str).Should()
            .ContainFile("TestClass.g.cs").Which.Should()
            .ContainClass("TestClass").Which;
        
        classDeclarationSyntax.Should()
            .ContainMethod("CacheName").Which.Should()
            .HaveBodyContaining("await CacheEntryProcessor(_entry_)");
    }

    
}