using System.Diagnostics;
using StabilityMatrix.Avalonia.Controls.Dock;

namespace StabilityMatrix.Avalonia.Views;

public partial class InferenceTextToImageView : DockUserControlBase
{
    public InferenceTextToImageView()
    {
        InitializeComponent();
    }
    
    ~InferenceTextToImageView()
    {
        if (DataContext is { } dataContext)
        {
            Debug.WriteLine("InferenceTextToImageView destructor");
        }
        Debug.WriteLine("InferenceTextToImageView destructor");
    }
}
