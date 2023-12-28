using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;

namespace CacheSourceGenerator.Sample;

// This code will not compile until you build the project with the Source Generators


public partial class SampleEntity
{
    private readonly IMemoryCache _memoryCache;

    public SampleEntity(IMemoryCache memoryCache)
    {
        _memoryCache = memoryCache;
    }

    [Cacho(MethodName = "GetId", CacheEnricherProcessor = nameof(ProcessCacheEntry))]
    private string? DoGetSomeValue(int id)
    {
        Console.WriteLine($"Retrieving id {id}");
        return "Someresult";
    }


    [Cacho(MethodName = "GetComplex", CacheEnricherProcessor = nameof(ProcessCacheEntry))]
    private DateTime DoGetComplex(Complex complex)
    {
        Console.WriteLine($"Retrieving complex {complex}");
        return DateTime.Now;
    }

    /// <summary>
    /// Process the cache entry by updating its sliding expiration time to 2 minutes.
    /// </summary>
    /// <param name="entry">The cache entry to be processed.</param>
    private void ProcessCacheEntry(ICacheEntry entry)
    {
        entry.SlidingExpiration = TimeSpan.FromMinutes(2);
    }
}

public record Complex(string Category, int Version)
{
    
}