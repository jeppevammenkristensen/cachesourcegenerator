using Microsoft.Extensions.Caching.Memory;

namespace Playground;

public class TestPoc
{
    public int? DoSomething()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        return cache.GetOrCreate("Hej", c =>
        {
            return 5;
        })!;
    }
}