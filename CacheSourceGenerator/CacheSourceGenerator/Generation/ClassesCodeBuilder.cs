﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using CacheSourceGenerator.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CacheSourceGenerator.Generation;

internal class UsingsBuilder
{
    private readonly HashSet<string> _usings = new HashSet<string>();

    public UsingsBuilder(SyntaxList<UsingDirectiveSyntax> source)
    {
        foreach (var fullUsingStatement in source.Select(x => x.ToString()))
        {
            _usings.Add(fullUsingStatement);
        }
    }

    public void AddNamespace(string usingNamespace)
    {
        _usings.Add($"using {usingNamespace};");
    }

    public CompilationUnitSyntax ApplyUsings(CompilationUnitSyntax original)
    {
        var compilationUnitSyntax = SyntaxFactory.ParseCompilationUnit(string.Join(" ", _usings));
        return original.WithUsings(compilationUnitSyntax.Usings);
    }
}

internal class ClassesCodeBuilder
{
    private readonly LazyTypes _types;
    private IgnoreKeyRemover _ignoreKeyRemover;


    public ClassesCodeBuilder(LazyTypes types)
    {
        _types = types;
        _ignoreKeyRemover = new IgnoreKeyRemover();
    }

    public string Build(EvaluatedClassCollection classCollection)
    {
        var stringBuilder = new StringBuilder();
        var compilationUnitSyntax = BuildCompilationUnit(classCollection);

        stringBuilder.AppendLine("#nullable enable");
        stringBuilder.AppendLine("//autogenerated");
        stringBuilder.AppendLine(compilationUnitSyntax.NormalizeWhitespace().ToFullString());
        return stringBuilder.ToString();
    }

    /// <summary>
    /// Builds a compilation unit with a class declaration based on the provided class data collection.
    /// </summary>
    /// <param name="classDataCollection">The evaluated class data collection.</param>
    /// <returns>A tuple containing the built compilation unit and class declaration.</returns>
    private CompilationUnitSyntax BuildCompilationUnit(
        EvaluatedClassCollection classDataCollection)
    {
        // Get the compilationunit of the existing class
        var compilation = classDataCollection.ClassDeclaration.Ancestors().OfType<CompilationUnitSyntax>().First();

        // Create a new compilation
        var newCompilation = SyntaxFactory.CompilationUnit();

        var usingsBuilder = new UsingsBuilder(compilation.Usings);
        usingsBuilder.AddNamespace("System"); // Added to support Lazy

        // If we use the Strategy SelfGeneratedFactory we add the relevant using if it's 
        // not already installed
        if (classDataCollection.CacheAccessStrategy == CacheAccessStrategy.FromSelfGeneratedFactory)
        {
            var microsoftExtensionsCachingMemory = "Microsoft.Extensions.Caching.Memory";
            usingsBuilder.AddNamespace(microsoftExtensionsCachingMemory);
        }

        newCompilation = usingsBuilder.ApplyUsings(newCompilation);

        // Create a namespace matching the existing (as a FileScopedNamespaceDeclaration)
        var namespaceDeclarationSyntax = SyntaxFactory.FileScopedNamespaceDeclaration(
            SyntaxFactory.ParseName(classDataCollection.NamedTypeSymbol.ContainingNamespace.ToDisplayString()));

        // Build the class 
        var classDeclaration = BuildPartialClass(classDataCollection);

        // Add the class to the namespace
        namespaceDeclarationSyntax = namespaceDeclarationSyntax.AddMembers(classDeclaration);

        // And update the new compilation
        newCompilation = newCompilation.AddMembers(namespaceDeclarationSyntax);
        return newCompilation;
    }

    /// <summary>
    /// Builds a partial class based on the provided evaluated class collection.
    /// </summary>
    /// <param name="collection">The evaluated class collection.</param>
    /// <returns>The built partial class as a ClassDeclarationSyntax object.</returns>
    private ClassDeclarationSyntax BuildPartialClass(EvaluatedClassCollection collection)
    {
        var existingClass = collection.ClassDeclaration;

        // Create a new class with the same modifiers as the existing class
        var newPartialClass = SyntaxFactory.ClassDeclaration(existingClass.Identifier)
            .WithModifiers(existingClass.Modifiers);

        // If we are using SelfGeneratedFactory. We parse the and insert a static class that can
        // init and return an IMemoryCache instance
        if (collection.CacheAccessStrategy == CacheAccessStrategy.FromSelfGeneratedFactory)
        {
            newPartialClass = newPartialClass.AddMembers(SyntaxFactory.ParseMemberDeclaration(Code.AddCacheClass) ??
                                                         throw new InvalidOperationException(
                                                             "Failed to parse AddCacheClass code"));
        }

        // Loop through all methods we have established as valid 
        foreach (var methodData in collection.Methods)
        {
            var wrappingMethod = CreateWrappingMethod(collection, methodData);
            var createEvictMethod = CreateEvictMethod(collection, methodData);
            newPartialClass = newPartialClass.AddMembers(wrappingMethod, createEvictMethod);
            newPartialClass = newPartialClass.AddMembers(GeneratePartialMethods(methodData).ToArray());
        }

        return newPartialClass;
    }

