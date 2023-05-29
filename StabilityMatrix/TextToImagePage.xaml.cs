using System.Windows;
using System.Windows.Controls;
using StabilityMatrix.Services;
using StabilityMatrix.ViewModels;
using Wpf.Ui.Controls.AutoSuggestBoxControl;

namespace StabilityMatrix;

public sealed partial class TextToImagePage : Page
{
    private TextToImageViewModel ViewModel => (TextToImageViewModel) DataContext;
    
    public TextToImagePage(TextToImageViewModel viewModel, PageContentDialogService pageContentDialogService)
    {
        InitializeComponent();
        DataContext = viewModel;
        pageContentDialogService.SetContentPresenter(PageContentDialog);
    }

    private void PositivePromptBox_OnQuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
    }

    private void PositivePromptBox_OnSuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
    }

    private void PositivePromptBox_OnTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        // Update the prompt text when the user types
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            var fullText = sender.Text;
            ViewModel.PositivePromptText = fullText;
        }
    }

    private void NegativePromptBox_OnQuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
    }

    private void NegativePromptBox_OnSuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
    }

    private void NegativePromptBox_OnTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        // Update the prompt text when the user types
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            var fullText = sender.Text;
            ViewModel.NegativePromptText = fullText;
        }
    }

    private async void TextToImagePage_OnLoaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.OnLoaded();
    }
}
