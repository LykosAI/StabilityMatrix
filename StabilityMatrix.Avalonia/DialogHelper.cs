using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using StabilityMatrix.Avalonia.Controls;

namespace StabilityMatrix.Avalonia;

public static class DialogHelper
{
    /// <summary>
    /// Create a generic textbox entry content dialog.
    /// </summary>
    public static BetterContentDialog CreateTextEntryDialog(
        string title, 
        string description, 
        IReadOnlyList<TextBoxField> textFields)
    {
        Dispatcher.UIThread.CheckAccess();

        var stackPanel = new StackPanel();
        var grid = new Grid
        {
            RowDefinitions = 
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star)
            },
            Children =
            {
                new TextBlock
                {
                    Text = description
                },
                stackPanel
            }
        };
        
        // Disable primary button if any textboxes are invalid
        var primaryCommand = new RelayCommand(delegate { },
            () =>
            {
                var invalidCount = textFields.Count(field => !field.IsValid);
                Debug.WriteLine($"Checking can execute: {invalidCount} invalid fields");
                return invalidCount == 0;
            });
        
        // Create textboxes
        foreach (var field in textFields)
        {
            var label = new TextBlock
            {
                Text = field.Label
            };
            stackPanel.Children.Add(label);
            
            var textBox = new TextBox
            {
                [!TextBox.TextProperty] = new Binding("TextProperty"),
                Watermark = field.Watermark,
                DataContext = field,
            };
            stackPanel.Children.Add(textBox);
            
            // When IsValid property changes, update invalid count and primary button
            field.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(TextBoxField.IsValid))
                {
                    primaryCommand.NotifyCanExecuteChanged();
                }
            };
            
            // Set initial value
            textBox.Text = field.Text;
            
            // See if initial value is valid
            try
            {
                field.Validator?.Invoke(field.Text);
            }
            catch (Exception)
            {
                field.IsValid = false;
            }
        }
        
        return new BetterContentDialog
        {
            Title = title,
            Content = grid,
            PrimaryButtonText = "OK",
            CloseButtonText = "Cancel",
            IsPrimaryButtonEnabled = true,
            PrimaryButtonCommand = primaryCommand,
            DefaultButton = ContentDialogButton.Primary
        };
    }
}

// Text fields
public sealed class TextBoxField : INotifyPropertyChanged
{
    // Label above the textbox
    public string Label { get; init; } = string.Empty;
    // Actual text value
    public string Text { get; set; } = string.Empty;
    // Watermark text
    public string Watermark { get; init; } = string.Empty;
    
    /// <summary>
    /// Validation action on text changes. Throw exception if invalid.
    /// </summary>
    public Action<string>? Validator { get; init; }
    
    public string TextProperty
    {
        get => Text;
        [DebuggerStepThrough]
        set
        {
            try
            {
                Validator?.Invoke(value);
            }
            catch (Exception e)
            {
                IsValid = false;
                throw new DataValidationException(e.Message);
            }
            Text = value;
            IsValid = true;
            OnPropertyChanged();
        }
    }
    
    // Default to true if no validator is provided
    private bool isValid;
    public bool IsValid
    {
        get => Validator == null || isValid;
        set
        {
            isValid = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
