using System.Windows.Controls;
using StabilityMatrix.ViewModels;
using Wpf.Ui.Controls.AutoSuggestBoxControl;

namespace StabilityMatrix;

public sealed partial class TextToImagePage : Page
{
    public TextToImagePage(TextToImageViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void PositivePromptBox_OnQuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        throw new System.NotImplementedException();
    }

    private void PositivePromptBox_OnSuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        throw new System.NotImplementedException();
    }

    private void PositivePromptBox_OnTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        throw new System.NotImplementedException();
    }

    private void NegativePromptBox_OnQuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        throw new System.NotImplementedException();
    }

    private void NegativePromptBox_OnSuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        throw new System.NotImplementedException();
    }

    private void NegativePromptBox_OnTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        throw new System.NotImplementedException();
    }
}
