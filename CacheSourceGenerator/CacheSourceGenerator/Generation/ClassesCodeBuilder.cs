﻿using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using CacheSourceGenerator.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CacheSourceGenerator.Generation;

internal class ClassesCodeBuilder
{
    private readonly LazyTypes _types;

    public ClassesCodeBuilder(LazyTypes types)
    {
        _types = types;
    }
    
    public string BuildClassPartialWithMethod(EvaluatedClassCollection classCollection)
    {
        var stringBuilder = new StringBuilder();
        var (compilationUnitSyntax, classDeclarationSyntax) = BuildCompilationUnit(classCollection);

        

        stringBuilder.AppendLine("//autogenerated");
        stringBuilder.AppendLine(compilationUnitSyntax.NormalizeWhitespace().ToFullString());
        return stringBuilder.ToString();
    }

    private  (CompilationUnitSyntax, ClassDeclarationSyntax) BuildCompilationUnit(EvaluatedClassCollection classCollection)
    {
        var compilation = classCollection.ClassDeclaration.Ancestors().OfType<CompilationUnitSyntax>().First();

        var newCompilation = SyntaxFactory.CompilationUnit();

        newCompilation = newCompilation.WithUsings(compilation.Usings);
        var microsoftExtensionsCachingMemory = "Microsoft.Extensions.Caching.Memory";
        
        if (newCompilation.Usings.All(x => x.Name?.ToString() != microsoftExtensionsCachingMemory))
        {
            newCompilation =
                newCompilation.AddUsings(
                    SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(microsoftExtensionsCachingMemory)));
        }

        var namespaceDeclarationSyntax = SyntaxFactory.FileScopedNamespaceDeclaration(
            SyntaxFactory.ParseName(classCollection.NamedTypeSymbol.ContainingNamespace.ToDisplayString()));

        //var cls = classCollection.ClassDeclaration;

        var classDeclaration = BuildClass(classCollection);

        namespaceDeclarationSyntax = namespaceDeclarationSyntax.AddMembers(classDeclaration);

        newCompilation = newCompilation.AddMembers(namespaceDeclarationSyntax);
        return (newCompilation, classDeclaration);
        
    }

    private ClassDeclarationSyntax BuildClass(EvaluatedClassCollection collection)
    {
        var cls = collection.ClassDeclaration;
        
        var newPartialClass = SyntaxFactory.ClassDeclaration(cls.Identifier)
            .WithModifiers(cls.Modifiers);
        
        foreach (var (methodDeclarationSyntax, methodSymbol, attributeData) in collection.Methods)
        {
            var newIdentifier = SyntaxFactory.Identifier(attributeData.GetAttributePropertyValue<string>("MethodName")!);

            // Resuse the source method but give it the new name
            var newMethod = methodDeclarationSyntax.WithIdentifier(newIdentifier);
            // Clear attributes
            newMethod = newMethod.WithAttributeLists(SyntaxFactory.List<AttributeListSyntax>());

            HashSet<SyntaxKind> accessModifiers = new HashSet<SyntaxKind>(new[]
            {
                SyntaxKind.PublicKeyword, SyntaxKind.PrivateKeyword, SyntaxKind.ProtectedKeyword,
                SyntaxKind.InternalKeyword, SyntaxKind.AsyncKeyword
            });

            // Create a list stripping of the access modifers
            var syntaxTokens = newMethod.Modifiers.Where(x => !accessModifiers.Contains(x.Kind())).ToList();

            var newModifiers = new List<SyntaxToken>() { SyntaxFactory.Token(SyntaxKind.PublicKeyword) };
            if (methodSymbol.IsAsyncWithResult(_types))
            {
                newModifiers.Add(SyntaxFactory.Token(SyntaxKind.AsyncKeyword));
            }
            
            // replace the modifiers but add public
            newMethod = newMethod.WithModifiers(
                SyntaxFactory.TokenList(new[] { SyntaxFactory.Token(SyntaxKind.PublicKeyword) }.Concat(syntaxTokens)));

            var methodStatement = GenerateMethodStatement(collection, methodSymbol);

            newMethod = newMethod.WithBody((BlockSyntax)SyntaxFactory.ParseStatement(methodStatement));
            newPartialClass = newPartialClass.AddMembers(newMethod);
        }
        
        return newPartialClass;
    }

    private  string GenerateMethodStatement(EvaluatedClassCollection collection, IMethodSymbol methodSymbol)
    {
        var nullThrow = methodSymbol.ReturnType.IsNullable()
            ? string.Empty
            : """?? throw new InvalidOperationException("Expected non empty result")""";
        
        
        if (methodSymbol.IsAsyncWithResult(_types))
        {
            
            
            return $$"""
                     {
                         var key = new { _MethodName = "{{methodSymbol.Name}}", _ClassName = "{{collection.NamedTypeSymbol.Name}}", {{string.Join(",", methodSymbol.Parameters.Select(x => x.Name))}} };
                     
                            var result = await CacheInit.MemoryCache.GetOrCreateAsync(key, async entry =>
                            {
                                var result = await {{methodSymbol.Name}}({{string.Join(",", methodSymbol.Parameters.Select(x => x.Name))}});
                            }); 
                            return result {{nullThrow}} ;

                     }
                     """;    
        }

        return $$"""
                 {
                     var key = new { _MethodName = "{{methodSymbol.Name}}", _ClassName = "{{collection.NamedTypeSymbol.Name}}", {{string.Join(",", methodSymbol.Parameters.Select(x => x.Name))}} };
                 
                        return CacheInit.MemoryCache.GetOrCreate(key, entry =>
                        {
                            return {{methodSymbol.Name}}({{string.Join(",", methodSymbol.Parameters.Select(x => x.Name))}});
                        }) {{nullThrow}};

                 }
                 """;


    }
}