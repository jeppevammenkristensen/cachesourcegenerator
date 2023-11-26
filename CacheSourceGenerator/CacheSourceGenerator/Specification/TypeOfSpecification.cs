using System.Linq;
using CacheSourceGenerator.Utilities;
using Microsoft.CodeAnalysis;

namespace CacheSourceGenerator.Specification;
public class TypeOfSpecification : Specification<ITypeSymbol>
{
    private readonly ITypeSymbol? _typeSymbol;

    public TypeOfSpecification(ITypeSymbol? typeSymbol)
    {
        _typeSymbol = typeSymbol;
    }

    public TypeOfSpecification SetExactMatch()
    {
        ExactMatch = true;
        return this;
    }

    public bool ExactMatch { get; set; }

    public override bool IsSatisfiedBy(ITypeSymbol obj)
    {
        if (_typeSymbol == null)
            return false;
        
        if (obj.Equals(_typeSymbol, SymbolEqualityComparer.Default))
        {
            return true;
        }

        if (ExactMatch)
            return false;

        return obj.AllInterfaces.Any(x => x.Equals(_typeSymbol, SymbolEqualityComparer.Default));
    }
}