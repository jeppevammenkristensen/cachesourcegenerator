namespace CacheSourceGenerator.Specification;

public interface ISpecification
{
    bool IsSatisfiedBy(object? obj);
}