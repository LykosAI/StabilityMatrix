using Microsoft.UI.Xaml;
using System;
using System.Diagnostics;
using Windows.ApplicationModel.Core;
using Windows.UI.ViewManagement;

namespace StabilityMatrix
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            CoreApplication.UnhandledErrorDetected += UnhandledError;
            InitializeComponent();
            DebugSettings.IsBindingTracingEnabled = true;
            DebugSettings.BindingFailed += (sender, args) => Debug.WriteLine(args.Message);
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            mainWindow = new MainWindow();
            mainWindow.Activate();
        }

        private static void UnhandledError(object sender, UnhandledErrorDetectedEventArgs eventArgs)
        {
            try
            {
                eventArgs.UnhandledError.Propagate();
            }
            catch (Exception e)
            {
                Debug.WriteLine("Error: {0}", e);
                throw;
            }
        }

        private Window mainWindow;
    }
}
