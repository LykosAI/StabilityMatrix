using System.Reactive.Linq;
using System.Reactive.Subjects;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Input;
using Avalonia.Threading;
using FuzzySharp;
using Microsoft.Extensions.DependencyInjection;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper.Cache;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api.Comfy;
using StabilityMatrix.Core.Models.Settings;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.Controls;

public class BetterComboBox : ComboBox
{
    private static readonly TimeSpan LegacySearchIdleResetDelay = TimeSpan.FromMilliseconds(1200);

    public static readonly StyledProperty<string> SearchWatermarkProperty = AvaloniaProperty.Register<
        BetterComboBox,
        string
    >(nameof(SearchWatermark), defaultValue: "Search...");
    public static readonly StyledProperty<bool> UseLegacyModelSearchProperty = AvaloniaProperty.Register<
        BetterComboBox,
        bool
    >(nameof(UseLegacyModelSearch));
    public static readonly DirectProperty<BetterComboBox, string> SearchTextProperty =
        AvaloniaProperty.RegisterDirect<BetterComboBox, string>(nameof(SearchText), o => o.SearchText);

    public string SearchWatermark
    {
        get => GetValue(SearchWatermarkProperty);
        set => SetValue(SearchWatermarkProperty, value);
    }

    public bool UseLegacyModelSearch
    {
        get => GetValue(UseLegacyModelSearchProperty);
        set => SetValue(UseLegacyModelSearchProperty, value);
    }

    public string SearchText
    {
        get => searchText;
        private set => SetAndRaise(SearchTextProperty, ref searchText, value);
    }

    private readonly Subject<string> inputSubject = new();
    private readonly IDisposable subscription;
    private readonly LRUCache<string, object?> searchCache = new(50);
    private readonly ISettingsManager? settingsManager;
    private readonly Popup legacyInputPopup;
    private readonly TextBlock legacyInputTextBlock;
    private readonly DispatcherTimer legacySearchResetTimer = new() { Interval = LegacySearchIdleResetDelay };
    private TextBox? searchTextBox;
    private string keyboardSearchText = string.Empty;
    private string searchText = string.Empty;
    private string lastAppliedFilter = string.Empty;
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
        legacySearchResetTimer.Tick += OnLegacySearchResetTimerTick;

        legacyInputTextBlock = new TextBlock { FontSize = 13 };
        legacyInputTextBlock.Bind(
            TextBlock.ForegroundProperty,
            this.GetResourceObservable("ComboBoxForeground")
        );
        var popupBorder = new Border { Padding = new Thickness(8, 4), Child = legacyInputTextBlock };
        popupBorder.Bind(Border.BackgroundProperty, this.GetResourceObservable("ComboBoxDropDownBackground"));
        legacyInputPopup = new Popup
        {
            IsLightDismissEnabled = true,
            Placement = PlacementMode.AnchorAndGravity,
            PlacementAnchor = PopupAnchor.Bottom,
            PlacementGravity = PopupGravity.Top,
            VerticalOffset = -6,
            Child = popupBorder,
        };

