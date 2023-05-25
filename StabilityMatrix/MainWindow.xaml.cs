using System.Diagnostics;
using System.Windows;
using StabilityMatrix.Helper;
using Wpf.Ui.Appearance;
using Wpf.Ui.Contracts;
using Wpf.Ui.Controls.Window;

namespace StabilityMatrix
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : FluentWindow
    {
        private readonly IPageService pageService;
        private readonly ISettingsManager settingsManager;

        public MainWindow(IPageService pageService, ISettingsManager settingsManager)
        {
            InitializeComponent();

            this.pageService = pageService;
            this.settingsManager = settingsManager;

            RootNavigation.Navigating += (_, _) => Debug.WriteLine("Navigating");
            RootNavigation.SetPageService(pageService);
        }

        private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            SetTheme();
            RootNavigation.Navigate(typeof(LaunchPage));
        }

        private void SetTheme()
        {
            var theme = settingsManager.Settings.Theme;
            switch (theme)
            {
                case "Dark":
                    Theme.Apply(ThemeType.Dark);
                    break;
                case "Light":
                    Theme.Apply(ThemeType.Light);
                    break;
            }
        }
    }
}
