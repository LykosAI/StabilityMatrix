using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Wpf.Ui.Appearance;

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
            
            switch (selectedTheme)
            {
                case "Light":
                    Theme.Apply(ThemeType.Light);
                    break;
                case "Dark":
                    Theme.Apply(ThemeType.Dark);
                    break;
            }
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
