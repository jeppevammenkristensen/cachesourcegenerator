using System;
using Microsoft.CodeAnalysis;

namespace CacheSourceGenerator.Specification;

public class PropertySpecification<T> : Specification<T> where T : ISymbol
{
    private ITypeSymbol? _propertyType;
    private Specification<ITypeSymbol>? _typeSpec;

    public override bool IsSatisfiedBy(T obj)
    {
        if (obj.Kind != SymbolKind.Property) return false;
        if (obj is not IPropertySymbol property) return false;

        if (_propertyType is {} && !property.Type.Equals(_propertyType, SymbolEqualityComparer.Default))
        {
            return false;
        }

        if (_typeSpec is { } && _typeSpec != property.Type)
            return false;

        return true;
    }

    public PropertySpecification<T> WithExpectedType(ITypeSymbol type)
    {
        _propertyType = type ?? throw new ArgumentNullException(nameof(type));
        return this;
    }

    public PropertySpecification<T> WithTypeSpec(Specification<ITypeSymbol> typeSpec)
    {
        _typeSpec = typeSpec;
        return this;
    }
}