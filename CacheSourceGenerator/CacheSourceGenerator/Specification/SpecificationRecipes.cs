using System.Diagnostics;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace CacheSourceGenerator.Specification;

public static class SpecificationRecipes
{
    /// <summary>
    /// Returns a specification that check if a given type is any kind of known IEnumerable or List or other
    /// of a given type. Only exception is that is will not treat string as a collection of chars.
    /// </summary>
    /// <param name="typeSymbol"></param>
    /// <returns></returns>
    public static EnumerableSpecification<ITypeSymbol> EnumerableOfTypeSpec(ITypeSymbol typeSymbol) =>
        new EnumerableSpecification<ITypeSymbol>()
            .WithUnderlyingType(typeSymbol);
    
    /// <summary>
    /// Returns a <see cref="Specification{T}"/> that evaluates if a given symbol is Public and an Instance
    /// </summary>
    public static Specification<ISymbol> IsPublicAndInstanceSpec => IsPublic & new IsInstanceSpecification<ISymbol>();

    public static Specification<ISymbol> IsPublic => new IsPublicSpecification<ISymbol>();

    public static readonly IsInstanceSpecification<ISymbol> IsInstance = new();
    public static readonly Specification<ISymbol> IsStatic = IsInstance.Not(); 
    
    public static MethodSpecification<ISymbol> MethodWithNoParametersSpec =>
        new MethodSpecification<ISymbol>().WithParameters(0);
    
    public static MethodSpecification<ISymbol> MethodWithParametersSpec(uint parameters) =>
        new MethodSpecification<ISymbol>().WithParameters(parameters);

    public static KeyGeneratorMethodMatchSpecification<ISymbol> KeyGeneratorMatch(IMethodSymbol source) =>
        new KeyGeneratorMethodMatchSpecification<ISymbol>(source);

    public static MethodSpecification<ISymbol> MethodWithTypeSpec(Specification<ITypeSymbol> typeSpec) =>
        new MethodSpecification<ISymbol>().WithTypeSpec(typeSpec);

    public static Specification<ISymbol> PropertyOfTypeSpec(ITypeSymbol typeSymbol) =>
        new PropertySpecification<ISymbol>().WithExpectedType(typeSymbol);

    public static Specification<ISymbol> PropertyOfTypeSpec(Specification<ITypeSymbol> typeSymbol) =>
        new PropertySpecification<ISymbol>().WithTypeSpec(typeSymbol);

    public static Specification<ISymbol> FieldOfTypeSpec(ITypeSymbol typeSymbol) =>
        new FieldSpecification<ISymbol>().WithType(typeSymbol);

    public static FieldSpecification<ISymbol> FieldOfTypeSpec(Specification<ITypeSymbol> typeSpec) =>
        new FieldSpecification<ISymbol>().WithTypeSpec(typeSpec);





}

public class KeyGeneratorMethodMatchSpecification<T>(IMethodSymbol source) : Specification<T>
    where T : ISymbol
{
    public override bool IsSatisfiedBy(T obj)
    {
        if (obj is not IMethodSymbol methodSymbol)
        {
            return false;
        }

        if (methodSymbol.ReturnsVoid)
        {
            return false;
        }

        if (methodSymbol.Parameters.Length != source.Parameters.Length)
        {
            return false; 
        }
        
        foreach (var (left, right) in methodSymbol.Parameters.Zip(source.Parameters, (left,right) => (left,right)))
        {
            if (!left.Type.Equals(right.Type, SymbolEqualityComparer.Default))
            {
                return false;
            }
        }

        return true;

    }
}