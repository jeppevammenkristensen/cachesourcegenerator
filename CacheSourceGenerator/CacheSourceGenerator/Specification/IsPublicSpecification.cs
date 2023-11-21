using CacheSourceGenerator.Utilities;
using Microsoft.CodeAnalysis;

namespace CacheSourceGenerator.Specification;

public class IsPublicSpecification<T> : Specification<T> where T : ISymbol
{
    public override bool IsSatisfiedBy(T obj)
    {
        return obj.IsPublic();
    }
}