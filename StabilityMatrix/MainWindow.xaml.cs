using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using WinRT.Interop;

namespace StabilityMatrix
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();
        }


        private void ButtonNavInstallPage_OnClick(object sender, RoutedEventArgs e)
        {
            ContentFrame.Navigate(typeof(InstallPage));
        }

        private void ButtonNavLaunchPage_OnClick(object sender, RoutedEventArgs e)
        {
            ContentFrame.Navigate(typeof(LaunchPage));
        }

        private void MainNavigationView_OnLoaded(object sender, RoutedEventArgs e)
        {
            var home = MainNavigationView.MenuItems.OfType<NavigationViewItem>().First();
            SetCurrentNavigationViewItem(home);
        }

        private void MainNavigationView_OnSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            SetCurrentNavigationViewItem(args.SelectedItemContainer as NavigationViewItem);
        }

        private void SetCurrentNavigationViewItem(NavigationViewItem item)
        {
            if (item == null || item.Tag == null) return;

            var tag = item.Tag.ToString();
            switch (tag)
            {
                case "InstallPage":
                    ContentFrame.Navigate(typeof(InstallPage));
                    break;
                case "LaunchPage":
                    ContentFrame.Navigate(typeof(LaunchPage));
                    break;
                default:
                    throw new ArgumentException($"Invalid tag: {tag}");
            }

            MainNavigationView.Header = item.Content;
            MainNavigationView.SelectedItem = item;
        }
    }
}