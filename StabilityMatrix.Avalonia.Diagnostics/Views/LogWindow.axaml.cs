using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using StabilityMatrix.Avalonia.Diagnostics.ViewModels;

namespace StabilityMatrix.Avalonia.Diagnostics.Views;

public partial class LogWindow : Window
{
    public LogWindow()
    {
        InitializeComponent();
    }

    public static IDisposable Attach(TopLevel root, IServiceProvider serviceProvider)
    {
        return Attach(root, serviceProvider, new KeyGesture(Key.F11));
    }
    
    public static IDisposable Attach(TopLevel root, IServiceProvider serviceProvider, KeyGesture gesture)
    {
        return (root ?? throw new ArgumentNullException(nameof(root))).AddDisposableHandler(
            KeyDownEvent,
            PreviewKeyDown,
            RoutingStrategies.Tunnel);

        void PreviewKeyDown(object? sender, KeyEventArgs e)
        {
            if (gesture.Matches(e))
            {
                var window = new LogWindow()
                {
                    DataContext = LogWindowViewModel.FromServiceProvider(serviceProvider)
                };
                window.Show();
            }
        }
    }
}
