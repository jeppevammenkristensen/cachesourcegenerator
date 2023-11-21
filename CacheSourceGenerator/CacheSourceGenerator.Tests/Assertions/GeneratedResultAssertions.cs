using System.Linq;
using FluentAssertions;
using FluentAssertions.Execution;
using FluentAssertions.Primitives;
using Microsoft.CodeAnalysis;

namespace CacheSourceGenerator.Tests.Assertions;

public class GeneratedResultAssertions : ReferenceTypeAssertions<GeneratorDriverRunResult, GeneratedResultAssertions>
{
    public GeneratedResultAssertions(GeneratorDriverRunResult subject) : base(subject)
    {
    }

    protected override string Identifier => "generatedresult";

    public AndWhichConstraint<GeneratedResultAssertions, SyntaxTree> ContainFile(string fileName, string because = "", params object[] becauseArgs)
    {
        SyntaxTree tree = default(SyntaxTree)!;
        
        var result = Execute.Assertion
            .BecauseOf(because, becauseArgs)
            .ForCondition(!string.IsNullOrEmpty(fileName))
            .FailWith("file name must not be empty")
            .Then
            .Given(() => Subject.GeneratedTrees)
            .ForCondition(trees => trees.Any(x => x.FilePath.EndsWith(fileName)))
            .FailWith("Expected {context:generatedresult} to contain {0}{reason}, but found {1}", _ => fileName,
                files => files.Select(x => x.FilePath));

        if (result == true)
        {
            tree = Subject.GeneratedTrees.Single(x => x.FilePath.EndsWith(fileName));
        }

        return new AndWhichConstraint<GeneratedResultAssertions, SyntaxTree>(this, tree);
    }
}