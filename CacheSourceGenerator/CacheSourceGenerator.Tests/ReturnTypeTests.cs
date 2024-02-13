using CacheSourceGenerator.Tests.Assertions;
using CacheSourceGenerator.Tests.Utils;
using FluentAssertions;
using Xunit;

namespace CacheSourceGenerator.Tests;

public class ReturnTypeTests : SourceGeneratorTests
{
    [Fact]
    public void ReturnTypeIsAsyncStringNullable_AppliesBang()
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
                        
                        [CacheSourceGenerator.GenerateMemoryCache(MethodName = "CacheName")]
                        public Task<string?> CalculateAge()
                        {
                            return string.Empty;
                        }
                   }
                   """;

        var result = Generator.GenerateResult(code);
        var method = this.AssertAndRetrieveGeneratedMethod(result,"TestClass","CacheName");
        method.Should().HaveBodyContaining("_result_;");
    }
    
    [Fact]
    public void ReturnTypeList_Ok()
    {
        var code = """
                   using Microsoft.Extensions.Caching.Memory;
                   using System.Threading.Tasks;
                   using System.Collections.Generic;

                   namespace TestNamespace;

                   public record Test(string Name){}
                   
                   public partial class TestClass
                   {
                        private readonly IMemoryCache _cache;
                   
                        public TestClass(IMemoryCache cache)
                        {
                            _cache = cache;
                        }
                        
                        [CacheSourceGenerator.GenerateMemoryCache(MethodName = "CacheName")]
                        public List<Test> CalculateAge()
                        {
                            return string.Empty;
                        }
                   }
                   """;

        var result = Generator.GenerateResult(code);
        var method = this.AssertAndRetrieveGeneratedMethod(result,"TestClass","CacheName");
        method.Should().HaveBodyContaining("_result_;");
    }
    
    [Fact]
    public void ReturnTypeIsAsyncStringNotNullable_DoesNotApplyBang()
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
                        
                        [CacheSourceGenerator.GenerateMemoryCache(MethodName = "CacheName")]
                        public Task<string> CalculateAge()
                        {
                            return string.Empty;
                        }
                   }
                   """;

        var result = Generator.GenerateResult(code);
        var method = this.AssertAndRetrieveGeneratedMethod(result,"TestClass","CacheName");
        method.Should().HaveBodyContaining("_result_!;");
    }
    
    [Fact]
    public void ReturnTypeIsInteger_DoesNotApplyBang()
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
                        
                        [CacheSourceGenerator.GenerateMemoryCache(MethodName = "CacheName")]
                        public int CalculateAge()
                        {
                            return 42;
                        }
                   }
                   """;

        var result = Generator.GenerateResult(code);
        var method = this.AssertAndRetrieveGeneratedMethod(result,"TestClass","CacheName");
        method.Should().HaveBodyContaining("return _result_;");
    }
    
    [Fact]
    public void ReturnTypeIsNullableInteger_DoesNotApplyBang()
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
                        
                        [CacheSourceGenerator.GenerateMemoryCache(MethodName = "CacheName")]
                        public int? CalculateAge()
                        {
                            return 42;
                        }
                   }
                   """;

        var result = Generator.GenerateResult(code);
        var method = this.AssertAndRetrieveGeneratedMethod(result,"TestClass","CacheName");
        method.Should().HaveBodyContaining("return _result_;");
    }
    
    
}