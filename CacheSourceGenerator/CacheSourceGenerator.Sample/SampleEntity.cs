using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;

namespace CacheSourceGenerator.Sample;

// This code will not compile until you build the project with the Source Generators


public  static partial class SomeOtherClass
{
private static IMemoryCache GetCache() => new MemoryCache(new MemoryCacheOptions());

[CacheSourceGenerator.Cacho(MethodName = "SomeMethod")]
public static Task<string> ExecuteCall()
{
    return Task.FromResult("Hello");
}
}

public partial class SomeClass
{

    [CacheSourceGenerator.Cacho(MethodName = "SomeMethod")]
    private string DoSomeMethod(string id, int age)
    {
        return $"{id}{age}";
    }
}

public partial class SampleEntitya
{
    private readonly IMemoryCache _memoryCache;

    public SampleEntitya(IMemoryCache memoryCache)
    {
        _memoryCache = memoryCache;
    }

    [Cacho(MethodName = "GetId")]
    private string DoGetId(int id)
    {
        return "Jeppe";
    }

    [Cacho(MethodName = "GetIdAsync")]
    public async Task<string> DoGetIdAsync()
    {
        await Task.Delay(3000);
        return "Jeppe";
    }
}