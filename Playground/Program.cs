// See https://aka.ms/new-console-template for more information

using CacheSourceGenerator.Sample;
using Microsoft.Extensions.Caching.Memory;

var cache = new MemoryCache(new MemoryCacheOptions());
var another = new Another();
another.Angriest(42, "Customerid");
