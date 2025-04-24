// Copyright (c) 2014 AlphaSierraPapa for the SharpDevelop Team
//
// Permission is hereby granted, free of charge, to any person obtaining a copy of this
// software and associated documentation files (the "Software"), to deal in the Software
// without restriction, including without limitation the rights to use, copy, modify, merge,
// publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons
// to whom the Software is furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all copies or
// substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
// INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR
// PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE
// FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
// OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml.Templates;
using AvaloniaEdit.Utils;
using StabilityMatrix.Core.Helper;

namespace StabilityMatrix.Avalonia.Controls.CodeCompletion;

/// <summary>
/// The listbox used inside the CompletionWindow, contains CompletionListBox.
/// </summary>
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
public class CompletionList : TemplatedControlBase
{
    private CompletionListBox? _listBox;

    public CompletionList()
    {
        AddHandler(DoubleTappedEvent, OnDoubleTapped);
    }

    /// <summary>
    /// If true, the CompletionList is filtered to show only matching items. Also enables search by substring.
    /// If false, enables the old behavior: no filtering, search by string.StartsWith.
    /// </summary>
    public bool IsFiltering { get; set; } = true;

    /// <summary>
    /// Dependency property for <see cref="EmptyTemplate" />.
    /// </summary>
    public static readonly StyledProperty<ControlTemplate> EmptyTemplateProperty = AvaloniaProperty.Register<
        CompletionList,
        ControlTemplate
    >(nameof(EmptyTemplate));

    /// <summary>
    /// Content of EmptyTemplate will be shown when CompletionList contains no items.
    /// If EmptyTemplate is null, nothing will be shown.
    /// </summary>
    public ControlTemplate EmptyTemplate
    {
        get => GetValue(EmptyTemplateProperty);
        set => SetValue(EmptyTemplateProperty, value);
    }

    /// <summary>
    /// Dependency property for <see cref="FooterText" />.
    /// </summary>
    public static readonly StyledProperty<string?> FooterTextProperty = AvaloniaProperty.Register<
        CompletionList,
        string?
    >("FooterText", "Press Enter to insert, Tab to replace");

    /// <summary>
    /// Gets/Sets the text displayed in the footer of the completion list.
    /// </summary>
    public string? FooterText
    {
        get => GetValue(FooterTextProperty);
        set => SetValue(FooterTextProperty, value);
    }

    /// <summary>
    /// Is raised when the completion list indicates that the user has chosen
    /// an entry to be completed.
    /// </summary>
    public event EventHandler<InsertionRequestEventArgs>? InsertionRequested;

    /// <summary>
    /// Raised when the completion list indicates that it should be closed.
    /// </summary>
    public event EventHandler? CloseRequested;

    /// <summary>
    /// Raises the InsertionRequested event.
    /// </summary>
    public void RequestInsertion(
        ICompletionData item,
        RoutedEventArgs triggeringEvent,
        string? appendText = null
    )
    {
        InsertionRequested?.Invoke(
            this,
            new InsertionRequestEventArgs
            {
                Item = item,
                TriggeringEvent = triggeringEvent,
                AppendText = appendText
            }
        );
    }

