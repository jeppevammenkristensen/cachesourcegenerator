using System.Linq;
using FluentAssertions;
using FluentAssertions.Execution;
using FluentAssertions.Primitives;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using CSharpExtensions = Microsoft.CodeAnalysis.CSharpExtensions;

namespace CacheSourceGenerator.Tests.Assertions;

public class ClassSyntaxAssertion : ReferenceTypeAssertions<ClassDeclarationSyntax,ClassSyntaxAssertion>
{
    private readonly CompilationUnitSyntax _parent;

    public ClassSyntaxAssertion(ClassDeclarationSyntax subject, CompilationUnitSyntax parent) : base(subject)
    {
        _parent = parent;
    }

    protected override string Identifier => "classsyntax";

    public AndConstraint<ClassSyntaxAssertion> BePublic()
    {
        return ModifierTest(SyntaxKind.PublicKeyword);
    }
    
    public AndConstraint<ClassSyntaxAssertion> BePartial()
    {
        return ModifierTest(SyntaxKind.PartialKeyword);
    }

    private AndConstraint<ClassSyntaxAssertion> ModifierTest(SyntaxKind syntaxKind, string because = "", params object[] becauseArgs)
    {
        Execute.Assertion.BecauseOf(because, becauseArgs)
            .Given(() => Subject.Modifiers)
            .ForCondition(list => list.Any(x => CSharpExtensions.IsKind((SyntaxToken)x, syntaxKind)))
            .FailWith("Expected {context:classsyntax} to have a {0} modifier{reason}, but had {1}", _ => syntaxKind,
                l => l.Select(x => x.ToString()));

        return new AndConstraint<ClassSyntaxAssertion>(this);
    }

    public AndConstraint<ClassSyntaxAssertion> BeStatic()
    {
        return ModifierTest(SyntaxKind.StaticKeyword);
    }

    public AndWhichConstraint<ClassSyntaxAssertion, MethodDeclarationSyntax> ContainMethod(string methodName, string because = "", params object[] becauseArgs)
    {
        var selector = () => Subject.DescendantNodes().OfType<MethodDeclarationSyntax>();
        Execute.Assertion.BecauseOf(because, becauseArgs)
            .ForCondition(!string.IsNullOrEmpty(methodName))
            .FailWith("method name should not empty")
            .Then.Given(selector)
            .ForCondition(m => m.Any(x => x.Identifier.ToString() == methodName))
            .FailWith("Expected {context:classsyntax} to have method with name {0}{reason}, but found {1},\r\n{2}",
                _ => methodName, m => m.Select(x => x.Identifier.ToString()), _ => Subject.ToString());

        return new AndWhichConstraint<ClassSyntaxAssertion, MethodDeclarationSyntax>(this,
            selector().First(x => x.Identifier.ToString() == methodName));
    }
}