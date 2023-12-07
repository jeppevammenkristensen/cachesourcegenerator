using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using CacheSourceGenerator.Generation;
using CacheSourceGenerator.Specification;
using CacheSourceGenerator.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace CacheSourceGenerator;

/// <summary>
/// A sample source generator that creates a custom report based on class properties. The target class should be annotated with the 'Generators.ReportAttribute' attribute.
/// When using the source code as a baseline, an incremental source generator is preferable because it reduces the performance overhead.
/// </summary>
[Generator]
public class CachoIncrementalSourceGenerator : IIncrementalGenerator
{
    private const string Namespace = "CacheSourceGenerator";
    
    
    private const string AttributeSourceCode = $$"""
                                                 // <auto-generated/>
                                                 #nullable enable
                                                 namespace {{Namespace}}
                                                 {
                                                     [System.AttributeUsage(System.AttributeTargets.Method)]
                                                     public class {{Code.AttributeName}} : System.Attribute
                                                     {
                                                        /// <summary>
                                                        /// The name of the generated cache method 
                                                        /// </summary>
                                                        public required string {{Code.MethodName}} { get;set; }
                                                        
                                                        /// <summary>
                                                        /// The name of a method in the current class that takes
                                                        /// an CacheEntry and processes it 
                                                        /// </summary>
                                                        public string? {{Code.CacheEnricherProcessor}} { get;set; }
                                                     }
                                                 }
                                                 """;


    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Add the marker attribute to the compilation.
        context.RegisterPostInitializationOutput(ctx =>
        {
            ctx.AddSource(
                "CachoAttribute.g.cs",
                SourceText.From(AttributeSourceCode, Encoding.UTF8));
        });
        
        var provider = context.SyntaxProvider
            .CreateSyntaxProvider(
                (s, _) => s is MethodDeclarationSyntax,
                (ctx, _) => GetMethodDeclarationForSourceGen(ctx))
            .Where(t => t.reportAttributeFound)
            .Select((t, _) => (t.Item1, t.Item2));

