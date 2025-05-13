using System.Collections.Immutable;
using System.Composition;
using System.Runtime.Serialization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Simplification;
using Formatter = Microsoft.CodeAnalysis.Formatting.Formatter;

namespace StabilityMatrix.Analyzers.CodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ControlMustInheritBaseFixProvider)), Shared]
public class ControlMustInheritBaseFixProvider : CodeFixProvider
{
    public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create("SM0001");

    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root == null)
            return;

        var diagnostic = context.Diagnostics.First();
        var diagnosticSpan = diagnostic.Location.SourceSpan;

        if (
            root.FindNode(diagnosticSpan, getInnermostNodeForTie: true)
            is not TypeOfExpressionSyntax typeOfExpression
        )
            return;

        var semanticModel = await context
            .Document.GetSemanticModelAsync(context.CancellationToken)
            .ConfigureAwait(false);
        if (semanticModel == null)
            return;

        var controlTypeSymbol =
            semanticModel.GetSymbolInfo(typeOfExpression.Type, context.CancellationToken).Symbol
            as INamedTypeSymbol;
        if (controlTypeSymbol == null || controlTypeSymbol.DeclaringSyntaxReferences.IsDefaultOrEmpty)
            return;

        // --- CRITICAL CHANGE: Get the Document for the Control Type ---
        var controlSyntaxRef = controlTypeSymbol.DeclaringSyntaxReferences.FirstOrDefault();
        if (controlSyntaxRef == null)
            return;

        var controlDocument = context.Document.Project.Solution.GetDocument(controlSyntaxRef.SyntaxTree);
        if (controlDocument == null)
            return;
        // --- END CRITICAL CHANGE ---

        var userControlSymbol = semanticModel.Compilation.GetTypeByMetadataName(
            "Avalonia.Controls.UserControl"
        );
        var templatedControlSymbol = semanticModel.Compilation.GetTypeByMetadataName(
            "Avalonia.Controls.TemplatedControl"
        );

        var suggestedBaseTypeName = "UserControlBase";
        var suggestedBaseTypeFullName = "StabilityMatrix.Avalonia.Controls.UserControlBase";

        if (templatedControlSymbol != null && DoesInheritFrom(controlTypeSymbol, templatedControlSymbol))
        {
            suggestedBaseTypeName = "TemplatedControlBase";
            suggestedBaseTypeFullName = "StabilityMatrix.Avalonia.Controls.TemplatedControlBase";
        }

        var title = $"Make '{controlTypeSymbol.Name}' inherit from {suggestedBaseTypeName}";
        context.RegisterCodeFix(
            CodeAction.Create(
                title: title,
                // Pass the controlDocument to the fix method
                createChangedSolution: c =>
                    MakeControlInheritBaseAsync(
                        controlDocument,
                        controlTypeSymbol,
                        suggestedBaseTypeFullName,
                        c
                    ),
                equivalenceKey: title
            ),
            diagnostic
        );
    }

    // MakeControlInheritBaseAsync now takes a Document parameter which is the control's document
    private async Task<Solution> MakeControlInheritBaseAsync(
        Document controlDocument,
        INamedTypeSymbol controlTypeSymbol,
        string suggestedBaseTypeFullName,
        CancellationToken cancellationToken
    )
    {
        var editor = await DocumentEditor
            .CreateAsync(controlDocument, cancellationToken)
            .ConfigureAwait(false);
        var semanticModel = await controlDocument
            .GetSemanticModelAsync(cancellationToken)
            .ConfigureAwait(false);
        if (semanticModel == null)
            return controlDocument.Project.Solution;

        if (
            controlTypeSymbol.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax(cancellationToken)
            is not ClassDeclarationSyntax controlDeclarationSyntaxFromSymbol
        )
            return controlDocument.Project.Solution;

        var classNodeToModify = editor
            .OriginalRoot.DescendantNodesAndSelf()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(c =>
                c.Span == controlDeclarationSyntaxFromSymbol.Span
                && c.IsEquivalentTo(controlDeclarationSyntaxFromSymbol)
            );

        if (classNodeToModify == null)
            return controlDocument.Project.Solution;

        var suggestedBaseSymbol = semanticModel.Compilation.GetTypeByMetadataName(suggestedBaseTypeFullName);
        if (suggestedBaseSymbol == null)
            return controlDocument.Project.Solution;

        // If it already inherits the target, no change needed.
        if (DoesInheritFrom(controlTypeSymbol, suggestedBaseSymbol))
        {
            return controlDocument.Project.Solution;
        }

        var finalClassNode = classNodeToModify; // Start with the original
        var baseListModified = false;

        if (classNodeToModify.BaseList != null && classNodeToModify.BaseList.Types.Any())
        {
            var existingBaseList = classNodeToModify.BaseList;
            var newTypes = new List<BaseTypeSyntax>();
            var replacedExisting = false;

            var typeToReplaceSimpleName = "";
            var replacementSimpleName = "";

            if (suggestedBaseTypeFullName.EndsWith("UserControlBase"))
            {
                typeToReplaceSimpleName = "UserControl";
                replacementSimpleName = "UserControlBase";
            }
            else if (suggestedBaseTypeFullName.EndsWith("TemplatedControlBase"))
            {
                typeToReplaceSimpleName = "TemplatedControl";
                replacementSimpleName = "TemplatedControlBase";
            }

            if (!string.IsNullOrEmpty(typeToReplaceSimpleName))
            {
                foreach (var baseTypeSyntax in existingBaseList.Types)
                {
                    // Check if the current baseTypeSyntax's Type is a SimpleNameSyntax matching typeToReplaceSimpleName
                    if (
                        baseTypeSyntax.Type is SimpleNameSyntax simpleName
                        && simpleName.Identifier.ValueText == typeToReplaceSimpleName
                    )
                    {
                        // Replace it
                        var replacementIdentifier = SyntaxFactory
                            .IdentifierName(replacementSimpleName)
                            .WithLeadingTrivia(simpleName.GetLeadingTrivia()) // Preserve trivia
                            .WithTrailingTrivia(simpleName.GetTrailingTrivia());
                        newTypes.Add(SyntaxFactory.SimpleBaseType(replacementIdentifier));
                        replacedExisting = true;
                        baseListModified = true;
                    }
                    // Check if it's a QualifiedNameSyntax ending with the typeToReplaceSimpleName
                    else if (
                        baseTypeSyntax.Type is QualifiedNameSyntax { Right: { } rightName }
                        && rightName.Identifier.ValueText == typeToReplaceSimpleName
                    )
                    {
                        // More complex: replace Namespace.UserControl with Namespace.UserControlBase
                        // Or, ideally, just use the minimally qualified name here too.
                        // For simplicity, let's assume we aim for simple names in base list and handle with usings.
                        var replacementIdentifier = SyntaxFactory
                            .IdentifierName(replacementSimpleName)
                            .WithLeadingTrivia(baseTypeSyntax.Type.GetLeadingTrivia())
                            .WithTrailingTrivia(baseTypeSyntax.Type.GetTrailingTrivia());
                        newTypes.Add(SyntaxFactory.SimpleBaseType(replacementIdentifier));
                        replacedExisting = true;
                        baseListModified = true;
                    }
                    else
                    {
                        newTypes.Add(baseTypeSyntax); // Keep other bases
                    }
                }
            }

            if (replacedExisting)
            {
                finalClassNode = classNodeToModify.WithBaseList(
                    existingBaseList.WithTypes(SyntaxFactory.SeparatedList(newTypes))
                );
            }
            else // Didn't replace, so add the suggested base type (if not already present)
            {
                var minimallyQualifiedSuggestedName = suggestedBaseSymbol.ToDisplayString(
                    SymbolDisplayFormat.MinimallyQualifiedFormat
                );
                var alreadyPresent = existingBaseList.Types.Any(bt =>
                    bt.Type.ToString() == minimallyQualifiedSuggestedName
                );
                if (!alreadyPresent)
                {
                    var newSimpleBaseType = SyntaxFactory.SimpleBaseType(
                        SyntaxFactory.ParseTypeName(minimallyQualifiedSuggestedName)
                    );
                    finalClassNode = classNodeToModify.WithBaseList(
                        existingBaseList.AddTypes(newSimpleBaseType)
                    );
                    baseListModified = true;
                }
            }
        }
        else // No base list, add a new one
        {
            var newSimpleBaseType = SyntaxFactory.SimpleBaseType(
                SyntaxFactory.ParseTypeName(
                    suggestedBaseSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)
                )
            );
            finalClassNode = classNodeToModify.WithBaseList(
                SyntaxFactory.BaseList(
                    SyntaxFactory.SingletonSeparatedList<BaseTypeSyntax>(newSimpleBaseType)
                )
            );
            baseListModified = true;
        }

        if (baseListModified)
        {
            // Add Formatter.Annotation to help with curly brace placement
            editor.ReplaceNode(
                classNodeToModify,
                finalClassNode.WithAdditionalAnnotations(Formatter.Annotation)
            );
        }

        // --- Add Using Directive ---
        if (
            suggestedBaseSymbol.ContainingNamespace != null
            && !IsGlobalNamespace(suggestedBaseSymbol.ContainingNamespace)
        )
        {
            // await editor.AddUsingDirectiveIfNotPresentAsync(suggestedBaseSymbol.ContainingNamespace, cancellationToken);
        }

        var finalDocument = editor.GetChangedDocument();

        // Simplifier should ensure minimally qualified names are used where possible
        var simplifiedDocument = await Simplifier
            .ReduceAsync(
                finalDocument,
                await controlDocument.GetOptionsAsync(cancellationToken),
                cancellationToken
            )
            .ConfigureAwait(false);

        return simplifiedDocument.Project.Solution;
    }

    private bool IsGlobalNamespace(INamespaceSymbol? namespaceSymbol)
    {
        return namespaceSymbol == null || namespaceSymbol.IsGlobalNamespace;
    }

    private bool HasUsingDirective(CompilationUnitSyntax root, string namespaceName)
    {
        return root.Usings.Any(u => u.Name?.ToString() == namespaceName);
    }

    private static bool DoesInheritFrom(INamedTypeSymbol? type, INamedTypeSymbol? baseType)
    {
        // ... (implementation remains the same)
        if (type == null || baseType == null)
            return false;
        if (SymbolEqualityComparer.Default.Equals(type, baseType))
            return true;
        var currentBase = type.BaseType;
        while (currentBase != null)
        {
            if (SymbolEqualityComparer.Default.Equals(currentBase, baseType))
                return true;
            if (currentBase.SpecialType == SpecialType.System_Object)
                break;
            currentBase = currentBase.BaseType;
        }
        return false;
    }
}
