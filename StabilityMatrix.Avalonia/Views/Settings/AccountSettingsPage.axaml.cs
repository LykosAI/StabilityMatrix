using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.ViewModels.Settings;

namespace StabilityMatrix.Avalonia.Views.Settings;

[RegisterSingleton<AccountSettingsPage>]
public partial class AccountSettingsPage : UserControlBase
{
    public AccountSettingsPage()
    {
        InitializeComponent();

        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is AccountSettingsViewModel vm)
        {
            vm.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(AccountSettingsViewModel.RoleBadges))
                {
                    UpdateRoleBadges(vm);
                }
            };

            // Initial update
            UpdateRoleBadges(vm);
        }
    }

    private void UpdateRoleBadges(AccountSettingsViewModel vm)
    {
        var panel = this.FindControl<WrapPanel>("RoleBadgesPanel");
        if (panel is null)
            return;

        panel.Children.Clear();

        foreach (var badge in vm.RoleBadges)
        {
            var label = new Label { Content = badge.DisplayName, Margin = new Thickness(0, 0, 4, 4) };

            if (
                this.TryFindResource("TagLabel", ActualThemeVariant, out var theme)
                && theme is ControlTheme controlTheme
            )
            {
                label.Theme = controlTheme;
            }

            label.Classes.Add(badge.ColorClass);

            panel.Children.Add(label);
        }
    }
}
