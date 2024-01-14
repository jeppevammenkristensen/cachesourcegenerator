﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using CacheSourceGenerator.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

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

    public ClassesCodeBuilder(LazyTypes types)
    {
        _types = types;
    }

    public string Build(EvaluatedClassCollection classCollection)
    {
        var stringBuilder = new StringBuilder();
        var (compilationUnitSyntax, _) = BuildCompilationUnit(classCollection);

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
    private (CompilationUnitSyntax, ClassDeclarationSyntax) BuildCompilationUnit(
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
        return (newCompilation, classDeclaration);
    }

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
        }

        return newPartialClass;
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
        newMethod = newMethod.WithAttributeLists(SyntaxFactory.List<AttributeListSyntax>());

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


        if (methodSymbol.IsAsyncWithResult(_types))
        {
            return $$"""
                     {
                         var _key_ = {{keyGenerator}};
                            
                            IMemoryCache _cache_ = {{GetCacheAccess(collection)}};
                            var _result_ = await _cache_.GetOrCreateAsync(_key_, async _entry_ =>
                            {
                                {{GenerateCacheEntryProcessing(methodData, true)}}
                                return await {{methodSymbol.Name}}({{string.Join(",", methodSymbol.Parameters.Select(x => x.Name))}});
                            });
                            return _result_;

                     }
                     """;
        }

        return $$"""
                 {
                     var _key_ = {{keyGenerator}};
                 
                        IMemoryCache _cache_ = {{GetCacheAccess(collection)}};
                        return _cache_.GetOrCreate(_key_, _entry_ =>
                        {
                            {{GenerateCacheEntryProcessing(methodData, false)}}
                            return {{methodSymbol.Name}}({{string.Join(",", methodSymbol.Parameters.Select(x => x.Name))}});
                        });

                 }
                 """;
    }

    private string KeyInitialiser(EvaluatedClassCollection collection, MethodData methodData)
    {
        if (methodData.EvaluatedKeyGenerator == null)
        {
            return
                $$"""new { _MethodName = "{{methodData.MethodSymbol.Name}}", _ClassName = "{{collection.NamedTypeSymbol.Name}}", {{string.Join(",", methodData.MethodSymbol.Parameters.Select(x => x.Name))}} }""";
        }

        return GenerateEvaluatedMethodCall(methodData, methodData.EvaluatedKeyGenerator,
            methodData.MethodSymbol.Parameters.Select(x => x.Name).ToArray());


    }

    private string GenerateCacheEntryProcessing(MethodData methodData, bool callerAsync)
    {
        if (methodData.EvaluatedCacheEnricher == null)
            return string.Empty;
        if (methodData.EvaluatedCacheEnricher.IsAsync)
        {
            if (callerAsync)
            {
                return $"""await {methodData.EvaluatedCacheEnricher.MethodName}(_entry_);""";
            }

            return $"""{methodData.EvaluatedCacheEnricher.MethodName}(_entry_).GetAwaiter().GetResult();""";
        }
        else
        {
            return $"""{methodData.EvaluatedCacheEnricher.MethodName}(_entry_);""";
        }
    }

    private string GetCacheAccess(EvaluatedClassCollection classCollection)
    {
        return classCollection switch
        {
            {CacheAccessStrategy: CacheAccessStrategy.FromSelfGeneratedFactory} => "CacheInit.MemoryCache",
            {
                CacheAccessStrategy: CacheAccessStrategy.FromMember,
                CacheAccessSource: CacheMemberAccessSource.PropertyOrField
            } => classCollection.CacheMemberAccessName!,
            {CacheAccessStrategy: CacheAccessStrategy.FromMember, CacheAccessSource: CacheMemberAccessSource.Method}
                => $"{classCollection.CacheMemberAccessName}()",
            _ => throw new InvalidOperationException("Should not be fired")
        };
    }


    /// <summary>
    /// Generates an evaluated method call by generating the invocation string and passing it to the GenerateInvocation method.
    /// </summary>
    /// <param name="collection">The collection of evaluated classes.</param>
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
    /// Generates the invocation statement for a given method.
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

