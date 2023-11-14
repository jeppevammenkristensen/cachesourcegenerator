namespace CacheSourceGenerator;

public static class Code
{
    public const string Namespace = "CacheSourceGenerator";
    
    public const string AttributeName = "CachoAttribute";
    
    public const string SetupCode = $$"""
                                      using System;
                                      using Microsoft.Extensions.Caching.Memory;

                                      namespace {{Namespace}};

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
                                      """;
}