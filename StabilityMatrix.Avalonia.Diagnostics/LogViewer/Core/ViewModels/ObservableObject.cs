using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace StabilityMatrix.Avalonia.Diagnostics.LogViewer.Core.ViewModels;

public class ObservableObject : INotifyPropertyChanged
{
    protected bool Set<TValue>(ref TValue field, TValue newValue, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<TValue>.Default.Equals(field, newValue)) return false;
        field = newValue;
        OnPropertyChanged(propertyName);

        return true;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
