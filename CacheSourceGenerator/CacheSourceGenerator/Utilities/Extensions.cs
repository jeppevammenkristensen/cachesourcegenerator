using System;
using System.Collections.Generic;
using System.Linq;
using CacheSourceGenerator.Generation;
using Microsoft.CodeAnalysis;

namespace CacheSourceGenerator.Utilities;

public static class EnumerableExtensions
{
    public static T[] EmptyIfNull<T>(this T[]? source)
    {
        if (source == null)
            return Array.Empty<T>();
        return source;
    }
    
    public static IEnumerable<T> EmptyIfNull<T>(this IEnumerable<T>? source)
    {
        if (source == null)
        {
            return Enumerable.Empty<T>();
        }

        return source;
    }
    
    public static List<T> EmptyIfNull<T>(this List<T>? source)
    {
        if (source == null)
        {
            return new List<T>();
        }

        return source;
    }

    public static SeparatedSyntaxList<T> ToSeparatedSyntaxList<T>(this IEnumerable<T> source) where T : SyntaxNode
    {
        if (source == null) throw new ArgumentNullException(nameof(source));

        var syntaxList = new SeparatedSyntaxList<T>();
        syntaxList.AddRange(source);
        return syntaxList;
    }
}
public static class Extensions
{
    private static readonly IgnoreKeyRemover IgnoreKeyRemover = new IgnoreKeyRemover();

    public static T? GetAttributePropertyValue<T>(this AttributeData attributeData, string name, T? valueIfNotPresent = default) where T : notnull
    {
        if (attributeData == null) throw new ArgumentNullException(nameof(attributeData));

        var named = attributeData.NamedArguments.Select(x => new {x.Key, x.Value})
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
        return valueIfNotPresent;
    }
    
    

    public static bool IsAsyncWithResult(this IMethodSymbol methodSymbol, LazyTypes _types)
    {
        return methodSymbol.ReturnType.OriginalDefinition.Equals(_types.GenericTask, SymbolEqualityComparer.Default);
    }

    public static bool IsAsync(this ITypeSymbol typeSymbol, LazyTypes types)
    {
        return typeSymbol.Equals(types.Task, SymbolEqualityComparer.Default) ||
               typeSymbol.OriginalDefinition.Equals(types.GenericTask, SymbolEqualityComparer.Default);
    }

