using Microsoft.CodeAnalysis;

namespace CacheSourceGenerator.Specification;

public class IsInstanceSpecification<T> : Specification<T> where T : ISymbol
{
    public override bool IsSatisfiedBy(T obj)
    {
        return !obj.IsStatic;
    }
}