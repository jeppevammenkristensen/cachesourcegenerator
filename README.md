# Cache source generator
 _[![CacheSourceGenerator Nuget Version](https://img.shields.io/nuget/v/CacheSourceGenerator?style=flat-square&label=NuGet%3A%20CacheSourceGenerator)](https://www.nuget.org/packages/CacheSourceGenerator)_
 
A source generator that can generate simple cache boilerplate around a method

## Getting started

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

This will generate

```csharp
public partial class Someclass
{
   public string Somemethod(string id, int age)
  {
      var key = new
          {
              _MethodName = "DoGetId",
              _ClassName = "SampleEntity",
              id,
              age,
          };
          return CacheInit.MemoryCache.GetOrCreate(key, entry =>
          {
              return DoGetId(i);
          }) ?? throw new InvalidOperationException("Expected non empty result");
  }
}
```

if the method is async or returning a `Task<T>` the generated method will take that into consideration. It will also leave of the exception if the return type is nullable.

Per default the caching mechanism uses IMemoryCache. So `Microsoft.Extensions.Caching.Abstractions` and  `Microsoft.Extensions.Caching.Memory`. The generator exposes the `CacheInit` static class that per default returns a singleton instance of memory

```csharp
namespace CacheSourceGenerator;

public delegate IMemoryCache CacheInitializer();

internal static class CacheInit
{
    static CacheInit()
    {
        _memoryCache = new Lazy<IMemoryCache>(() => Initializer());
    }
    
    private static Lazy<IMemoryCache> _memoryCache; //= new Lazy<IMemoryCache>(Initializer)
    
    private static CacheInitializer Initializer = () => new MemoryCache(new MemoryCacheOptions());
    public static IMemoryCache MemoryCache => _memoryCache.Value;

    public static void ReplaceInitializer(CacheInitializer initializer)
    {
        Initializer = initializer;
        _memoryCache = new Lazy<IMemoryCache>(() => Initializer());
    }
}
```

You can replace how the IMemoryCache is initialized by calling the ReplaceInitializer method on CacheInit
