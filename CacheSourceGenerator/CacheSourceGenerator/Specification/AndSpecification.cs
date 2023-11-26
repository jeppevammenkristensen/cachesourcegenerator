namespace CacheSourceGenerator.Specification;

public class AndSpecification<T> : Specification<T>
{
    public AndSpecification(Specification<T> left, Specification<T> right)
    {
        Left = left;
        Right = right;
    }

    private Specification<T> Left { get; }
    private Specification<T> Right { get; }

    

    public override bool IsSatisfiedBy(T obj)
    {
        return Left.IsSatisfiedBy(obj) && Right.IsSatisfiedBy(obj);
    }
}