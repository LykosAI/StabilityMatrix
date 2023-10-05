using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using FluentAvalonia.UI.Controls;
using NLog;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Attributes;

namespace StabilityMatrix.Avalonia;

public class ViewLocator : IDataTemplate, INavigationPageFactory
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /*/// <summary>
    /// Weak Dictionary of (DataContext, View) pairs to keep the view and layout alive
    /// </summary>
    private static readonly ConditionalWeakTable<object, Control> PersistentViewCache = new();*/

    /// <inheritdoc />
    public Control Build(object? data)
    {
        if (data is null)
            throw new ArgumentNullException(nameof(data));

        var type = data.GetType();

        if (Attribute.GetCustomAttribute(type, typeof(ViewAttribute)) is ViewAttribute viewAttr)
        {
            var viewType = viewAttr.ViewType;
            return GetView(viewType, data, viewAttr.IsPersistent);
        }

        return new TextBlock { Text = "View Model Not Found: " + data.GetType().FullName };
    }

    private Control GetView(Type viewType)
    {
        if (App.Services.GetService(viewType) is Control view)
        {
            return view;
        }

        return new TextBlock { Text = "View Not Found: " + viewType.FullName };
    }

    private Control GetView(Type viewType, object context, bool persistent)
    {
        // Disregard persistent settings in design mode
        if (Design.IsDesignMode)
        {
            persistent = false;
        }

        if (persistent)
        {
            // Check assignable from IPersistentViewProvider
            if (context is not IPersistentViewProvider persistentViewProvider)
            {
                throw new InvalidOperationException(
                    $"View {viewType.Name} is marked as persistent but does not implement IPersistentViewProvider"
                );
            }

            // Try get from context
            if (persistentViewProvider.AttachedPersistentView is { } view)
            {
                Logger.Trace("Got persistent view {ViewType} from context", viewType.Name);

                return view;
            }

            // Otherwise get from service provider
            if (App.Services.GetService(viewType) is Control newView)
            {
                // Set as attached view
                persistentViewProvider.AttachedPersistentView = newView;
                Logger.Trace("Attached persistent view {ViewType}", viewType.Name);
                return newView;
            }
        }
        else
        {
            // Get from service provider
            if (App.Services.GetService(viewType) is Control view)
            {
                return view;
            }
        }

        return new TextBlock { Text = "View Not Found: " + viewType.FullName };
    }

    /// <inheritdoc />
    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }

    /// <inheritdoc />
    public Control? GetPage(Type srcType)
    {
        if (
            Attribute.GetCustomAttribute(srcType, typeof(ViewAttribute))
            is not ViewAttribute viewAttr
        )
        {
            throw new InvalidOperationException("View not found for " + srcType.FullName);
        }

        // Get new view
        var view = GetView(viewAttr.ViewType);
        view.DataContext ??= App.Services.GetService(srcType);

        return view;
    }

    /// <inheritdoc />
    public Control GetPageFromObject(object target)
    {
        if (
            Attribute.GetCustomAttribute(target.GetType(), typeof(ViewAttribute))
            is not ViewAttribute viewAttr
        )
        {
            throw new InvalidOperationException("View not found for " + target.GetType().FullName);
        }

        var viewType = viewAttr.ViewType;
        var view = GetView(viewType, target, viewAttr.IsPersistent);
        view.DataContext ??= target;
        return view;
    }
}