    /// <summary>
    /// Raises the CloseRequested event.
    /// </summary>
    public void RequestClose()
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        _listBox = e.NameScope.Find("PART_ListBox") as CompletionListBox;
        if (_listBox is not null)
        {
            // _listBox.ItemsSource = _completionData;
            _listBox.ItemsSource = FilteredCompletionData;
        }
    }

    /// <summary>
    /// Gets the list box.
    /// </summary>
    public CompletionListBox? ListBox
    {
        get
        {
            if (_listBox == null)
                ApplyTemplate();
            return _listBox;
        }
    }

    /// <summary>
    /// Dictionary of keys that request insertion of the completion
    /// mapped to strings that will be appended to the completion when selected.
    /// The string may be empty.
    /// </summary>
    public Dictionary<Key, string> CompletionAcceptKeys { get; init; } =
        new() { [Key.Enter] = "", [Key.Tab] = "" };

    /// <summary>
    /// Gets the scroll viewer used in this list box.
    /// </summary>
    public ScrollViewer? ScrollViewer => _listBox?.ScrollViewer;

    private readonly ObservableCollection<ICompletionData> _completionData = new();

    /// <summary>
    /// Gets the list to which completion data can be added.
    /// </summary>
    public IList<ICompletionData> CompletionData => _completionData;

    public ObservableCollection<ICompletionData> FilteredCompletionData { get; } = new();

    /// <inheritdoc/>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled)
        {
            HandleKey(e);
        }
    }

    /// <summary>
    /// Handles a key press. Used to let the completion list handle key presses while the
    /// focus is still on the text editor.
    /// </summary>
    [SuppressMessage("ReSharper", "SwitchStatementHandlesSomeKnownEnumValuesWithDefault")]
    public void HandleKey(KeyEventArgs e)
    {
        if (_listBox == null)
            return;

        // We have to do some key handling manually, because the default doesn't work with
        // our simulated events.
        // Also, the default PageUp/PageDown implementation changes the focus, so we avoid it.
        switch (e.Key)
        {
            case Key.Down:
                e.Handled = true;
                _listBox.SelectNextIndexWithLoop();
                break;
            case Key.Up:
                e.Handled = true;
                _listBox.SelectPreviousIndexWithLoop();
                break;
            case Key.PageDown:
                e.Handled = true;
                _listBox.SelectIndex(_listBox.SelectedIndex + _listBox.VisibleItemCount);
                break;
            case Key.PageUp:
                e.Handled = true;
                _listBox.SelectIndex(_listBox.SelectedIndex - _listBox.VisibleItemCount);
                break;
            case Key.Home:
                e.Handled = true;
                _listBox.SelectIndex(0);
                break;
            case Key.End:
                e.Handled = true;
                _listBox.SelectIndex(_listBox.ItemCount - 1);
                break;
            default:
                // Check insertion keys
                if (CompletionAcceptKeys.TryGetValue(e.Key, out var appendText) && CurrentList?.Count > 0)
                {
                    e.Handled = true;

                    if (SelectedItem is { } item)
                    {
                        RequestInsertion(item, e, appendText);
                    }
                    else
                    {
                        RequestClose();
                    }
                }

                break;
        }
    }

    protected void OnDoubleTapped(object? sender, RoutedEventArgs e)
    {
        //TODO TEST
        if (
            ((AvaloniaObject?)e.Source)
                .VisualAncestorsAndSelf()
                .TakeWhile(obj => obj != this)
                .Any(obj => obj is ListBoxItem)
        )
        {
            e.Handled = true;

            if (SelectedItem is { } item)
            {
                RequestInsertion(item, e);
            }
            else
            {
                RequestClose();
            }
        }
    }

    /// <summary>
    /// Gets/Sets the selected item.
    /// </summary>
    /// <remarks>
    /// The setter of this property does not scroll to the selected item.
    /// You might want to also call <see cref="ScrollIntoView"/>.
    /// </remarks>
    public ICompletionData? SelectedItem
    {
        get => _listBox?.SelectedItem as ICompletionData;
        set
        {
            if (_listBox == null && value != null)
                ApplyTemplate();
            if (_listBox != null) // may still be null if ApplyTemplate fails, or if listBox and value both are null
                _listBox.SelectedItem = value;
        }
    }

    /// <summary>
    /// Scrolls the specified item into view.
    /// </summary>
    public void ScrollIntoView(ICompletionData item)
    {
        if (_listBox == null)
            ApplyTemplate();
        _listBox?.ScrollIntoView(item);
    }

    /// <summary>
    /// Occurs when the SelectedItem property changes.
    /// </summary>
    public event EventHandler<SelectionChangedEventArgs> SelectionChanged
    {
        add => AddHandler(SelectingItemsControl.SelectionChangedEvent, value);
        remove => RemoveHandler(SelectingItemsControl.SelectionChangedEvent, value);
    }

    // SelectItem gets called twice for every typed character (once from FormatLine), this helps execute SelectItem only once
    private string? _currentText;

    private ObservableCollection<ICompletionData>? _currentList;

    public List<ICompletionData>? CurrentList => ListBox?.Items.Cast<ICompletionData>().ToList();

    /// <summary>
    /// Selects the best match, and filter the items if turned on using <see cref="IsFiltering" />.
    /// </summary>
    public void SelectItem(string text, bool fullUpdate = false)
    {
        if (text == _currentText)
        {
            return;
        }

        using var _ = CodeTimer.StartDebug();

        if (_listBox == null)
        {
            ApplyTemplate();
        }

        if (IsFiltering)
        {
            SelectItemFilteringLive(text, fullUpdate);
        }
        else
        {
            SelectItemWithStart(text);
        }

        _currentText = text;
    }

    private IReadOnlyList<ICompletionData> FilterItems(IEnumerable<ICompletionData> items, string query)
    {
        using var _ = CodeTimer.StartDebug();

        // Order first by quality, then by priority
        var matchingItems = items
            .Select(item => new { Item = item, Quality = GetMatchQuality(item.Text, query) })
            .Where(x => x.Quality > 0)
            .OrderByDescending(x => x.Quality)
            .ThenByDescending(x => x.Item.Priority)
            .Select(x => x.Item)
            .ToList();

        return matchingItems;
    }

    /// <summary>
    /// Filters CompletionList items to show only those matching given query, and selects the best match.
    /// </summary>
    private void SelectItemFilteringLive(string query, bool fullUpdate = false)
    {
        var listToFilter = _completionData;

        // if the user just typed one more character, don't filter all data but just filter what we are already displaying
        if (
            !fullUpdate
            && FilteredCompletionData.Count > 0
            && !string.IsNullOrEmpty(_currentText)
            && !string.IsNullOrEmpty(query)
            && query.StartsWith(_currentText, StringComparison.Ordinal)
        )
        {
            listToFilter = FilteredCompletionData;
        }

        var matchingItems = FilterItems(listToFilter, query);

        // Close if no items match
        if (matchingItems.Count == 0)
        {
            RequestClose();
            return;
        }

        // Fast path if both only 1 item, and item is the same
        if (
            FilteredCompletionData.Count == 1
            && matchingItems.Count == 1
            && FilteredCompletionData[0] == matchingItems[0]
        )
        {
            // Just update the character highlighting
            matchingItems[0].UpdateCharHighlighting(query);
        }
        else
        {
            // Clear current items and set new ones
            FilteredCompletionData.Clear();

            foreach (var item in matchingItems)
            {
                item.UpdateCharHighlighting(query);
                FilteredCompletionData.Add(item);
            }

            // Set index to 0 if not already
            if (_listBox != null && _listBox.SelectedIndex != 0)
            {
                _listBox.SelectedIndex = 0;
            }
        }
    }

    /// <summary>
    /// Filters CompletionList items to show only those matching given query, and selects the best match.
    /// </summary>
    private void SelectItemFiltering(string query, bool fullUpdate = false)
    {
        if (_listBox is null)
            throw new NullReferenceException("ListBox not set");

        var listToFilter = _completionData;

        // if the user just typed one more character, don't filter all data but just filter what we are already displaying
        if (
            !fullUpdate
            && _currentList != null
            && !string.IsNullOrEmpty(_currentText)
            && !string.IsNullOrEmpty(query)
            && query.StartsWith(_currentText, StringComparison.Ordinal)
        )
        {
            listToFilter = _currentList;
        }

        // Order first by quality, then by priority
        var matchingItems = listToFilter
            .Select(item => new { Item = item, Quality = GetMatchQuality(item.Text, query) })
            .Where(x => x.Quality > 0)
            .OrderByDescending(x => x.Quality)
            .ThenByDescending(x => x.Item.Priority)
            .ToImmutableArray();

        /*var matchingItems =
            from item in listToFilter
            let quality = GetMatchQuality(item.Text, query)
            where quality > 0
            orderby quality
            select new { Item = item, Quality = quality };*/

        var suggestedItem = _listBox.SelectedIndex != -1 ? (ICompletionData)_listBox.SelectedItem! : null;

        var listBoxItems = new ObservableCollection<ICompletionData>();
        var bestIndex = -1;
        var bestQuality = -1;
        double bestPriority = 0;
        var i = 0;
        foreach (var matchingItem in matchingItems)
        {
            var priority =
                matchingItem.Item == suggestedItem ? double.PositiveInfinity : matchingItem.Item.Priority;
            var quality = matchingItem.Quality;
            if (quality > bestQuality || quality == bestQuality && priority > bestPriority)
            {
                bestIndex = i;
                bestPriority = priority;
                bestQuality = quality;
            }

            // Add to listbox
            listBoxItems.Add(matchingItem.Item);

            // Update the character highlighting
            matchingItem.Item.UpdateCharHighlighting(query);

            i++;
        }

        _currentList = listBoxItems;
        //_listBox.Items = null; Makes no sense? Tooltip disappeared because of this
        _listBox.ItemsSource = listBoxItems;
        SelectIndex(bestIndex);
    }

    /// <summary>
    /// Selects the item that starts with the specified query.
    /// </summary>
    private void SelectItemWithStart(string query)
    {
        if (string.IsNullOrEmpty(query))
            return;

        var suggestedIndex = _listBox?.SelectedIndex ?? -1;
        if (suggestedIndex == -1)
        {
            return;
        }

        var bestIndex = -1;
        var bestQuality = -1;
        double bestPriority = 0;
        for (var i = 0; i < _completionData.Count; ++i)
        {
            var quality = GetMatchQuality(_completionData[i].Text, query);
            if (quality < 0)
                continue;

            var priority = _completionData[i].Priority;
            bool useThisItem;
            if (bestQuality < quality)
            {
                useThisItem = true;
            }
            else
            {
                if (bestIndex == suggestedIndex)
                {
                    useThisItem = false;
                }
                else if (i == suggestedIndex)
                {
                    // prefer recommendedItem, regardless of its priority
                    useThisItem = bestQuality == quality;
                }
                else
                {
                    useThisItem = bestQuality == quality && bestPriority < priority;
                }
            }
            if (useThisItem)
            {
                bestIndex = i;
                bestPriority = priority;
                bestQuality = quality;
            }
        }
        SelectIndexCentered(bestIndex);
    }

    private void SelectIndexCentered(int index)
    {
        if (_listBox is null)
        {
            throw new NullReferenceException("ListBox not set");
        }

        if (index < 0)
        {
            _listBox.ClearSelection();
        }
        else
        {
            var firstItem = _listBox.FirstVisibleItem;
            if (index < firstItem || firstItem + _listBox.VisibleItemCount <= index)
            {
                // CenterViewOn does nothing as CompletionListBox.ScrollViewer is null
                _listBox.CenterViewOn(index);
                _listBox.SelectIndex(index);
            }
            else
            {
                _listBox.SelectIndex(index);
            }
        }
    }

    private void SelectIndex(int index)
    {
        if (_listBox is null)
        {
            throw new NullReferenceException("ListBox not set");
        }

        if (index == _listBox.SelectedIndex)
            return;

        if (index < 0)
        {
            _listBox.ClearSelection();
        }
        else
        {
            _listBox.SelectedIndex = index;
        }
    }

    private int GetMatchQuality(string itemText, string query)
    {
        if (itemText == null)
            throw new ArgumentNullException(nameof(itemText), "ICompletionData.Text returned null");

        // Qualities:
        //  	8 = full match case sensitive
        // 		7 = full match
        // 		6 = match start case sensitive
        //		5 = match start
        //		4 = match CamelCase when length of query is 1 or 2 characters
        // 		3 = match substring case sensitive
        //		2 = match substring
        //		1 = match CamelCase
        //		-1 = no match
        if (query == itemText)
            return 8;
        if (string.Equals(itemText, query, StringComparison.CurrentCultureIgnoreCase))
            return 7;

        if (itemText.StartsWith(query, StringComparison.CurrentCulture))
            return 6;
        if (itemText.StartsWith(query, StringComparison.CurrentCultureIgnoreCase))
            return 5;

        bool? camelCaseMatch = null;
        if (query.Length <= 2)
        {
            camelCaseMatch = CamelCaseMatch(itemText, query);
            if (camelCaseMatch == true)
                return 4;
        }

        // search by substring, if filtering (i.e. new behavior) turned on
        if (IsFiltering)
        {
            if (itemText.Contains(query, StringComparison.CurrentCulture))
                return 3;
            if (itemText.Contains(query, StringComparison.CurrentCultureIgnoreCase))
                return 2;
        }

        if (!camelCaseMatch.HasValue)
            camelCaseMatch = CamelCaseMatch(itemText, query);
        if (camelCaseMatch == true)
            return 1;

        return -1;
    }

    private static bool CamelCaseMatch(string text, string query)
    {
        // We take the first letter of the text regardless of whether or not it's upper case so we match
        // against camelCase text as well as PascalCase text ("cct" matches "camelCaseText")
        var theFirstLetterOfEachWord = text.AsEnumerable()
            .Take(1)
            .Concat(text.AsEnumerable().Skip(1).Where(char.IsUpper));

        var i = 0;
        foreach (var letter in theFirstLetterOfEachWord)
        {
            if (i > query.Length - 1)
                return true; // return true here for CamelCase partial match ("CQ" matches "CodeQualityAnalysis")
            if (char.ToUpperInvariant(query[i]) != char.ToUpperInvariant(letter))
                return false;
            i++;
        }
        if (i >= query.Length)
            return true;
        return false;
    }
}
