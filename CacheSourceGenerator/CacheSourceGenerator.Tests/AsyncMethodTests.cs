using CacheSourceGenerator.Tests.Assertions;
using CacheSourceGenerator.Tests.Utils;
using Xunit;

namespace CacheSourceGenerator.Tests;


public class AsyncMethodTests
{
    [Fact]
    public void MethodWithTaskGeneratesAsyncMethod()
    {
        var code = """
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
                        
                        [CacheSourceGenerator.Cacho(MethodName = "CacheName")]
                        public Task<int> CalculateAge()
                        {
                            return 42;
                        }
                   }
                   """;

        Generator.GenerateResult(code).Should().ContainFile("TestClass.g.cs").Which.Should()
            .ContainClass("TestClass").Which.Should()
            .ContainMethod("CacheName").Which.Should().BeAsync().And.HaveReturnType("Task<int>").
            And.HaveBodyContaining("var result = await cache.GetOrCreateAsync(key, async entry =>").And.HaveBodyContaining("return await CalculateAge()");
    }    
}