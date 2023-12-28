// See https://aka.ms/new-console-template for more information

using CacheSourceGenerator.Sample;
using Microsoft.Extensions.Caching.Memory;

var cache = new MemoryCache(new MemoryCacheOptions());

SampleEntity entity = new SampleEntity(cache);
entity.GetComplex(new Complex("First",3));
entity.GetComplex(new Complex("First",3));
Console.WriteLine("Evicting");
entity.GetComplex_Evict(new Complex("First",3));
entity.GetComplex(new Complex("First",3));
entity.GetComplex(new Complex("First",3));

