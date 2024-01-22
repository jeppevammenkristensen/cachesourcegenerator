using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CacheSourceGenerator.Utilities;

public class IgnoreKeyRemover : CSharpSyntaxRewriter
{
    public override SyntaxNode? VisitAttributeList(AttributeListSyntax node)
    {
        var attributesWithoutIgnoreKey = node.Attributes
            .Where(attribute => attribute.Name.ToString() != "IgnoreKey")
            .ToSeparatedSyntaxList();

        if (attributesWithoutIgnoreKey.Count == 0)
            return null;
            
        return base.VisitAttributeList(node.WithAttributes(attributesWithoutIgnoreKey));
    }
}