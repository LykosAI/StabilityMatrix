using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace StabilityMatrix.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class ViewModelControlConventionAnalyzer : DiagnosticAnalyzer
{
    // --- Diagnostic Rule 1: Control must inherit UserControlBase/TemplatedControlBase ---
    private const string Rule1_Id = "SM0001";
    private static readonly LocalizableString Rule1_Title = new LocalizableResourceString(
        nameof(Resources.SM0001_Title),
        Resources.ResourceManager,
        typeof(Resources)
    );
    private static readonly LocalizableString Rule1_MessageFormat = new LocalizableResourceString(
        nameof(Resources.SM0001_MessageFormat),
        Resources.ResourceManager,
        typeof(Resources)
    );
    private static readonly LocalizableString Rule1_Description = new LocalizableResourceString(
        nameof(Resources.SM0001_Description),
        Resources.ResourceManager,
        typeof(Resources)
    );
    private const string Category = "Naming"; // Or "Design", "Usage", etc.

    private static readonly DiagnosticDescriptor Rule1_ControlMustInheritBase = new DiagnosticDescriptor(
        Rule1_Id,
        Rule1_Title,
        Rule1_MessageFormat,
        Category,
        DiagnosticSeverity.Error, // This is an error because it breaks functionality
        isEnabledByDefault: true,
        description: Rule1_Description
    );

    // --- Diagnostic Rule 2: Missing View Attribute ---
    private const string Rule2_Id = "SM0002";
    private static readonly LocalizableString Rule2_Title = new LocalizableResourceString(
        nameof(Resources.SM0002_Title),
        Resources.ResourceManager,
        typeof(Resources)
    );
    private static readonly LocalizableString Rule2_MessageFormat = new LocalizableResourceString(
        nameof(Resources.SM0002_MessageFormat),
        Resources.ResourceManager,
        typeof(Resources)
    );
    private static readonly LocalizableString Rule2_Description = new LocalizableResourceString(
        nameof(Resources.SM0002_Description),
        Resources.ResourceManager,
        typeof(Resources)
    );

    private static readonly DiagnosticDescriptor Rule2_MissingViewAttribute = new DiagnosticDescriptor(
        Rule2_Id,
        Rule2_Title,
        Rule2_MessageFormat,
        Category,
        DiagnosticSeverity.Warning, // This is a warning as it's a convention
        isEnabledByDefault: true,
        description: Rule2_Description
    );

    // --- Analyzer Implementation ---

    // Base DiagnosticAnalyzer requires this property
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule1_ControlMustInheritBase, Rule2_MissingViewAttribute);

    // This is the entry point for DiagnosticAnalyzer
    public override void Initialize(AnalysisContext context)
    {
        // Configure analysis
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None); // Don't analyze generated code
        context.EnableConcurrentExecution(); // Allow running concurrently

        // --- Register the actions ---
        // We'll use RegisterSymbolAction as it's often simpler for type-based checks
        // than the full incremental pipeline, unless performance becomes an issue.
        context.RegisterSymbolAction(AnalyzeNamedTypeSymbol, SymbolKind.NamedType);
    }

    private static void AnalyzeNamedTypeSymbol(SymbolAnalysisContext context)
    {
        var namedTypeSymbol = (INamedTypeSymbol)context.Symbol;

        var viewModelBaseSymbol = context.Compilation.GetTypeByMetadataName(
            "StabilityMatrix.Avalonia.ViewModels.Base.ViewModelBase"
        );
        var userControlBaseSymbol = context.Compilation.GetTypeByMetadataName(
            "StabilityMatrix.Avalonia.Controls.UserControlBase"
        );
        var templatedControlBaseSymbol = context.Compilation.GetTypeByMetadataName(
            "StabilityMatrix.Avalonia.Controls.TemplatedControlBase"
        );
        var appWindowBaseSymbol = context.Compilation.GetTypeByMetadataName(
            "StabilityMatrix.Avalonia.Controls.AppWindowBase"
        );
        var viewAttributeSymbol = context.Compilation.GetTypeByMetadataName(
            "StabilityMatrix.Core.Attributes.ViewAttribute"
        );

        if (viewModelBaseSymbol == null || userControlBaseSymbol == null || viewAttributeSymbol == null)
        {
            return;
        }

        var inheritsViewModelBase = DoesInheritFrom(namedTypeSymbol, viewModelBaseSymbol);

        if (!inheritsViewModelBase)
        {
            return;
        }

        // Rule 1 Check (remains the same)
        var directViewAttribute = namedTypeSymbol
            .GetAttributes()
            .FirstOrDefault(ad =>
                SymbolEqualityComparer.Default.Equals(ad.AttributeClass, viewAttributeSymbol)
            );

        if (directViewAttribute != null)
        {
            if (
                directViewAttribute.ConstructorArguments.Length > 0
                && directViewAttribute.ConstructorArguments[0].Kind == TypedConstantKind.Type
                && directViewAttribute.ConstructorArguments[0].Value is INamedTypeSymbol controlTypeSymbol
            )
            {
                var inheritsUserControlBase = DoesInheritFrom(controlTypeSymbol, userControlBaseSymbol);
                var inheritsTemplatedControlBase =
                    templatedControlBaseSymbol != null
                    && DoesInheritFrom(controlTypeSymbol, templatedControlBaseSymbol);
                var inheritsAppWindowBase =
                    appWindowBaseSymbol != null && DoesInheritFrom(controlTypeSymbol, appWindowBaseSymbol);

                if (!inheritsUserControlBase && !inheritsTemplatedControlBase && !inheritsAppWindowBase)
                {
                    var location = directViewAttribute.ApplicationSyntaxReference?.GetSyntax(
                        context.CancellationToken
                    )
                        is AttributeSyntax { ArgumentList.Arguments.Count: > 0 } attrSyntax
                        ? attrSyntax.ArgumentList.Arguments[0].Expression.GetLocation()
                        : namedTypeSymbol.Locations.FirstOrDefault() ?? Location.None;

                    // --- Report diagnostic on the ControlType's location ---
                    /*var controlLocation = controlTypeSymbol.Locations.FirstOrDefault();
                    if (
                        controlLocation == null
                        || controlLocation == Location.None
                        || controlLocation.SourceTree == null
                    )
                    {
                        // Fallback to the attribute location if control source isn't found
                        controlLocation = location;
                    }*/

                    var diagnostic = Diagnostic.Create(
                        Rule1_ControlMustInheritBase,
                        location,
                        controlTypeSymbol.Name,
                        namedTypeSymbol.Name,
                        userControlBaseSymbol.Name,
                        templatedControlBaseSymbol?.Name ?? "TemplatedControlBase"
                    );
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }
        // Rule 2 Check: ViewModel inherits ViewModelBase, is NOT abstract, but LACKS [View]
        // *AND* no base class in the ViewModelBase hierarchy has a [View] attribute.
        else // directViewAttribute is null
        {
            if (!namedTypeSymbol.IsAbstract)
            {
                // Check if any base class (that also inherits ViewModelBase) has the ViewAttribute
                var viewAttributeFoundInHierarchy = false;
                var currentAncestor = namedTypeSymbol.BaseType;
                while (currentAncestor != null && DoesInheritFrom(currentAncestor, viewModelBaseSymbol))
                {
                    if (
                        currentAncestor
                            .GetAttributes()
                            .Any(ad =>
                                SymbolEqualityComparer.Default.Equals(ad.AttributeClass, viewAttributeSymbol)
                            )
                    )
                    {
                        viewAttributeFoundInHierarchy = true;
                        break;
                    }
                    // Stop if we hit ViewModelBase itself without finding the attribute,
                    // or if we go beyond where ViewModelBase is relevant.
                    if (SymbolEqualityComparer.Default.Equals(currentAncestor, viewModelBaseSymbol))
                        break;
                    currentAncestor = currentAncestor.BaseType;
                }

                if (!viewAttributeFoundInHierarchy)
                {
                    var diagnostic = Diagnostic.Create(
                        Rule2_MissingViewAttribute,
                        namedTypeSymbol.Locations.FirstOrDefault() ?? Location.None,
                        namedTypeSymbol.Name
                    );
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }
    }

    /// <summary>
    /// Helper method to check if a type inherits from a specific base type.
    /// (Includes checking if the type itself is the base type)
    /// </summary>
    private static bool DoesInheritFrom(INamedTypeSymbol? type, INamedTypeSymbol? baseType)
    {
        if (type == null || baseType == null)
            return false;

        if (SymbolEqualityComparer.Default.Equals(type, baseType))
            return true; // Check if type itself is the baseType

        var currentBase = type.BaseType;
        while (currentBase != null)
        {
            if (SymbolEqualityComparer.Default.Equals(currentBase, baseType))
            {
                return true;
            }
            // Important: Check for object type to prevent infinite loop for interfaces or misconfigured hierarchies
            if (currentBase.SpecialType == SpecialType.System_Object)
            {
                break;
            }
            currentBase = currentBase.BaseType;
        }
        return false;
    }
}
