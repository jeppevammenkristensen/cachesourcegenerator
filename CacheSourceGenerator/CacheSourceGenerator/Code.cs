namespace CacheSourceGenerator;

public static class Code
{
    public const string Namespace = "CacheSourceGenerator";
    
    public const string AttributeName = "CachoAttribute";
    
    public const string AddCacheClass = """
                                      private static class CacheInit
                                      {
                                          static CacheInit()
                                          {
                                              _memoryCache = new Lazy<IMemoryCache>(() => new MemoryCache(new MemoryCacheOptions()));
                                          }
                                          
                                          private static Lazy<IMemoryCache> _memoryCache;
                                          public static IMemoryCache MemoryCache => _memoryCache.Value;
                                      }
                                      """;

    public const string MethodName = "MethodName";
    public const string CacheEnricherProcessor = "CacheEnricherProcessor";
}