    private ImmutableArray<MemberDeclarationSyntax> GeneratePartialMethods(MethodData methodData)
    {
        var onCallingMethod = SyntaxFactory.MethodDeclaration(
            SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)),
            SyntaxFactory.Identifier(methodData.OnCallingMethodName));
        onCallingMethod = onCallingMethod
            .AddModifiers(SyntaxFactory.Token(SyntaxKind.PartialKeyword))
            .WithParameterList(methodData.MethodDeclarationSyntax.ParameterList)
            .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken))
            .RemoveIgnoreAttribute();
        var result = ImmutableArray.Create<MemberDeclarationSyntax>(onCallingMethod);


        var returnedParameter = SyntaxFactory.Parameter(SyntaxFactory.Identifier("_returned_"))
            .WithType(methodData.UnderlyingTypeAsTypeSyntax());
        result = result.Add(onCallingMethod
            .WithIdentifier(SyntaxFactory.Identifier(methodData.OnCalledMethodName))
            .AddParameterListParameters(returnedParameter));
        return result;
    }

    private MethodDeclarationSyntax CreateEvictMethod(EvaluatedClassCollection collection, MethodData methodData)
    {
        var overridingMethodName = methodData.Attribute.GetAttributePropertyValue<string>(Code.MethodName)!;
        var newIdentifier = SyntaxFactory.Identifier($"{overridingMethodName}_Evict");
        var wrappingMethod = CreateMethodSignatureFromExisting(methodData, newIdentifier);
        wrappingMethod =
            wrappingMethod.WithReturnType(SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)));
        var evictMethodStatement = GenerateEvictMethodStatement(collection, methodData);

        return wrappingMethod.WithBody((BlockSyntax) SyntaxFactory.ParseStatement(evictMethodStatement));
    }

    private MethodDeclarationSyntax CreateWrappingMethod(EvaluatedClassCollection collection, MethodData methodData)
    {
        var newIdentifier =
            SyntaxFactory.Identifier(methodData.Attribute.GetAttributePropertyValue<string>(Code.MethodName)!);
        var wrappingMethod = CreateMethodSignatureFromExisting(methodData, newIdentifier);
        var methodStatement = GenerateMethodStatement(collection, methodData);

        return wrappingMethod.WithBody((BlockSyntax) SyntaxFactory.ParseStatement(methodStatement));
    }

    private MethodDeclarationSyntax CreateMethodSignatureFromExisting(MethodData methodData, SyntaxToken newIdentifier)
    {
        // Reuse the source method but give it the new name
        var newMethod = methodData.MethodDeclarationSyntax.WithIdentifier(newIdentifier);
        // Clear attributes
        newMethod = newMethod
            .WithAttributeLists(SyntaxFactory.List<AttributeListSyntax>())
            .RemoveIgnoreAttribute();

        HashSet<SyntaxKind> accessModifiers = new HashSet<SyntaxKind>(new[]
        {
            SyntaxKind.PublicKeyword, SyntaxKind.PrivateKeyword, SyntaxKind.ProtectedKeyword,
            SyntaxKind.InternalKeyword, SyntaxKind.AsyncKeyword
        });

        // Create a list stripping of the access modifers
        var syntaxTokens = newMethod.Modifiers.Where(x => !accessModifiers.Contains(x.Kind())).ToList();

        var newModifiers = new List<SyntaxToken>() {SyntaxFactory.Token(SyntaxKind.PublicKeyword)};
        if (methodData.MethodSymbol.IsAsyncWithResult(_types))
        {
            newModifiers.Add(SyntaxFactory.Token(SyntaxKind.AsyncKeyword));
        }

        // replace the modifiers but add public
        newMethod = newMethod.WithModifiers(
            SyntaxFactory.TokenList(newModifiers.Concat(syntaxTokens)));
        return newMethod;
    }

    

    private string GenerateEvictMethodStatement(EvaluatedClassCollection collection, MethodData methodData)
    {
        var key = KeyInitialiser(collection, methodData);
        return $$"""
                 {
                  var _key_ = {{key}};
                  IMemoryCache _cache_ = {{GetCacheAccess(collection)}};
                  _cache_.Remove(_key_);
                  }
                 """;
    }

    private string GenerateMethodStatement(EvaluatedClassCollection collection, MethodData methodData)
    {
        var methodSymbol = methodData.MethodSymbol;

        var keyGenerator =
            KeyInitialiser(collection, methodData);

        var conditionalBang = methodData switch
        {
            {UnderlyingType.IsValueType: true} => string.Empty,
            {ReturnTypeIsNullable: false} => "!",
            _ => string.Empty
        };


        if (methodSymbol.IsAsyncWithResult(_types))
        {
            return $$"""
                     {
                         var _key_ = {{keyGenerator}};
                            
                            IMemoryCache _cache_ = {{GetCacheAccess(collection)}};
                            var _result_ = await _cache_.GetOrCreateAsync(_key_, async _entry_ =>
                            {
                                {{GenerateCacheEntryProcessing(methodData)}}
                                
                                {{GenerateOnCallingInvocation(methodData)}};
                                var _callResult_ = await {{methodSymbol.Name}}({{string.Join(",", methodSymbol.Parameters.Select(x => x.Name))}});
                                {{GenerateOnCalledInvocation(methodData)}};
                                return _callResult_;
                            });
                            return _result_{{conditionalBang}};

                     }
                     """;
        }

        return $$"""
                 {
                     var _key_ = {{keyGenerator}};
                 
                        IMemoryCache _cache_ = {{GetCacheAccess(collection)}};
                        var _result_ = _cache_.GetOrCreate(_key_, _entry_ =>
                        {
                            {{GenerateCacheEntryProcessing(methodData)}}
                            {{GenerateOnCallingInvocation(methodData)}}
                            var _callResult_ = {{methodSymbol.Name}}({{string.Join(",", methodSymbol.Parameters.Select(x => x.Name))}});
                            {{GenerateOnCalledInvocation(methodData)}};
                            return _callResult_;
                        });
                        return _result_{{conditionalBang}};

                 }
                 """;
    }

    private string GenerateOnCallingInvocation(MethodData methodData)
    {
        return
            $"{methodData.OnCallingMethodName}({string.Join(",", methodData.MethodSymbol.Parameters.Select(x => x.Name))});";
    }
    
    private string GenerateOnCalledInvocation(MethodData methodData)
    {
        return
            $"{methodData.OnCalledMethodName}({string.Join(",", methodData.MethodSymbol.Parameters.Select(x => x.Name).Concat(new[] {"_callResult_"}))});";
    }

    private string KeyInitialiser(EvaluatedClassCollection collection, MethodData methodData)
    {
        if (methodData.EvaluatedKeyGenerator == null)
        {
            return
                $$"""new { _MethodName = "{{methodData.MethodSymbol.Name}}", _ClassName = "{{collection.NamedTypeSymbol.Name}}", {{string.Join(",", methodData.GetParametersForKeyGenerator()) }} }""";
        }

        return GenerateEvaluatedMethodCall(methodData, methodData.EvaluatedKeyGenerator,
            methodData.MethodSymbol.Parameters.Select(x => x.Name).ToArray());


    }

    private string GenerateCacheEntryProcessing(MethodData methodData)
    {
        if (methodData.EvaluatedCacheEnricher == null)
            return string.Empty;

        return $"{GenerateEvaluatedMethodCall(methodData, methodData.EvaluatedCacheEnricher, "_entry_")};";
    }

    /// <summary>
    /// Gets the cache access string based on the provided class collection.
    /// </summary>
    /// <param name="classCollection">The evaluated class collection.</param>
    /// <returns>The cache access string.</returns>
    private string GetCacheAccess(EvaluatedClassCollection classCollection)
    {
        return classCollection switch
        {
            {CacheAccessStrategy: CacheAccessStrategy.FromSelfGeneratedFactory} => "CacheInit.MemoryCache",
            {
                CacheAccessStrategy: CacheAccessStrategy.FromMember,
                CacheAccessSource: CacheMemberAccessSource.PropertyOrField
            } => classCollection.CacheMemberAccessName!,
            {
                CacheAccessStrategy: CacheAccessStrategy.FromMember, 
                CacheAccessSource: CacheMemberAccessSource.Method
            } => $"{classCollection.CacheMemberAccessName}()",
            _ => throw new InvalidOperationException("Should not be fired")
        };
    }


    /// <summary>
    /// Generates an evaluated method call by generating the invocation string and passing it to the GenerateInvocation method.
    /// </summary>
    /// <param name="methodData">The method data.</param>
    /// <param name="method">The evaluated method.</param>
    /// <param name="parameterNames">The parameter names used in the method call.</param>
    /// <returns>The generated evaluated method call.</returns>
    internal string GenerateEvaluatedMethodCall(MethodData methodData,
        EvaluatedMethod method, params string[] parameterNames)
    {
        var invocation = $"{method.MethodName}({string.Join(",", parameterNames.EmptyIfNull())})";
        return GenerateInvocation(methodData, method, invocation);

    }

    /// <summary>
    /// Generates the correct invocation based on if the method being called is async and
    /// if the method calling it is async
    /// </summary>
    /// <param name="methodData">The method data.</param>
    /// <param name="method">The evaluated method.</param>
    /// <param name="invocation">The original invocation statement.</param>
    /// <returns>The generated invocation statement.</returns>
    private string GenerateInvocation(MethodData methodData, EvaluatedMethod method, string invocation)
    {
        if (methodData.CallerIsAsync)
        {
            return method switch
            {
                {IsAsync: true} => $"await {invocation}",
                {IsAsync: false} => invocation
            };
        }

        return method switch
        {
            {IsAsync: true} => $"{invocation}.GetAwaiter().GetResult()",
            {IsAsync: false} => invocation
        };

    }

}