        // Generate the source code.
        context.RegisterSourceOutput(context.CompilationProvider.Combine(provider.Collect()),
            ((ctx, t) => GenerateCode(ctx, t.Left, t.Right)));
    }

    /// <summary>
    /// Checks whether the Node is annotated with the [Report] attribute and maps syntax context to the specific node type (ClassDeclarationSyntax).
    /// </summary>
    /// <param name="context">Syntax context, based on CreateSyntaxProvider predicate</param>
    /// <returns>The specific cast and whether the attribute was found.</returns>
    private static (MethodDeclarationSyntax, ClassDeclarationSyntax, bool reportAttributeFound) GetMethodDeclarationForSourceGen(
        GeneratorSyntaxContext context)
    {
        var methodDeclarationSyntax = (MethodDeclarationSyntax)context.Node;
        // Go through all attributes of the class.
        foreach (AttributeListSyntax attributeListSyntax in methodDeclarationSyntax.AttributeLists)
        {
            foreach (AttributeSyntax attributeSyntax in attributeListSyntax.Attributes)
            {
                if (ModelExtensions.GetSymbolInfo(context.SemanticModel, attributeSyntax).Symbol is not IMethodSymbol
                    attributeSymbol)
                    continue; // if we can't get the symbol, ignore it

                string attributeName = attributeSymbol.ContainingType.ToDisplayString();

                // Check the full name of the [Report] attribute.
                if (attributeName == $"{Namespace}.{Code.AttributeName}")
                {
                    if (methodDeclarationSyntax.Ancestors()
                            .FirstOrDefault(x => x.IsKind(SyntaxKind.ClassDeclaration)) is
                        not ClassDeclarationSyntax parentClass)
                    {
                        return (methodDeclarationSyntax, default!, false);
                    }

                    return (methodDeclarationSyntax, parentClass, true);
                }
            }
        }

        return (methodDeclarationSyntax, default!,false);
    }

    /// <summary>
    /// Generate code action.
    /// It will be executed on specific nodes (ClassDeclarationSyntax annotated with the [Report] attribute) changed by the user.
    /// </summary>
    /// <param name="context">Source generation context used to add source files.</param>
    /// <param name="compilation">Compilation used to provide access to the Semantic Model.</param>
    /// <param name="classDeclarations">Nodes annotated with the [Report] attribute that trigger the generate action.</param>
    private void GenerateCode(SourceProductionContext context, Compilation compilation,
        ImmutableArray<(MethodDeclarationSyntax, ClassDeclarationSyntax)> classDeclarations)
    {
        var types = new LazyTypes(compilation);
        
        // Go through all filtered class declarations.
        foreach (var grouping in classDeclarations.GroupBy(x => x.Item2, x => x.Item1))
        {
            var classDeclarationSyntax = grouping.Key;

            if (!classDeclarationSyntax.Modifiers.Any(x => x.IsKind(SyntaxKind.PartialKeyword)))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor(DiagnosticIds.Id_001,
                        "Containing class must be partial",
                        "When decoration a method with the Cacho attribute the containing class must be partial",
                        "General",
                        DiagnosticSeverity.Error,
                        true),
                    classDeclarationSyntax.GetLocation()));
                continue;
            }
            
            // We need to get semantic model of the class to retrieve metadata.
            var semanticModel = compilation.GetSemanticModel(classDeclarationSyntax.SyntaxTree);
            
            
            // Symbols allow us to get the compile-time information.
            if (ModelExtensions.GetDeclaredSymbol(semanticModel, classDeclarationSyntax) is not INamedTypeSymbol classSymbol)
                continue;
            
            var classes = new List<EvaluatedClassCollection>();
            if (Evaluate(grouping) is { Methods.Length : > 0 } evaluated)
            {
                classes.Add(evaluated);
            }
            
            EvaluatedClassCollection Evaluate(IGrouping<ClassDeclarationSyntax,MethodDeclarationSyntax> classMethodGrouping)
            {
                EvaluatedClassCollection collection = new EvaluatedClassCollection(classMethodGrouping.Key, classSymbol);
                
                // Check if the Microsoft.Extensions.Caching.Memory assembly is references
                var memoryCacheExists =
                    classSymbol.ContainingModule.ReferencedAssemblies.Any(x =>
                        x.Name == "Microsoft.Extensions.Caching.Memory");

                // Check if any valid members can provide a memorycache
                var typeSpec = new TypeOfSpecification(types.MemoryCache);
                var methodSpecification = SpecificationRecipes.MethodWithNoParametersSpec.WithTypeSpec(typeSpec);
                
                var memberSpecficiation =
                    methodSpecification |
                    SpecificationRecipes.FieldOfTypeSpec(typeSpec) |
                    SpecificationRecipes.PropertyOfTypeSpec(typeSpec);

                var firstMember = classSymbol.GetAllMembers().Where(x =>
                {
                    if (x.isInherited && x.member.DeclaredAccessibility == Accessibility.Private)
                        return false;
                    return memberSpecficiation.IsSatisfiedBy(x.member);
                })
                    .Select(x => x.member)
                    .FirstOrDefault();
                
                if (firstMember is { })
                {
                    CacheMemberAccessSource accessSource = methodSpecification.IsSatisfiedBy(firstMember) ? CacheMemberAccessSource.Method : CacheMemberAccessSource.PropertyOrField;
                    collection.SetCacheFromMember(firstMember.Name, accessSource);
                }
                else if (memoryCacheExists)
                {
                    collection.SetCacheFromCustomFactory();
                }
                
                if (collection.CacheAccessStrategy == CacheAccessStrategy.None)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        new DiagnosticDescriptor(DiagnosticIds.Id_003_MemoryCacheRequired,
                            "Microsoft.Extensions.Caching.Memory is required to be installed or an item of type IMemoryCache should be available",
                            "No valid source to provide IMemoryCache available. This required Micorosft.Extensions.Caching.Memory to be installed",
                            "General",
                            DiagnosticSeverity.Error,
                            true),
                        classDeclarationSyntax.GetLocation()));
                    return collection;
                }
               
                foreach (var methodDeclarationSyntax in classMethodGrouping)
                {
                    if (semanticModel.GetDeclaredSymbol(methodDeclarationSyntax) is not {} methodSymbol)
                    {
                        continue;
                    }

                    if (methodSymbol.ReturnsVoid || methodSymbol.ReturnType.Equals(types.Task, SymbolEqualityComparer.Default))
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            new DiagnosticDescriptor(DiagnosticIds.Id_002_NotAllowedOnVoid,
                                "It not allowed to decorate a void method or a method returning a Task",
                                "When decoration a method with the Cacho attribute the method decorated must not return null",
                                "General",
                                DiagnosticSeverity.Error,
                                true),
                            classDeclarationSyntax.GetLocation()));
                    }
                    else
                    {
                        var attribute = methodSymbol.GetAttributes().FirstOrDefault(x =>
                            x.AttributeClass?.ToDisplayString() == $"{Namespace}.{Code.AttributeName}") ??
                                     throw new InvalidOperationException("Attribute should be available");
                        var methodData = new MethodData(methodSymbol, methodDeclarationSyntax, attribute);

                        var cacheEnricher = attribute.GetAttributePropertyValue<string>(Code.CacheEnricherProcessor, null);
                        if (cacheEnricher != null)
                        {
                            var methodSpec = SpecificationRecipes.MethodWithParametersSpec(1);
                            var typeParameterSpec = new TypeOfSpecification(types.CacheEntry).SetExactMatch();

                            var method = classSymbol
                                .GetAllMembers()
                                .Select(x => x.member)
                                .OfType<IMethodSymbol>()
                                .Where(x => x.Name == cacheEnricher)
                                .Where(methodSpec)
                                .FirstOrDefault(m => typeParameterSpec.IsSatisfiedBy(m.Parameters[0].Type));
                            if (method == null)
                            {
                                context.ReportDiagnostic(Diagnostic.Create(
                                    new DiagnosticDescriptor(DiagnosticIds.Id_004_CacheEntryMethodMismatch,
                                        $"No correct method match was found for {Code.CacheEnricherProcessor} {cacheEnricher}",
                                        "This must match a method that takes Exactly 1 parameter of type ICacheEntry",
                                        "General",
                                        DiagnosticSeverity.Error,
                                        true),
                                    methodData.MethodDeclarationSyntax.GetLocation()));
                            }
                            else
                            {
                                methodData.EvaluatedCacheEnricher = new EvaluatedCacheEnricher(method,types);
                            }
                        }
                        


                        collection.AddMethod(methodData);
                    }
                }

                return collection;
            }

            var builder = new ClassesCodeBuilder(types);

            foreach (var item in classes)
            {
               var result = builder.Build(item);
               context.AddSource($"{item.NamedTypeSymbol.Name}.g.cs", SourceText.From(result,Encoding.UTF8));
            }
        }
    }

    
}


