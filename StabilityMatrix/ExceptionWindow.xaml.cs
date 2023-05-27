using System.Windows;
using Wpf.Ui.Controls.Window;

namespace StabilityMatrix;

public partial class ExceptionWindow : FluentWindow
{
    public ExceptionWindow()
    {
        InitializeComponent();
    }

    private void ExceptionWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        System.Media.SystemSounds.Hand.Play();
    }
}
