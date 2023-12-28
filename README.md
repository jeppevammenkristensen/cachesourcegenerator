# Cache source generator
 _[![CacheSourceGenerator Nuget Version](https://img.shields.io/nuget/v/CacheSourceGenerator?style=flat-square&label=NuGet%3A%20CacheSourceGenerator)](https://www.nuget.org/packages/CacheSourceGenerator)_

A source generator that can generate simple cache boilerplate to wrap around a method

## Getting started

This generator works by wrapping a method in another method with the same signature, that ensures calls are cached.

In a partial class decorate the method with the `Cacho` attribute

```csharp
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
        return "Someresult";
    }

    
    public void ProcessCacheEntry(ICacheEntry entry)
    {
        entry.SlidingExpiration = TimeSpan.FromMinutes(2);
    }
}
```

And it will generate

```csharp
public partial class SampleEntity
{
    public string? GetId(int id)
    {
        var _key_ = new
        {
            _MethodName = "DoGetSomeValue",
            _ClassName = "SampleEntity",
            id
        };
        IMemoryCache _cache_ = _memoryCache;
        return _cache_.GetOrCreate(_key_, _entry_ =>
        {
            ProcessCacheEntry(_entry_);
            return DoGetSomeValue(id);
        });
    }
    
    public void GetId_Evict(int id)
    {
        var _key_ = new
        {
            _MethodName = "DoGetSomeValue",
            _ClassName = "SampleEntity",
            id
        };
        IMemoryCache _cache_ = _memoryCache;
        _cache_.Remove(_key_);
    }
}
```

Note that that defining the CacheEnricherProcessor is optional and can be left out

## Cache access

The IMemoryCache can be retrieved in two ways. Autogenerated or by providing it in the class

### Autogenerated cache access code

This requires that you install the nuget package Microsoft.Extensions.Caching.Memory.

Decorate a method that returns a value on a partial class with the Cacho Attribute

```csharp
public partial class SomeClass
{

  [CacheSourceGenerator.Cache(MethodName = "SomeMethod")]
  private string DoSomeMethod(string id, int age)
  {
      return $"{id}{age}";
  }
}
```

This will generate the code below.

```csharp
public partial class SomeClass
{
    private static class CacheInit
    {
        static CacheInit()
        {
            _memoryCache = new Lazy<IMemoryCache>(() => new MemoryCache(new MemoryCacheOptions()));
        }

        private static Lazy<IMemoryCache> _memoryCache;
        public static IMemoryCache MemoryCache => _memoryCache.Value;
    }

    public string SomeMethod(string id, int age)
    {
        var key = new
        {
            _MethodName = "DoSomeMethod",
            _ClassName = "SomeClass",
            id,
            age
        };
        IMemoryCache cache = CacheInit.MemoryCache;
        return cache.GetOrCreate(key, entry =>
        {
            return DoSomeMethod(id, age);
        });
    }
}
```

### Providing the cache from the class

An alternative is to provide a IMemoryCache instance from the class. This can be done through a

* Field
* Property
* Method (parameter less)

```csharp
public static partial class SomeOtherClass
{
    private static IMemoryCache GetCache() => new MemoryCache(new MemoryCacheOptions());

    [CacheSourceGenerator.Cacho(MethodName = "SomeMethod")]
    public static Task<string> ExecuteCall()
    {
        return Task.FromResult("Hello");
    }
}
```

This will generate the code below.

```csharp
public static partial class SomeOtherClass
{
    public async static Task<string> SomeMethod()
    {
        var key = new
        {
            _MethodName = "ExecuteCall",
            _ClassName = "SomeOtherClass",
        };
        IMemoryCache cache = GetCache();
        var result = await cache.GetOrCreateAsync(key, async entry =>
        {
            return await ExecuteCall();
        });
        return result ?? throw new InvalidOperationException("Expected non empty result");
    }
}
```

## Method generation

if the method is async or returning a `Task<T>` the generated method will take that into consideration.

If the return type is not nullable, the generated method will throw an exception if the result of the method call is null.
