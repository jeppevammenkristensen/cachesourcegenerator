using Microsoft.CodeAnalysis;

namespace CacheSourceGenerator.Specification;

public class FieldSpecification<T> : Specification<T> where T : ISymbol
{
    private ITypeSymbol? _typeSymbol;
    private Specification<ITypeSymbol>? _typeSpec;

    public override bool IsSatisfiedBy(T obj)
    {
        if (obj.Kind != SymbolKind.Field) return false;

        if (obj is IFieldSymbol fieldSymbol)
        {
            if (_typeSymbol is {} && !fieldSymbol.Type.Equals(_typeSymbol, SymbolEqualityComparer.Default))
                return false;
            if (_typeSpec is { } && _typeSpec != fieldSymbol.Type)
                return false;
        }
        else
        {
            return false;
        }
        
        return true;
    }

    public FieldSpecification<T> WithType(ITypeSymbol typeSymbol)
    {
        _typeSymbol = typeSymbol;
        return this;
    }

    public FieldSpecification<T> WithTypeSpec(Specification<ITypeSymbol> typeSpec)
    {
        _typeSpec = typeSpec;
        return this;
    }
}