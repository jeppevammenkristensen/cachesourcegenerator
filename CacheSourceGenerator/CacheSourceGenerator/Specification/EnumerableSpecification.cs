using CacheSourceGenerator.Utilities;
using Microsoft.CodeAnalysis;

namespace CacheSourceGenerator.Specification;

public class EnumerableSpecification<T> : Specification<T> where T : ITypeSymbol
{
    public ITypeSymbol? Type { get; set; }
    
    public override bool IsSatisfiedBy(T obj)
    {
        var (isEnumerable, underlyingType) = obj.IsEnumerableOfTypeButNotString();
        if (!isEnumerable) return false;

        if (Type is { })
        {
            if (!underlyingType.Equals(Type, SymbolEqualityComparer.Default))
            {
                return false;
            }
        }

        return true;
    }
    
    public EnumerableSpecification<T> WithUnderlyingType(ITypeSymbol typeSymbol)
    {
        var (isEnumerable, underlyingType) = typeSymbol.IsEnumerableOfTypeButNotString();
        
        Type = isEnumerable ? underlyingType : typeSymbol;
        return this;
    }
}