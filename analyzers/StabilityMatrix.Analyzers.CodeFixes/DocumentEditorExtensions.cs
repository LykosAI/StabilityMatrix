using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Simplification;

namespace StabilityMatrix.Analyzers.CodeFixes;

public static class DocumentEditorExtensions
{
    public static Task AddUsingDirectiveIfNotPresentAsync(
        this DocumentEditor editor,
        string namespaceName,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrEmpty(namespaceName))
        {
            return Task.CompletedTask;
        }

        // Get the current root from the editor. This reflects all previous changes.
        var currentRoot = editor.GetChangedRoot();
        if (currentRoot is not CompilationUnitSyntax compilationUnit)
        {
            // This might happen if the document isn't C# or some other issue.
            // Consider logging or handling this case.
            return Task.CompletedTask;
        }

        // Check if the using directive already exists.
        var alreadyHasUsing = compilationUnit.Usings.Any(u => u.Name?.ToString() == namespaceName);

        if (alreadyHasUsing)
        {
            return Task.CompletedTask;
        }

        // Create the new using directive.
        var usingDirective = SyntaxFactory
            .UsingDirective(SyntaxFactory.ParseName(namespaceName))
            .WithAdditionalAnnotations(Formatter.Annotation, Simplifier.Annotation)
            .WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed); // Ensure it gets a new line

        // Create a new CompilationUnit with the added using.
        var newCompilationUnit = compilationUnit.AddUsings(usingDirective);

        // It's crucial that 'compilationUnit' (the node being replaced) is the *exact* node
        // that the editor currently considers the root of its tracked changes.
        // 'GetChangedRoot()' should provide this.
        editor.ReplaceNode(compilationUnit, newCompilationUnit);
        return Task.CompletedTask;
    }

    // Optional: A version that takes an INamespaceSymbol
    public static async Task AddUsingDirectiveIfNotPresentAsync(
        this DocumentEditor editor,
        INamespaceSymbol? namespaceSymbol,
        CancellationToken cancellationToken = default
    )
    {
        if (namespaceSymbol == null || IsGlobalNamespace(namespaceSymbol))
        {
            return;
        }
        await editor.AddUsingDirectiveIfNotPresentAsync(namespaceSymbol.ToDisplayString(), cancellationToken);
    }

    private static bool IsGlobalNamespace(INamespaceSymbol? namespaceSymbol)
    {
        return namespaceSymbol == null || namespaceSymbol.IsGlobalNamespace;
    }
}
