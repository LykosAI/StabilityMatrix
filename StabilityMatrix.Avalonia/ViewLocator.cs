using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using StabilityMatrix.Avalonia.ViewModels;

namespace StabilityMatrix.Avalonia;

public class ViewLocator : IDataTemplate
{
    public Control Build(object? data)
    {
        if (data is null) throw new ArgumentNullException(nameof(data));
        
        var name = data.GetType().FullName!.Replace("ViewModel", "View");
        var type = Type.GetType(name);
        
        // Get from DI
        if (type is not null)
        {
            var view = App.Services.GetService(type);
            if (view is not null)
            {
                return (Control) view;
            }
        }

        return new TextBlock {Text = "Not Found: " + name};
    }

    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }
}
