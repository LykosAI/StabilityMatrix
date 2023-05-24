using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace StabilityMatrix.ViewModels;

internal class SettingsViewModel : INotifyPropertyChanged
{
    public ObservableCollection<string> AvailableThemes => new()
    {
        "Light",
        "Dark",
        "System",
    };
    private string selectedTheme;
    
    public string SelectedTheme
    {
        get => selectedTheme;
        set
        {
            if (value == selectedTheme) return;
            selectedTheme = value;
            OnPropertyChanged();
            
            // TODO: redo for new UI Framework
            // Update theme
            // switch (selectedTheme)
            // {
            //     case "Light":
            //         Application.Current.RequestedTheme = ApplicationTheme.Light;
            //         break;
            //     case "Dark":
            //         Application.Current.RequestedTheme = ApplicationTheme.Dark;
            //         break;
            // }
        }
    }

    public SettingsViewModel()
    {
        SelectedTheme = "System";
    }

    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
