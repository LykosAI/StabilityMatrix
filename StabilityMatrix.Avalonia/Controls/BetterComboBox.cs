using System.Reactive.Linq;
using System.Reactive.Subjects;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Threading;
using FuzzySharp;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper.Cache;
using StabilityMatrix.Core.Models;

namespace StabilityMatrix.Avalonia.Controls;

public class BetterComboBox : ComboBox
{
    public static readonly StyledProperty<string> SearchWatermarkProperty = AvaloniaProperty.Register<
        BetterComboBox,
        string
    >(nameof(SearchWatermark), defaultValue: "Search...");

    public string SearchWatermark
    {
        get => GetValue(SearchWatermarkProperty);
        set => SetValue(SearchWatermarkProperty, value);
    }

    private readonly Subject<string> inputSubject = new();
    private readonly IDisposable subscription;
    private readonly LRUCache<string, object?> searchCache = new(50);
    private TextBox? searchTextBox;
    private string keyboardSearchText = string.Empty;
    private bool isUpdatingSearchText;

    public BetterComboBox()
    {
        DropDownOpened += OnDropDownOpened;
        DropDownClosed += OnDropDownClosed;
        ContainerPrepared += OnContainerPrepared;
        ContainerIndexChanged += OnContainerIndexChanged;

        var inputObservable = inputSubject
            .Select(text => text.Trim())
            .Throttle(TimeSpan.FromMilliseconds(200))
            .DistinctUntilChanged();

        subscription = inputObservable
            .ObserveOn(SynchronizationContext.Current)
            .Subscribe(OnInputReceived, _ => ResetSearchText());
    }

    /// <inheritdoc/>
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (e.NameScope.Find<ContentControl>("ContentPresenter") is { } contentPresenter)
        {
            if (SelectionBoxItemTemplate is { } template)
            {
                contentPresenter.ContentTemplate = template;
            }
        }

        if (searchTextBox is not null)
        {
            searchTextBox.TextChanged -= SearchTextBoxOnTextChanged;
            searchTextBox.KeyDown -= SearchTextBoxOnKeyDown;
        }

        searchTextBox = e.NameScope.Find<TextBox>("PART_SearchTextBox");
        if (searchTextBox is not null)
        {
            AutomationProperties.SetName(searchTextBox, "Search models");
            searchTextBox.TextChanged += SearchTextBoxOnTextChanged;
            searchTextBox.KeyDown += SearchTextBoxOnKeyDown;
        }
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        if (e.Handled)
            return;

        if (searchTextBox?.IsFocused == true)
        {
            base.OnTextInput(e);
            return;
        }

        if (!string.IsNullOrWhiteSpace(e.Text))
        {
            keyboardSearchText += e.Text;
            inputSubject.OnNext(keyboardSearchText);

            if (IsDropDownOpen)
            {
                UpdateSearchTextBoxText(keyboardSearchText);
                Dispatcher.UIThread.Post(() => searchTextBox?.Focus(), DispatcherPriority.Input);
            }

            e.Handled = true;
        }

