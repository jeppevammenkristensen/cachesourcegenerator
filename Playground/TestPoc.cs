using Microsoft.Extensions.Caching.Memory;

namespace Playground;

public class TestPoc
{
    public int? DoSomething([IgnoreKey]int something)
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        return cache.GetOrCreate("Hej", c =>
        {
            return 5;
        })!;
    }
}
[AttributeUsage(AttributeTargets.Parameter)]
public class IgnoreKeyAttribute : Attribute
{
    
}
