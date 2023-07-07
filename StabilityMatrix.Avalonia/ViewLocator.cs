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
        var attr = (ViewAttribute) Attribute.GetCustomAttribute(type, typeof(ViewAttribute));

        if (attr is null)
            return new TextBlock
                {Text = "Not Found: " + name + ". Did you forget to add the [View] attribute?"};
        
        // Get from DI
        var view = App.Services.GetService(attr.GetViewType());
        if (view is not null)
        {
            return (Control) view;
        }

        return new TextBlock {Text = "Not Found: " + name};
    }

    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }
}
