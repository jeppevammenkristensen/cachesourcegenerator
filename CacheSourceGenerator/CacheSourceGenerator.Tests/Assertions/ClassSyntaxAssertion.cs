using System.Linq;
using FluentAssertions;
using FluentAssertions.Execution;
using FluentAssertions.Primitives;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using CSharpExtensions = Microsoft.CodeAnalysis.CSharpExtensions;

namespace CacheSourceGenerator.Tests.Assertions;

public class MethodSyntaxAssertion : ReferenceTypeAssertions<MethodDeclarationSyntax, MethodSyntaxAssertion>
{
    public MethodSyntaxAssertion(MethodDeclarationSyntax subject) : base(subject)
    {
    }

    protected override string Identifier => "methodsyntax";

    public AndConstraint<MethodSyntaxAssertion> HaveReturnType(string returnType, string because = "",
        params object[] becauseArgs)
    {
        Execute.Assertion.BecauseOf(because, becauseArgs)
            .ForCondition(!string.IsNullOrEmpty(returnType))
            .FailWith("Returntype should not be empty")
            .Then
            .ForCondition(Subject.ReturnType.ToString() == returnType)
            .FailWith("Expected {container:methodsyntax} to have return type {0}{reason}, but was {1},\r\n{2}",
                returnType, Subject.ReturnType.ToString(), Subject.ToString());

        return new AndConstraint<MethodSyntaxAssertion>(this);
    }
    
    public AndConstraint<MethodSyntaxAssertion> HaveParameters((string type, string name)[] parameters, string because = "", params object[] becauseArgs)
    {
        Execute.Assertion.BecauseOf(because, becauseArgs)
            .ForCondition(parameters != null)
            .FailWith("Parameters should not be null")
            .Then.Given(() =>
                Subject.ParameterList.Parameters
                    .Select(x => (x.Type?.ToString() ?? string.Empty, x.Identifier.ToString())).ToList())
            .ForCondition(methodParameters => methodParameters.SequenceEqual(parameters!))
            .FailWith("Expected {container:methodsyntax} to have parameters {0}{reason}, but was {1},\r\n{2}",
                _ => parameters.Select(x => $"{x.type} {x.name}"), 
                m => m.Select(x => $"{x.Item1} {x.Item2}"), 
                _ => Subject.ToString());

        return new AndConstraint<MethodSyntaxAssertion>(this);
    }

    public AndConstraint<MethodSyntaxAssertion> HaveBodyContaining(string bodyText, string because= "", params object[] becauseArgs)
    {
        Execute.Assertion.BecauseOf(because, becauseArgs)
            .ForCondition(!string.IsNullOrEmpty(bodyText))
            .FailWith("bodyText should not be empty")
            .Then.Given(() => Subject.Body?.ToString() ?? string.Empty)
            .ForCondition(body => body.Contains(bodyText))
            .FailWith("Expected {context:methodsyntax} to have body containing {0}{reason}, but body was {1}",
                _ => bodyText, b => b);

        return new AndConstraint<MethodSyntaxAssertion>(this);
    }

    public AndConstraint<MethodSyntaxAssertion> BeAsync(string because = "", params object[] becauseArgs)
    {
        return ModifierTest(SyntaxKind.AsyncKeyword, because, becauseArgs);
    }
    
    private AndConstraint<MethodSyntaxAssertion> ModifierTest(SyntaxKind syntaxKind, string because = "", params object[] becauseArgs)
    {
        Execute.Assertion.BecauseOf(because, becauseArgs)
            .Given(() => Subject.Modifiers)
            .ForCondition(list => list.Any(x => CSharpExtensions.IsKind((SyntaxToken)x, syntaxKind)))
            .FailWith("Expected {context:methodsyntax} to have a {0} modifier{reason}, but had {1}\r\n{2}", _ => syntaxKind,
                l => l.Select(x => x.ToString()), _ => Subject.ToString());

        return new AndConstraint<MethodSyntaxAssertion>(this);
    }
}


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