internal enum CacheAccessStrategy
{
    None,
    FromMember,
    FromSelfGeneratedFactory
}

internal enum CacheMemberAccessSource
{
    PropertyOrField,
    Method
}

public class MethodData
{
    public IMethodSymbol MethodSymbol { get; }
    public MethodDeclarationSyntax MethodDeclarationSyntax { get; }
    public AttributeData Attribute { get; }

    public MethodData(IMethodSymbol methodSymbol, MethodDeclarationSyntax methodDeclarationSyntax, AttributeData attribute)
    {
        MethodSymbol = methodSymbol;
        MethodDeclarationSyntax = methodDeclarationSyntax;
        Attribute = attribute;
    }
    
    public EvaluatedCacheEnricher? EvaluatedCacheEnricher { get; set; }
}

public class EvaluatedCacheEnricher
{
    private readonly IMethodSymbol _method;
    private readonly LazyTypes _types;

    public bool IsAsync => _method.ReturnType.IsAsync(_types);
    public string MethodName => _method.Name;
    
    public EvaluatedCacheEnricher(IMethodSymbol method, LazyTypes types)
    {
        _method = method;
        _types = types;
    }
}

internal class EvaluatedClassCollection
{
    public ClassDeclarationSyntax ClassDeclaration { get; }
    public INamedTypeSymbol NamedTypeSymbol { get; }

    public ImmutableArray<MethodData> Methods { get; private set; } = ImmutableArray<MethodData>.Empty;

    public EvaluatedClassCollection(ClassDeclarationSyntax classDeclaration, INamedTypeSymbol namedTypeSymbol)
    {
        ClassDeclaration = classDeclaration;
        NamedTypeSymbol = namedTypeSymbol;
    }

    public void SetCacheFromMember(string name, CacheMemberAccessSource accessSource)
    {
        CacheMemberAccessName = name;
        CacheAccessSource = accessSource;
        CacheAccessStrategy = CacheAccessStrategy.FromMember;
    }

    public CacheMemberAccessSource? CacheAccessSource { get; private set; }
    public CacheAccessStrategy CacheAccessStrategy { get; private set; }

    public string? CacheMemberAccessName { get; private set; }

    public void AddMethod(MethodData methodData)
    {
        Methods = Methods.Add(methodData);
    }

    public void SetCacheFromCustomFactory()
    {
        CacheAccessStrategy = CacheAccessStrategy.FromSelfGeneratedFactory;
    }
}