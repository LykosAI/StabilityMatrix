using System.Collections.ObjectModel;

namespace StabilityMatrix;

internal class InstallerViewModel
{
    public InstallerViewModel()
    {
        
    }
    
    public static ObservableCollection<BasePackage> Packages => new()
    {
        new A3WebUI(),
    };
}