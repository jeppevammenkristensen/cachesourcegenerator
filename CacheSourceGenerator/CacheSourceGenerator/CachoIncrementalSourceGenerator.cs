using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
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
                                                        public string {{Code.MethodName}} { get;init; } = default!;
                                                        
                                                        /// <summary>
                                                        /// The name of a method in the current class that takes
                                                        /// an CacheEntry and processes it 
                                                        /// </summary>
                                                        public string? {{Code.CacheEnricherProcessor}} { get;set; }
                                                        
                                                        
                                                        /// <summary>
                                                         /// The name of a method in the current class that can 
                                                         /// generate a custom cache key. The method must take the same parameters
                                                         /// as the method being decorated. But can return any type.
                                                         /// </summary>
                                                        public string? {{Code.KeyGenerator}} {get;set;}
                                                        
                                                        /// <summary>
                                                        /// Set this to true to not generate an evict method
                                                        /// </summary>
                                                        public bool {{Code.SuppressEvictMethod}} {get;set;}
                                                     }
                                                 }
                                                 """;

    private const string IgnoreAttributeSourceCode = $$"""
                                                       // <auto-generated/>
                                                       #nullable enable
                                                       namespace {{Namespace}}
                                                       {
                                                           [System.AttributeUsage(System.AttributeTargets.Parameter)]
                                                           public class {{Code.IgnoreAttributeName}} : System.Attribute
                                                           {
                                                               
                                                           }
                                                       }
                                                       """;


    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Add the marker attribute to the compilation.
        context.RegisterPostInitializationOutput(ctx =>
        {
            ctx.AddSource(
                "GenerateMemoryCache.g.cs",
                SourceText.From(AttributeSourceCode, Encoding.UTF8));
            ctx.AddSource("IgnoreKey.g.cs", SourceText.From(IgnoreAttributeSourceCode, Encoding.UTF8));
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
                        "When decorating a method with the GenerateMemoryCache attribute the containing class must be partial",
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
            if (EvaluateClass(grouping, classSymbol, types, context, classDeclarationSyntax, semanticModel) is { Methods.Length : > 0 } evaluated)
            {
                classes.Add(evaluated);
            }

            var builder = new ClassesCodeBuilder(types);

            foreach (var item in classes)
            {
               var result = builder.Build(item);
               context.AddSource($"{item.NamedTypeSymbol.Name}.g.cs", SourceText.From(result,Encoding.UTF8));
            }
        }
    }

    /// <summary>
    /// Evaluates a class and its methods for caching attributes.
    /// </summary>
    /// <param name="classMethodGrouping">The grouping of class and method declarations.</param>
    /// <param name="classSymbol">The symbol representing the class.</param>
    /// <param name="types">The lazy types.</param>
    /// <param name="context">The source production context.</param>
    /// <param name="classDeclarationSyntax">The class declaration syntax.</param>
    /// <param name="semanticModel">The semantic model.</param>
    /// <returns>An instance of EvaluatedClassCollection.</returns>
    private EvaluatedClassCollection EvaluateClass(IGrouping<ClassDeclarationSyntax, MethodDeclarationSyntax> classMethodGrouping, INamedTypeSymbol classSymbol, LazyTypes types, SourceProductionContext context, ClassDeclarationSyntax? classDeclarationSyntax, SemanticModel? semanticModel)
    {
        EvaluatedClassCollection collection = new EvaluatedClassCollection(classMethodGrouping.Key, classSymbol);
                
        // Check if the Microsoft.Extensions.Caching.Memory assembly is references
        var memoryCacheExists =
            classSymbol.ContainingModule.ReferencedAssemblies.Any(x =>
                x.Name == "Microsoft.Extensions.Caching.Memory");

        // Check if any valid members can provide a memorycache
        var typeSpec = new TypeOfSpecification(types.MemoryCache);
        var methodSpecification = SpecificationRecipes.MethodWithNoParametersSpec.WithTypeSpec(typeSpec);
                
        var memberSpecification =
            methodSpecification |
            SpecificationRecipes.FieldOfTypeSpec(typeSpec) |
            SpecificationRecipes.PropertyOfTypeSpec(typeSpec);

        var firstMember = classSymbol.GetAllMembers().Where(x =>
            {
                if (x.isInherited && x.member.DeclaredAccessibility == Accessibility.Private)
                    return false;
                return memberSpecification.IsSatisfiedBy(x.member);
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
                    "No valid source to provide IMemoryCache available. It is required to install Microsoft.Extensions.Caching.Memory to be installed",
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
                var methodData = new MethodData(methodSymbol, methodDeclarationSyntax, attribute, types);

                HandleCacheEnricher(context, attribute, types, classSymbol, methodData);
                HandleKeyGenerator(context, attribute, types, classSymbol, methodData);
                collection.AddMethod(methodData);
            }
        }

        return collection;
    }

    /// <summary>
    /// Handles checking for the <see cref="Code.CacheEnricherProcessor"/> attribute and
    /// adding relevant data
    /// </summary>
    /// <param name="context">The source production context.</param>
    /// <param name="attribute">The cache enricher attribute data.</param>
    /// <param name="types">The lazy types.</param>
    /// <param name="classSymbol">The class symbol.</param>
    /// <param name="methodData">The method data.</param>
    private static void HandleCacheEnricher(SourceProductionContext context, AttributeData attribute, LazyTypes types,
        INamedTypeSymbol classSymbol, MethodData methodData)
    {
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
    }
    
    private static void HandleKeyGenerator(SourceProductionContext context, AttributeData attribute, LazyTypes types,
        INamedTypeSymbol classSymbol, MethodData methodData)
    {
        var keyGenerator = attribute.GetAttributePropertyValue<string>(Code.KeyGenerator, null);
        if (keyGenerator != null)
        {
            var methodSpec = SpecificationRecipes.KeyGeneratorMatch(methodData.MethodSymbol);

            var candidates = classSymbol
                .GetAllMembers()
                .Select(x => x.member)
                .OfType<IMethodSymbol>()
                .Where(x => x.Name == keyGenerator).ToImmutableArray();


            if (candidates.Length == 0)
            {
                context.ReportDiagnostic(Diagnostic.Create(new DiagnosticDescriptor(
                        DiagnosticIds.Id_005_KeyGeneratorNoCandidates,
                        $"No candidates found matching the name {keyGenerator}",
                        $"Was not able to find a candidate that has the name {keyGenerator}",
                        "General",
                        DiagnosticSeverity.Error,
                        true),
                    methodData.MethodDeclarationSyntax.GetLocation()));
                return;
            }

            var match = candidates.FirstOrDefault(methodSpec);
                
            if (match == null)
            {
                context.ReportDiagnostic(Diagnostic.Create(new DiagnosticDescriptor(
                        DiagnosticIds.Id_006_KeyGeneratorNoParameterMatch,
                        $"No candidates with correct amount of parameters found for {keyGenerator}",
                        $"Was not able to find a candidate with the name {keyGenerator} that has the parameters match",
                        "General",
                        DiagnosticSeverity.Error,
                        true),
                    methodData.MethodDeclarationSyntax.GetLocation()));
            }
            else
            {
                methodData.EvaluatedKeyGenerator = new EvaluatedKeyGenerator(match,types);
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

/// <summary>
/// Represents data related to a method.
/// </summary>
public class MethodData
{
    
    private readonly LazyTypes _types;
    public IMethodSymbol MethodSymbol { get; }
    public MethodDeclarationSyntax MethodDeclarationSyntax { get; }
    public AttributeData Attribute { get; }

    public string OnCallingMethodName => $"OnCalling{MethodSymbol.Name}";
    public string OnCalledMethodName => $"OnCalled{MethodSymbol.Name}";

    /// <summary>
    /// Gets a value indicating whether the caller is an asynchronous method.
    /// </summary>
    /// <remarks>
    /// The value of this property is determined by checking if the <see cref="MethodSymbol"/> is an asynchronous method
    /// that returns a result.
    /// </remarks>
    /// <value>
    /// <c>true</c> if the caller is an asynchronous method; otherwise, <c>false</c>.
    /// </value>
    public bool CallerIsAsync => MethodSymbol.IsAsyncWithResult(_types);
    
    public bool ReturnTypeIsNullable => MethodSymbol.ReturnType.IsNullable(_types);

    public MethodData(IMethodSymbol methodSymbol, MethodDeclarationSyntax methodDeclarationSyntax,
        AttributeData attribute, LazyTypes types)
    {
        _types = types;
        MethodSymbol = methodSymbol;
        MethodDeclarationSyntax = methodDeclarationSyntax;
        Attribute = attribute;
        _lazyUnderlyingType = new Lazy<ITypeSymbol>(() => MethodSymbol.ReturnType.GetUnderlyingType(_types));
    }

    private readonly Lazy<ITypeSymbol> _lazyUnderlyingType;

    /// <summary>
    /// Gets or sets the EvaluatedCacheEnricher property.
    /// </summary>
    /// <remarks>
    /// This property represents the evaluated cache enricher used for caching and enriching data.
    /// The property allows for storing and retrieving instances of the <see cref="EvaluatedCacheEnricher"/> class.
    /// </remarks>
    /// <value>
    /// The EvaluatedCacheEnricher instance or null if not set.
    /// </value>
    public EvaluatedCacheEnricher? EvaluatedCacheEnricher { get; set; }

    /// <summary>
    /// Gets or sets the evaluated key generator for this property.
    /// </summary>
    public EvaluatedKeyGenerator? EvaluatedKeyGenerator { get; set; }

    public ITypeSymbol UnderlyingType => _lazyUnderlyingType.Value;

    /// <summary>
    /// Retrieves the parameters for the key generator.
    /// </summary>
    /// <returns>A read-only collection of parameter names.</returns>
    public ReadOnlyCollection<string> GetParametersForKeyGenerator()
    {
        var parameterSymbols = MethodSymbol
            .Parameters.Where(x => x.GetAttributes().Length == 0 || !x.GetAttributes().Any(y => y.AttributeClass?.Equals(_types.IgnoreKeyAttribute, SymbolEqualityComparer.Default) == true));

        return new ReadOnlyCollection<string>(parameterSymbols.Select(x => x.Name).ToList());
    }

    public TypeSyntax UnderlyingTypeAsTypeSyntax()
    {
        if (UnderlyingType.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is not TypeSyntax typeSyntax)
        {
            typeSyntax = SyntaxFactory.ParseTypeName(UnderlyingType.ToDisplayString());
        }
        
        if (UnderlyingType is {IsValueType: true} && ReturnTypeIsNullable)
        {
            return SyntaxFactory.NullableType(typeSyntax);
        }

        return typeSyntax;
    }
}

public abstract class EvaluatedMethod
{
    protected readonly IMethodSymbol _method;
    protected readonly LazyTypes _lazyTypes;

    /// <summary>
    /// Initializes a new instance of the <see cref="EvaluatedMethod"/> class with the specified method symbol and lazy types.
    /// </summary>
    /// <param name="method">The method symbol representing the evaluated method.</param>
    /// <param name="lazyTypes">The lazy types used for evaluation.</param>
    protected EvaluatedMethod(IMethodSymbol method, LazyTypes lazyTypes)
    {
        _method = method;
        _lazyTypes = lazyTypes;
    }

    public bool IsAsync => _method.ReturnType.IsAsync(_lazyTypes);
    public string MethodName => _method.Name;
    public bool IsStatic => _method.IsStatic;
    
}

public class EvaluatedKeyGenerator : EvaluatedMethod
{
    public EvaluatedKeyGenerator(IMethodSymbol method, LazyTypes lazyTypes) : base(method, lazyTypes)
    {
    }
}

public class EvaluatedCacheEnricher : EvaluatedMethod
{
    public EvaluatedCacheEnricher(IMethodSymbol method, LazyTypes lazyTypes) : base(method, lazyTypes)
    {
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