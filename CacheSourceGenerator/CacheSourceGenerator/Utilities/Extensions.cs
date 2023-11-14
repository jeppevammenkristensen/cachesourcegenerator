using System;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace CacheSourceGenerator.Utilities;

public static class Extensions
{
    public static T? GetAttributePropertyValue<T>(this AttributeData attributeData, string name, T? valueIfNotPresent = default) where T : notnull
    {
        if (attributeData == null) throw new ArgumentNullException(nameof(attributeData));

        var named = attributeData.NamedArguments.Select(x => new {x.Key, Value = x.Value})
            .FirstOrDefault(x => x.Key == name);
        if (named != null)
        {
            return named.Value switch
            {
                {Value: not { }} => default(T),
                {Kind: TypedConstantKind.Enum} x => (T) Enum.ToObject(typeof(T), x.Value),
                _ => (T?) named.Value.Value
            };
        }

        //attributeData.ConstructorArguments.FirstOrDefault(x => x.())

        return valueIfNotPresent;
    }

    public static bool IsAsyncWithResult(this IMethodSymbol methodSymbol, LazyTypes _types)
    {
        if (!methodSymbol.IsAsync)
            return false;

        return methodSymbol.ReturnType.OriginalDefinition.Equals(_types.ListGeneric, SymbolEqualityComparer.Default);
    }

    public static bool IsNullable(this ITypeSymbol typeSymbol)
    {
        if (typeSymbol.NullableAnnotation == NullableAnnotation.Annotated)
        {
            return true;
        }
        
        if (typeSymbol.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T &&
            typeSymbol is INamedTypeSymbol { IsGenericType: true })
        {
            return true;
        }

        return false;
    }
}