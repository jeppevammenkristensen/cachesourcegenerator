using System;
using Microsoft.CodeAnalysis;

namespace CacheSourceGenerator.Utilities;

public class LazyTypes
{
    private readonly Compilation _compilation;
    private Lazy<INamedTypeSymbol?> _listGeneric;
    private Lazy<INamedTypeSymbol?> _hashSet;
    private readonly Lazy<INamedTypeSymbol?> _collection;
    private readonly Lazy<INamedTypeSymbol?> _task;
    private readonly Lazy<INamedTypeSymbol?> _genericTask;


    public INamedTypeSymbol? ListGeneric => _listGeneric.Value;
    public INamedTypeSymbol? HashSet => _hashSet.Value;
    public INamedTypeSymbol? Collection => _collection.Value;
    public INamedTypeSymbol? Task => _task.Value;

    public INamedTypeSymbol? GenericTask => _genericTask.Value;
    public LazyTypes(Compilation compilation)
    {
        _compilation = compilation;
        _listGeneric = new Lazy<INamedTypeSymbol?>(() => compilation.GetTypeByMetadataName("System.Collections.Generic.List`1"));
        _hashSet = new Lazy<INamedTypeSymbol?>(() =>
            compilation.GetTypeByMetadataName("System.Collections.Generic.HashSet`1"));
        _collection = new Lazy<INamedTypeSymbol?>(() =>
            compilation.GetTypeByMetadataName("System.Collections.Generic.Collection`1"));
        _task = new Lazy<INamedTypeSymbol?>(() => compilation.GetTypeByMetadataName("System.Threading.Tasks.Task"));
        _genericTask =
            new Lazy<INamedTypeSymbol?>(() => compilation.GetTypeByMetadataName("System.Threading.Tasks.Task`1"));
    }

    /// <summary>
    /// For testing purposes
    /// </summary>
    /// <returns></returns>
    internal static LazyTypes Empty()
    {
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
        return new LazyTypes(null);
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
    }
}