    /// <summary>
    /// Gets the underlying type of the given type symbol.
    /// </summary>
    /// <param name="typeSymbol">The type symbol.</param>
    /// <param name="types">The lazy-loaded type symbols.</param>
    /// <returns>The underlying type of the given type symbol.</returns>
    public static ITypeSymbol GetUnderlyingType(this ITypeSymbol typeSymbol, LazyTypes types)
    { 
        if (typeSymbol is INamedTypeSymbol { IsGenericType: true } namedTypeSymbol )
        {
            if (namedTypeSymbol.OriginalDefinition.Equals(types.GenericTask, SymbolEqualityComparer.Default))
            {
                return GetUnderlyingType(namedTypeSymbol.TypeArguments[0], types);    
            }

            if (namedTypeSymbol.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
            {
                return GetUnderlyingType(namedTypeSymbol.TypeArguments[0], types);
            }

            return typeSymbol;
        }

        return typeSymbol;


    }
    
    public static bool IsNullable(this ITypeSymbol typeSymbol, LazyTypes _types)
    {
        var candidate = typeSymbol;

        if (typeSymbol.OriginalDefinition.Equals(_types.GenericTask, SymbolEqualityComparer.Default) && typeSymbol is INamedTypeSymbol named)
        {
            candidate = named.TypeArguments.First();
        }
        
        if (candidate.NullableAnnotation == NullableAnnotation.Annotated)
        {
            return true;
        }
        
        if (candidate.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T &&
            candidate is INamedTypeSymbol { IsGenericType: true })
        {
            return true;
        }

        return false;
    }
    
    public static TSyntaxNode RemoveIgnoreAttribute<TSyntaxNode>(this TSyntaxNode node) where TSyntaxNode : SyntaxNode
    {
        // remove ignore key
        return IgnoreKeyRemover.Visit(node) switch
        {
            TSyntaxNode m => m,
            { } o => throw new InvalidOperationException($"Node returned was unexpectedly of type {o.GetType().Name}"),
            _ => throw new NullReferenceException("Converted was unexpectedly null"),
        };
    }
    
    public static (bool isEnumerable, ITypeSymbol underlyingType) IsEnumerableOfTypeButNotString(
        this ITypeSymbol typeSymbol)
    {
        if (typeSymbol == null) throw new ArgumentNullException(nameof(typeSymbol));
        if (typeSymbol.SpecialType == SpecialType.System_String)
            return (false, default!);

        return IsEnumerableOfType(typeSymbol);
    }
    
    public static (bool isEnumerable, ITypeSymbol underlyingType) IsEnumerableOfType(this ITypeSymbol typeSymbol)
    {
        if (typeSymbol is IArrayTypeSymbol arrayType)
        {
            return (true, arrayType.ElementType);
        }

        if (typeSymbol is INamedTypeSymbol namedType)
        {
            if (namedType.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T)
                return (true, namedType.TypeArguments.First());

            var candidates = namedType.AllInterfaces.Where(x =>
                x.OriginalDefinition?.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T);

            foreach (var namedTypeSymbol in candidates)
            {
                if (namedTypeSymbol.TypeArguments.FirstOrDefault() is { } result)
                    return (true, result);
            }
        }

        return (false, default!);
    }
    
    internal static bool IsPublic(this ISymbol source)
    {
        return source.DeclaredAccessibility == Accessibility.Public;
    }
    
    /// <summary>
    /// Gets all members and members of base types that are not interfaces
    /// It will return all types of members. But will have a special focus on Properties
    /// and Methods. If the method of a base class is virtual and it matches the signature of an existing members
    /// in a derived class it will not be returned
    /// </summary>
    /// <param name="source"></param>
    /// <returns></returns>
    internal static List<(ISymbol member, bool isInherited)> GetAllMembers(this INamedTypeSymbol source)
    {
        List<(ISymbol,bool)> members = new(source.GetMembers().Select(x => (x,false)));

        if (source.BaseType is {TypeKind: TypeKind.Class, SpecialType: SpecialType.None} baseType) 
        {
            foreach ((ISymbol member, _) in baseType.GetAllMembers())
            {
                if (member.IsAbstract)
                    continue;
                if (member.IsVirtual && members.Exists(x => x.Item1.IsApproximateMemberMatch(member)))
                    continue;
                members.Add((member, false));
            }
        }

        return members;
    }

    internal static bool IsApproximateMemberMatch(this ISymbol source, ISymbol other)
    {
        if (source.Kind == other.Kind && source.Name == other.Name)
        {
            if (source is IMethodSymbol methodSymbol && other is IMethodSymbol otherMethodSymbol)
            {
                if (methodSymbol.Parameters.Length != otherMethodSymbol.Parameters.Length)
                    return false;

                for (int i = 0; i < methodSymbol.Parameters.Length; i++)
                {
                    if (!methodSymbol.Parameters[i].Type.Equals(otherMethodSymbol.Parameters[i].Type, SymbolEqualityComparer.Default))
                        return false;
                }

                return true;
            }
            else if (source is IPropertySymbol propertySymbol && other is IPropertySymbol otherPropertySymbol)
            {
                return propertySymbol.Type.Equals(otherPropertySymbol.Type, SymbolEqualityComparer.Default);
            }
            else if (source is IFieldSymbol fieldSymbol && other is IFieldSymbol otherFieldSymbol)
            {
                return fieldSymbol.Type.Equals(otherFieldSymbol.Type, SymbolEqualityComparer.Default);
            }
        }

        // We currently only have interest in properties and symbols
        return false;
            
    }
    
   

}