        if (!Design.IsDesignMode)
        {
            settingsManager = App.Services.GetService<ISettingsManager>();
            if (settingsManager is not null)
            {
                UseLegacyModelSearch = settingsManager.Settings.UseLegacyModelSearch;
                settingsManager.SettingsPropertyChanged += OnSettingsPropertyChanged;
            }
        }
    }

    /// <inheritdoc/>
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        legacyInputPopup.PlacementTarget = this;

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
            RestartLegacySearchResetTimer();
            UpdateLegacySearchPopupText(keyboardSearchText);

            if (IsDropDownOpen)
            {
                UpdateSearchTextBoxText(keyboardSearchText);
                if (!UseLegacyModelSearch)
                {
                    Dispatcher.UIThread.Post(() => searchTextBox?.Focus(), DispatcherPriority.Input);
                }
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
        SearchText = keyboardSearchText;
        inputSubject.OnNext(keyboardSearchText);
        RestartLegacySearchResetTimer();
        UpdateLegacySearchPopupText(keyboardSearchText);
    }

    private void SearchTextBoxOnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
            return;

        StopLegacySearchResetTimer();
        IsDropDownOpen = false;
        e.Handled = true;
    }

    private void OnDropDownOpened(object? sender, EventArgs e)
    {
        StopLegacySearchResetTimer();
        ResetSearchText();
        ApplyFilter(string.Empty);
        if (!UseLegacyModelSearch)
        {
            Dispatcher.UIThread.Post(() => searchTextBox?.Focus(), DispatcherPriority.Input);
        }
    }

    private void OnDropDownClosed(object? sender, EventArgs e)
    {
        StopLegacySearchResetTimer();
        ResetSearchText();
        ApplyFilter(string.Empty);
    }

    private void UpdateSearchTextBoxText(string text)
    {
        SearchText = text;
        UpdateLegacySearchPopupText(text);

        if (searchTextBox is null)
            return;

        isUpdatingSearchText = true;
        searchTextBox.Text = text;
        searchTextBox.CaretIndex = searchTextBox.Text?.Length ?? 0;
        isUpdatingSearchText = false;
    }

    private void ResetSearchText()
    {
        StopLegacySearchResetTimer();
        keyboardSearchText = string.Empty;
        UpdateSearchTextBoxText(string.Empty);
    }

    private void RestartLegacySearchResetTimer()
    {
        if (!UseLegacyModelSearch || string.IsNullOrEmpty(keyboardSearchText))
            return;

        legacySearchResetTimer.Stop();
        legacySearchResetTimer.Start();
    }

    private void StopLegacySearchResetTimer()
    {
        legacySearchResetTimer.Stop();
    }

    private void UpdateLegacySearchPopupText(string text)
    {
        if (!UseLegacyModelSearch || string.IsNullOrWhiteSpace(text))
        {
            HideLegacySearchPopup();
            return;
        }

        legacyInputTextBlock.Text = text;

        if (legacyInputPopup.PlacementTarget is null)
        {
            legacyInputPopup.PlacementTarget = this;
        }

        if (!legacyInputPopup.IsOpen)
        {
            legacyInputPopup.IsOpen = true;
        }
    }

    private void HideLegacySearchPopup()
    {
        legacyInputTextBlock.Text = string.Empty;
        legacyInputPopup.IsOpen = false;
    }

    private void OnLegacySearchResetTimerTick(object? sender, EventArgs e)
    {
        legacySearchResetTimer.Stop();

        if (!UseLegacyModelSearch || string.IsNullOrWhiteSpace(keyboardSearchText))
            return;

        ResetSearchText();
    }

    private void OnInputReceived(string input)
    {
        if (IsDropDownOpen)
        {
            if (UseLegacyModelSearch)
            {
                var query = input.Trim();
                if (string.IsNullOrWhiteSpace(query))
                    return;

                var legacyMatch = FindLegacyMatch(query);
                if (legacyMatch is not null)
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        SelectedItem = legacyMatch;
                        ScrollIntoView(legacyMatch);
                    });
                }
            }
            else
            {
                Dispatcher.UIThread.Post(() => ApplyFilter(input));
            }
            return;
        }

        if (string.IsNullOrWhiteSpace(input))
            return;

        if (searchCache.Get(input, out var cachedResult) && cachedResult is not null)
        {
            Dispatcher.UIThread.Post(() => SelectedItem = cachedResult);
            return;
        }

        if (UseLegacyModelSearch)
        {
            var legacyMatch = FindLegacyMatch(input);
            if (legacyMatch is null)
                return;

            searchCache.Add(input, legacyMatch);
            Dispatcher.UIThread.Post(() => SelectedItem = legacyMatch);
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
        var filterChanged = !string.Equals(lastAppliedFilter, query, StringComparison.Ordinal);
        lastAppliedFilter = query;

        var hasQuery = !string.IsNullOrWhiteSpace(query);
        object? firstMatch = null;

        foreach (var item in Items.Cast<object>())
        {
            var isMatch = !hasQuery || IsItemMatch(item, query);
            if (isMatch && firstMatch is null)
            {
                firstMatch = item;
            }

            if (ContainerFromItem(item) is not Control container)
                continue;

            container.IsVisible = isMatch;
        }

        if (!IsDropDownOpen || firstMatch is null)
        {
            return;
        }

        if (!filterChanged)
        {
            return;
        }

        // Keep the first matching result pinned near the top when virtualizing.
        Dispatcher.UIThread.Post(() => ScrollIntoView(firstMatch), DispatcherPriority.Background);
    }

    private bool IsItemMatch(object item, string query)
    {
        var itemText = GetItemSearchText(item, UseLegacyModelSearch);

        if (UseLegacyModelSearch)
        {
            return itemText.Contains(query, StringComparison.OrdinalIgnoreCase);
        }

        if (itemText.Contains(query, StringComparison.OrdinalIgnoreCase))
            return true;

        // Allow approximate matching for typos while filtering.
        return Fuzz.PartialRatio(query, itemText) >= 70;
    }

    private object? FindLegacyMatch(string query)
    {
        var trimmedQuery = query.Trim();
        if (string.IsNullOrWhiteSpace(trimmedQuery))
            return null;

        object? firstSearchTextMatch = null;

        foreach (var item in Items)
        {
            if (item is Enum enumItem)
            {
                if (enumItem.GetStringValue().Contains(trimmedQuery, StringComparison.OrdinalIgnoreCase))
                {
                    return enumItem;
                }
            }
            else if (firstSearchTextMatch is null && item is ISearchText or ComfySampler or ComfyScheduler)
            {
                if (GetItemSearchText(item, true).Contains(trimmedQuery, StringComparison.OrdinalIgnoreCase))
                {
                    firstSearchTextMatch = item;
                }
            }
        }

        return firstSearchTextMatch;
    }

    private static string GetItemSearchText(object item, bool useLegacySearch = false)
    {
        return item switch
        {
            HybridModelFile hybridModel => useLegacySearch
                ? hybridModel.SearchText
                : hybridModel.DetailedSearchText,
            Enum enumItem => enumItem.GetStringValue(),
            ComfySampler sampler => $"{sampler.DisplayName} {sampler.Name}",
            ComfyScheduler scheduler => $"{scheduler.DisplayName} {scheduler.Name}",
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
        if (!IsDropDownOpen || UseLegacyModelSearch)
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
        if (!IsDropDownOpen || UseLegacyModelSearch)
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

        if (change.Property == UseLegacyModelSearchProperty && !UseLegacyModelSearch)
        {
            StopLegacySearchResetTimer();
            HideLegacySearchPopup();
        }
    }

    private void OnSettingsPropertyChanged(object? sender, RelayPropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(Settings.UseLegacyModelSearch) || settingsManager is null)
            return;

        Dispatcher.UIThread.Post(() =>
        {
            UseLegacyModelSearch = settingsManager.Settings.UseLegacyModelSearch;
            if (!UseLegacyModelSearch)
            {
                StopLegacySearchResetTimer();
                HideLegacySearchPopup();
            }
        });
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

        if (settingsManager is not null)
        {
            settingsManager.SettingsPropertyChanged -= OnSettingsPropertyChanged;
        }

        legacySearchResetTimer.Tick -= OnLegacySearchResetTimerTick;
        StopLegacySearchResetTimer();
        HideLegacySearchPopup();
        subscription.Dispose();
    }
}
