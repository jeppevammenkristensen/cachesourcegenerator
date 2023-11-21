using System.Linq;
using System.Threading;
using FluentAssertions;
using FluentAssertions.Execution;
using FluentAssertions.Primitives;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CacheSourceGenerator.Tests.Assertions;

public class SyntaxTreeAssertion : ReferenceTypeAssertions<SyntaxTree, SyntaxTreeAssertion>
{
    private readonly CompilationUnitSyntax _compilationUnitSyntax;

    public SyntaxTreeAssertion(SyntaxTree subject) : base(subject)
    {
        _compilationUnitSyntax = subject.GetCompilationUnitRoot();
    }

    public AndConstraint<SyntaxTreeAssertion> ContainUsing(string usingStatement, string? because = "", params object[] becauseArgs)
    {
        Execute.Assertion.BecauseOf(because, becauseArgs)
            .ForCondition(!string.IsNullOrEmpty(usingStatement))
            .FailWith("namespace must be empty")
            .Then
            .Given(() => _compilationUnitSyntax.Usings)
            .ForCondition(usings => usings.Any(x => x.Name?.ToString().Equals(usingStatement) == true))
            .FailWith("Expected {context:syntaxtree} to contain {0}{reason}, but found {1}.", _ => usingStatement,
                usings => usings.ToString());

        return new AndConstraint<SyntaxTreeAssertion>(this);

    }

    public AndWhichConstraint<SyntaxTreeAssertion, ClassDeclarationSyntax> ContainClass(string className, string because = "", params object[] becauseArgs)
    {
        ClassDeclarationSyntax cls = default(ClassDeclarationSyntax)!;
        
        var result = Execute.Assertion.BecauseOf(because, becauseArgs)
            .ForCondition(!string.IsNullOrEmpty(className))
            .FailWith("className must not be empty")
            .Then
            .Given(() => _compilationUnitSyntax.DescendantNodes().OfType<ClassDeclarationSyntax>())
            .ForCondition(classes => classes.Any(x => x.Identifier.ToString() == className))
            .FailWith("Expected {context:syntaxtree} to contain {0}{reason}, but found {1}\r\n{2}", _ => className,
                us => us.Select(x => x.Identifier.ToString()),_ => _compilationUnitSyntax.ToString());

        if (result)
        {
            cls = _compilationUnitSyntax.DescendantNodes().OfType<ClassDeclarationSyntax>()
                .First(x => x.Identifier.ToString() == className);
        }

        return new AndWhichConstraint<SyntaxTreeAssertion, ClassDeclarationSyntax>(this, cls);


    }

    public AndConstraint<SyntaxTreeAssertion> ContainNamespace(string @namespace, string? because = "",
        params object[] becauseArgs)
    {
        Execute.Assertion.BecauseOf(because, becauseArgs)
            .ForCondition(!string.IsNullOrEmpty(@namespace))
            .FailWith("namespace can not be empty")
            .Then
            .Given(() => _compilationUnitSyntax.Members.OfType<BaseNamespaceDeclarationSyntax>())
            .ForCondition(namespaces => namespaces.Any(x => x.Name.ToString() == @namespace))
            .FailWith("Expected {context:syntaxtree} to contain {0}{reason}, but found {1}\r\n{2}", _ => @namespace,
                n => n.Select(x => x.Name.ToString()), _ => _compilationUnitSyntax.ToString());
        return new AndConstraint<SyntaxTreeAssertion>(this);
    }
    
    protected override string Identifier => "syntaxtree";


    public AndConstraint<SyntaxTreeAssertion> NotContainClass(string className, string because = "", params object[] becauseArgs)
    {
        var selector = () => _compilationUnitSyntax.DescendantNodes().OfType<ClassDeclarationSyntax>();
        Execute.Assertion.BecauseOf(because, becauseArgs)
            .ForCondition(!string.IsNullOrEmpty(className))
            .FailWith("className should not be empty")
            .Then
            .Given(selector)
            .ForCondition(cls => cls.All(x => x.Identifier.ToString() != className))
            .FailWith("Expected {context:syntaxtree} to not containt {0}{reason}\r\n{1}", _ => className,
                _ => Subject.ToString());

        return new AndConstraint<SyntaxTreeAssertion>(this);
    }
}