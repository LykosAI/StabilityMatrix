using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Attributes;

namespace StabilityMatrix.Avalonia;

public class ViewLocator : IDataTemplate
{
    public Control Build(object? data)
    {
        if (data is null) throw new ArgumentNullException(nameof(data));

        var type = data.GetType();
        
        if (type is null) throw new InvalidOperationException("Type is null");
        
        if (Attribute.GetCustomAttribute(type, typeof(ViewAttribute)) is ViewAttribute viewAttr)
        {
            var viewType = viewAttr.GetViewType();
            
#pragma warning disable IL2072
            // In design mode, just create a new instance of the view
            if (Design.IsDesignMode)
            {
                return (Control) Activator.CreateInstance(viewType)!;
            }
#pragma warning restore IL2072
            // Otherwise get from the service provider
            if (App.Services.GetService(viewType) is Control view)
            {
                return view;
            }
        }

        return new TextBlock
        {
            Text = "Not Found: " + data.GetType().FullName
        };
    }

    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }
}
