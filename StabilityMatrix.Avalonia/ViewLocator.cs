using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using StabilityMatrix.Avalonia.ViewModels;
using StabilityMatrix.Core.Attributes;

namespace StabilityMatrix.Avalonia;

public class ViewLocator : IDataTemplate
{
    public Control Build(object? data)
    {
        if (data is null) throw new ArgumentNullException(nameof(data));
        
        var name = data.GetType().FullName!;
        var type = Type.GetType(name);
        
        if (type is null) throw new InvalidOperationException("Type is null");
        
        if (Attribute.GetCustomAttribute(type, typeof(ViewAttribute)) is ViewAttribute viewAttr)
        {
            var viewType = viewAttr.GetViewType();
            // In design mode, just create a new instance of the view
            if (Design.IsDesignMode)
            {
                return (Control) Activator.CreateInstance(viewType)!;
            }
            if (App.Services.GetService(viewType) is Control view)
            {
                return view;
            }
        }

        return new TextBlock {Text = "Not Found: " + name};
    }

    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }
}
