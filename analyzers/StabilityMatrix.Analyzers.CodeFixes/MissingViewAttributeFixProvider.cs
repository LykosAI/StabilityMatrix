using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

namespace StabilityMatrix.Analyzers.CodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MissingViewAttributeFixProvider)), Shared]
public class MissingViewAttributeFixProvider : CodeFixProvider
{
    public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create("SM0002");

    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root == null)
            return;

        var diagnostic = context.Diagnostics.First();
        var diagnosticSpan = diagnostic.Location.SourceSpan;

        var viewModelClassDeclaration = root.FindToken(diagnosticSpan.Start)
            .Parent?.AncestorsAndSelf()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault();
        if (viewModelClassDeclaration == null)
            return;

        var semanticModel = await context
            .Document.GetSemanticModelAsync(context.CancellationToken)
            .ConfigureAwait(false);
        if (semanticModel == null)
            return;

        var viewModelSymbol = semanticModel.GetDeclaredSymbol(
            viewModelClassDeclaration,
            context.CancellationToken
        );
        if (viewModelSymbol == null)
            return;

        // Suggestion 1: Add [View(typeof(PlaceholderView))]
        var titleAddAttribute = "Add missing [View(...)] attribute";
        context.RegisterCodeFix(
            CodeAction.Create(
                title: titleAddAttribute,
                createChangedDocument: c =>
                    AddViewAttributeAsync(
                        context.Document,
                        viewModelClassDeclaration,
                        "YourControlTypeHere",
                        c
                    ),
                equivalenceKey: titleAddAttribute + "_placeholder"
            ),
            diagnostic
        );

        // Suggestion 2: Find potential matching views based on naming convention
        var potentialViews = await FindPotentialViewsAsync(
            context.Document.Project.Solution,
            viewModelSymbol,
            context.CancellationToken
        );
        foreach (var potentialView in potentialViews)
        {
            var titleAddSpecificAttribute = $"Add [View(typeof({potentialView.Name}))]";
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: titleAddSpecificAttribute,
                    createChangedDocument: c =>
                        AddViewAttributeAsync(
                            context.Document,
                            viewModelClassDeclaration,
                            potentialView.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                            c
                        ),
                    equivalenceKey: titleAddSpecificAttribute + "_" + potentialView.Name
                ),
                diagnostic
            );
        }

        // Suggestion 3: Change base class (if applicable and makes sense)
        var observableObjectSymbol = semanticModel.Compilation.GetTypeByMetadataName(
            "CommunityToolkit.Mvvm.ComponentModel.ObservableObject"
        );
        if (
            observableObjectSymbol != null
            && viewModelSymbol.BaseType != null
            && !SymbolEqualityComparer.Default.Equals(viewModelSymbol.BaseType, observableObjectSymbol)
        )
        {
            // This is a more complex change and might not always be desired.
            // Consider if the ViewModel truly doesn't need the ViewModelBase lifecycle.
            var titleChangeBase =
                $"Change base class to ObservableObject (if lifecycle methods are not needed)";
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: titleChangeBase,
                    createChangedDocument: c =>
                        ChangeBaseClassAsync(
                            context.Document,
                            viewModelClassDeclaration,
                            observableObjectSymbol,
                            c
                        ),
                    equivalenceKey: titleChangeBase
                ),
                diagnostic
            );
        }
    }

    private async Task<Document> AddViewAttributeAsync(
        Document document,
        ClassDeclarationSyntax classDecl,
        string controlTypeName,
        CancellationToken cancellationToken
    )
    {
        var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);

        var attributeArgument = SyntaxFactory.AttributeArgument(
            SyntaxFactory.TypeOfExpression(SyntaxFactory.ParseTypeName(controlTypeName))
        );
        var attribute = SyntaxFactory
            .Attribute(SyntaxFactory.IdentifierName("View"))
            .WithArgumentList(
                SyntaxFactory.AttributeArgumentList(SyntaxFactory.SingletonSeparatedList(attributeArgument))
            );

        editor.AddAttribute(classDecl, attribute);

        // Add using for ViewAttribute if necessary
        var viewAttributeNamespace = "StabilityMatrix.Core.Attributes"; // Adjust if different
        if (!HasUsingDirective(editor.OriginalRoot, viewAttributeNamespace))
        {
            // editor.AddUsingDirective(SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(viewAttributeNamespace)));
        }
        // Potentially add using for controlTypeName's namespace if it's fully qualified

        return editor.GetChangedDocument();
    }

    private async Task<Document> ChangeBaseClassAsync(
        Document document,
        ClassDeclarationSyntax classDecl,
        INamedTypeSymbol newBaseTypeSymbol,
        CancellationToken cancellationToken
    )
    {
        var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
        var newBaseTypeName = SyntaxFactory.ParseTypeName(
            newBaseTypeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
        );

        if (classDecl.BaseList == null || !classDecl.BaseList.Types.Any())
        {
            editor.ReplaceNode(
                classDecl,
                classDecl.WithBaseList(
                    SyntaxFactory.BaseList(
                        SyntaxFactory.SingletonSeparatedList<BaseTypeSyntax>(
                            SyntaxFactory.SimpleBaseType(newBaseTypeName)
                        )
                    )
                )
            );
        }
        else
        {
            // Replace the first base type (assuming ViewModelBase or its derivative was first)
            var existingBaseList = classDecl.BaseList;
            var types = existingBaseList.Types.ToList();
            if (types.Any())
            {
                types[0] = SyntaxFactory.SimpleBaseType(newBaseTypeName); // Replace first base type
                editor.ReplaceNode(
                    existingBaseList,
                    existingBaseList.WithTypes(SyntaxFactory.SeparatedList(types))
                );
            }
        }

        // Add using for newBaseTypeSymbol's namespace if necessary
        var newBaseNamespace = newBaseTypeSymbol.ContainingNamespace.ToDisplayString();
        if (!HasUsingDirective(editor.OriginalRoot, newBaseNamespace))
        {
            // editor.AddUsingDirective(SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(newBaseNamespace)));
        }

        return editor.GetChangedDocument();
    }

    private async Task<IEnumerable<INamedTypeSymbol>> FindPotentialViewsAsync(
        Solution solution,
        INamedTypeSymbol viewModelSymbol,
        CancellationToken cancellationToken
    )
    {
        var potentialViews = new List<INamedTypeSymbol>();
        var viewModelName = viewModelSymbol.Name;

        if (!viewModelName.EndsWith("ViewModel"))
            return potentialViews;

        var baseName = viewModelName.Substring(0, viewModelName.Length - "ViewModel".Length);
        var possibleViewNames = new[]
        {
            baseName,
            baseName + "Page",
            baseName + "View",
            baseName + "Dialog",
            baseName + "Window",
            baseName + "Control",
            baseName + "Card", // From your example ExtraNetworkCard
        };

        // This can be slow on large solutions. Consider optimizing or scoping search.
        foreach (var project in solution.Projects)
        {
            if (cancellationToken.IsCancellationRequested)
                break;
            var compilation = await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
            if (compilation == null)
                continue;

            foreach (var viewName in possibleViewNames)
            {
                // A more robust search would iterate all INamedTypeSymbols in the compilation
                // and check their names and if they are suitable (e.g., derive from Control).
                // GetTypeByMetadataName is for fully qualified names.
                // We'll do a simpler name check for now.
                var symbols = compilation
                    .GlobalNamespace.GetAllTypes(cancellationToken)
                    .Where(s =>
                        s.Name == viewName && s.CanBeReferencedByName && s.TypeKind == TypeKind.Class
                    );

                potentialViews.AddRange(symbols);
            }
        }
        return potentialViews.Distinct(SymbolEqualityComparer.Default).OfType<INamedTypeSymbol>(); // Ensure distinct
    }

    private bool HasUsingDirective(SyntaxNode root, string namespaceName)
    {
        return root.DescendantNodes()
            .OfType<UsingDirectiveSyntax>()
            .Any(u => u.Name?.ToString() == namespaceName);
    }
}

// Helper extension for GetAllTypes
internal static class NamespaceSymbolExtensions
{
    internal static IEnumerable<INamedTypeSymbol> GetAllTypes(
        this INamespaceSymbol namespaceSymbol,
        CancellationToken cancellationToken
    )
    {
        var types = new List<INamedTypeSymbol>();
        var queue = new Queue<INamespaceOrTypeSymbol>();
        queue.Enqueue(namespaceSymbol);

        while (queue.Count > 0)
        {
            if (cancellationToken.IsCancellationRequested)
                break;
            var current = queue.Dequeue();
            foreach (var member in current.GetMembers())
            {
                if (member is INamespaceSymbol ns)
                {
                    queue.Enqueue(ns);
                }
                else if (member is INamedTypeSymbol ts)
                {
                    types.Add(ts);
                    // Also enqueue nested types if any
                    foreach (var nestedType in ts.GetTypeMembers())
                    {
                        queue.Enqueue(nestedType);
                    }
                }
            }
        }
        return types;
    }
}