        base.OnTextInput(e);
    }

    private void SearchTextBoxOnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (isUpdatingSearchText || sender is not TextBox textBox)
            return;

        keyboardSearchText = textBox.Text ?? string.Empty;
        inputSubject.OnNext(keyboardSearchText);
    }

    private void SearchTextBoxOnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
            return;

        IsDropDownOpen = false;
        e.Handled = true;
    }

    private void OnDropDownOpened(object? sender, EventArgs e)
    {
        ResetSearchText();
        ApplyFilter(string.Empty);
        Dispatcher.UIThread.Post(() => searchTextBox?.Focus(), DispatcherPriority.Input);
    }

    private void OnDropDownClosed(object? sender, EventArgs e)
    {
        ResetSearchText();
        ApplyFilter(string.Empty);
    }

    private void UpdateSearchTextBoxText(string text)
    {
        if (searchTextBox is null)
            return;

        isUpdatingSearchText = true;
        searchTextBox.Text = text;
        searchTextBox.CaretIndex = searchTextBox.Text?.Length ?? 0;
        isUpdatingSearchText = false;
    }

    private void ResetSearchText()
    {
        keyboardSearchText = string.Empty;
        UpdateSearchTextBoxText(string.Empty);
    }

    private void OnInputReceived(string input)
    {
        if (IsDropDownOpen)
        {
            Dispatcher.UIThread.Post(() => ApplyFilter(input));
            return;
        }

        if (string.IsNullOrWhiteSpace(input))
            return;

        if (searchCache.Get(input, out var cachedResult) && cachedResult is not null)
        {
            Dispatcher.UIThread.Post(() => SelectedItem = cachedResult);
            return;
        }

        object? found = null;

        var enumBestMatch = FindBestMatch(input, Items.OfType<Enum>(), e => e.GetStringValue());
        if (enumBestMatch.Score > 50)
        {
            found = enumBestMatch.Item;
        }
        else
        {
            var modelBestMatch = FindBestMatch(input, Items.OfType<ISearchText>(), m => GetItemSearchText(m));
            if (modelBestMatch.Score > 50)
            {
                found = modelBestMatch.Item;
            }
        }

        if (found is not null)
        {
            searchCache.Add(input, found);
            Dispatcher.UIThread.Post(() => SelectedItem = found);
        }
    }

    private void ApplyFilter(string input)
    {
        var query = input.Trim();
        var hasQuery = !string.IsNullOrWhiteSpace(query);

        foreach (var item in Items.Cast<object>())
        {
            if (ContainerFromItem(item) is not Control container)
                continue;

            container.IsVisible = !hasQuery || IsItemMatch(item, query);
        }
    }

    private bool IsItemMatch(object item, string query)
    {
        var itemText = GetItemSearchText(item);
        if (itemText.Contains(query, StringComparison.OrdinalIgnoreCase))
            return true;

        // Allow approximate matching for typos while filtering.
        return Fuzz.PartialRatio(query, itemText) >= 70;
    }

    private static string GetItemSearchText(object item)
    {
        return item switch
        {
            HybridModelFile hybridModel => hybridModel.DetailedSearchText,
            Enum enumItem => enumItem.GetStringValue(),
            ISearchText searchable => searchable.SearchText,
            _ => item.ToString() ?? string.Empty,
        };
    }

    private static (TItem? Item, int Score) FindBestMatch<TItem>(
        string input,
        IEnumerable<TItem> items,
        Func<TItem, string> getSearchText
    )
    {
        TItem? bestItem = default;
        var bestScore = 0;

        foreach (var item in items)
        {
            var score = Fuzz.WeightedRatio(input, getSearchText(item));
            if (score <= bestScore)
                continue;

            bestScore = score;
            bestItem = item;
        }

        return (bestItem, bestScore);
    }

    private void OnContainerPrepared(object? sender, ContainerPreparedEventArgs e)
    {
        if (!IsDropDownOpen)
            return;

        var query = keyboardSearchText.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            e.Container.IsVisible = true;
            return;
        }

        if (e.Index >= 0 && e.Index < ItemsView.Count && ItemsView[e.Index] is { } item)
        {
            e.Container.IsVisible = IsItemMatch(item, query);
        }
    }

    private void OnContainerIndexChanged(object? sender, ContainerIndexChangedEventArgs e)
    {
        if (!IsDropDownOpen)
            return;

        var query = keyboardSearchText.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            e.Container.IsVisible = true;
            return;
        }

        if (e.NewIndex >= 0 && e.NewIndex < ItemsView.Count && ItemsView[e.NewIndex] is { } item)
        {
            e.Container.IsVisible = IsItemMatch(item, query);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ItemsSourceProperty)
        {
            searchCache.Clear();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        DropDownOpened -= OnDropDownOpened;
        DropDownClosed -= OnDropDownClosed;
        ContainerPrepared -= OnContainerPrepared;
        ContainerIndexChanged -= OnContainerIndexChanged;

        if (searchTextBox is not null)
        {
            searchTextBox.TextChanged -= SearchTextBoxOnTextChanged;
            searchTextBox.KeyDown -= SearchTextBoxOnKeyDown;
        }

        subscription.Dispose();
    }
}
