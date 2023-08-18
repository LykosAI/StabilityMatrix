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
using System.Diagnostics;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Utils;
using StabilityMatrix.Avalonia.Models.TagCompletion;

namespace StabilityMatrix.Avalonia.Controls.CodeCompletion;

/// <summary>
/// The code completion window.
/// </summary>
public class CompletionWindow : CompletionWindowBase
{
    private readonly ICompletionProvider completionProvider;
    
    private PopupWithCustomPosition? _toolTip;
    private ContentControl? _toolTipContent;
    
    /// <summary>
    /// Max number of items in the completion list.
    /// </summary>
    public int MaxListLength { get; set; } = 40;
    
    /// <summary>
    /// Gets the completion list used in this completion window.
    /// </summary>
    public CompletionList CompletionList { get; }

    /// <summary>
    /// Creates a new code completion window.
    /// </summary>
    public CompletionWindow(TextArea textArea, ICompletionProvider completionProvider) : base(textArea)
    {
        this.completionProvider = completionProvider;
        
        CompletionList = new CompletionList
        {
            IsFiltering = true
        };

        // keep height automatic
        CloseAutomatically = true;
        MaxHeight = 225;
        // Width = 175;
        Width = 350;
        Child = CompletionList;
        // prevent user from resizing window to 0x0
        MinHeight = 15;
        MinWidth = 30;
        
        _toolTipContent = new ContentControl();
        _toolTipContent.Classes.Add("ToolTip");

        _toolTip = new PopupWithCustomPosition
        {
            IsLightDismissEnabled = true,
            PlacementTarget = this,
            Placement = PlacementMode.RightEdgeAlignedTop,
            // Placement = PlacementMode.LeftEdgeAlignedBottom,
            Child = _toolTipContent,
        };

        LogicalChildren.Add(_toolTip);

        //_toolTip.Closed += (o, e) => ((Popup)o).Child = null;

        AttachEvents();
    }

    protected override void OnClosed()
    {
        base.OnClosed();

        if (_toolTip != null)
        {
            _toolTip.IsOpen = false;
            _toolTip = null;
            _toolTipContent = null;
        }
    }

    #region ToolTip handling

    private void CompletionList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_toolTipContent == null || _toolTip == null) return;

        var item = CompletionList.SelectedItem;
        if (item?.Description is { } descriptionText)
        {
            _toolTipContent.Content = new TextBlock
            {
                Text = descriptionText,
                TextWrapping = TextWrapping.Wrap
            };

            _toolTip.IsOpen = false; // Popup needs to be closed to change position

            // Calculate offset for tooltip
            var popupRoot = Host as PopupRoot;
            if (CompletionList.CurrentList != null)
            {
                double yOffset = 0;
                var itemContainer = CompletionList.ListBox!.ContainerFromItem(item);
                if (popupRoot != null && itemContainer != null)
                {
                    var position = itemContainer.TranslatePoint(new Point(0, 0), popupRoot);
                    if (position.HasValue)
                        yOffset = position.Value.Y;
                }

                _toolTip.Offset = new Point(2, yOffset);
            }

            _toolTip.PlacementTarget = popupRoot;
            _toolTip.IsOpen = true;
        }
        else
        {
            _toolTip.IsOpen = false;
        }
    }

    #endregion

    private void CompletionList_InsertionRequested(object? sender, EventArgs e)
    {
        Hide();
        // The window must close before Complete() is called.
        // If the Complete callback pushes stacked input handlers, we don't want to pop those when the CC window closes.
        var item = CompletionList.SelectedItem;
        item?.Complete(TextArea, new AnchorSegment(TextArea.Document, StartOffset, EndOffset - StartOffset), e);
    }

    private void AttachEvents()
    {
        CompletionList.InsertionRequested += CompletionList_InsertionRequested;
        CompletionList.SelectionChanged += CompletionList_SelectionChanged;
        TextArea.Caret.PositionChanged += CaretPositionChanged;
        TextArea.PointerWheelChanged += TextArea_MouseWheel;
        TextArea.TextInput += TextArea_PreviewTextInput;
    }

    /// <inheritdoc/>
    protected override void DetachEvents()
    {
        CompletionList.InsertionRequested -= CompletionList_InsertionRequested;
        CompletionList.SelectionChanged -= CompletionList_SelectionChanged;
        TextArea.Caret.PositionChanged -= CaretPositionChanged;
        TextArea.PointerWheelChanged -= TextArea_MouseWheel;
        TextArea.TextInput -= TextArea_PreviewTextInput;
        base.DetachEvents();
    }

    /// <inheritdoc/>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled)
        {
            CompletionList.HandleKey(e);
        }
    }

    private void TextArea_PreviewTextInput(object? sender, TextInputEventArgs e)
    {
        e.Handled = RaiseEventPair(this, null, TextInputEvent,
            new TextInputEventArgs { Text = e.Text });
    }

    private void TextArea_MouseWheel(object? sender, PointerWheelEventArgs e)
    {
        e.Handled = RaiseEventPair(GetScrollEventTarget(),
            null, PointerWheelChangedEvent, e);
    }

    private Control GetScrollEventTarget()
    {
        /*if (CompletionList == null)
            return this;*/
        return CompletionList.ScrollViewer ?? CompletionList.ListBox ?? (Control)CompletionList;
    }

    /// <summary>
    /// Gets/Sets whether the completion window should close automatically.
    /// The default value is true.
    /// </summary>
    public bool CloseAutomatically { get; set; }

    /// <inheritdoc/>
    protected override bool CloseOnFocusLost => CloseAutomatically;

    /// <summary>
    /// When this flag is set, code completion closes if the caret moves to the
    /// beginning of the allowed range. This is useful in Ctrl+Space and "complete when typing",
    /// but not in dot-completion.
    /// Has no effect if CloseAutomatically is false.
    /// </summary>
    public bool CloseWhenCaretAtBeginning { get; set; }

    private void CaretPositionChanged(object? sender, EventArgs e)
    {
        Debug.WriteLine($"Caret Position changed: {e}");
        var offset = TextArea.Caret.Offset;
        if (offset == StartOffset)
        {
            if (CloseAutomatically && CloseWhenCaretAtBeginning)
            {
                Hide();
            }
            else
            {
                CompletionList.SelectItem(string.Empty);

                IsVisible = CompletionList.ListBox!.ItemCount != 0;
            }
            return;
        }
        if (offset < StartOffset || offset > EndOffset)
        {
            if (CloseAutomatically)
            {
                Hide();
            }
        }
        else
        {
            var document = TextArea.Document;
            if (document != null)
            {
                var newText = document.GetText(StartOffset, offset - StartOffset);
                Debug.WriteLine("CaretPositionChanged newText: " + newText);
                
                // CompletionList.SelectItem(newText);
                Dispatcher.UIThread.Post(() => UpdateQuery(newText));
                // UpdateQuery(newText);
                
                IsVisible = CompletionList.ListBox!.ItemCount != 0;
            }
        }
    }

    private string? lastSearchTerm;
    private int lastCompletionLength;
    
    /// <summary>
    /// Update the completion window's current search term.
    /// </summary>
    public void UpdateQuery(string searchTerm)
    {
        // Fast path if the search term starts with the last search term
        // and the last completion count was less than the max list length
        // (such we won't get new results by searching again)
        if (lastSearchTerm is not null 
            && searchTerm.StartsWith(lastSearchTerm) 
            && lastCompletionLength < MaxListLength)
        {
            CompletionList.SelectItem(searchTerm);
            lastSearchTerm = searchTerm;
            return;
        }
        
        var results = completionProvider.GetCompletions(searchTerm, MaxListLength, true);
        CompletionList.CompletionData.Clear();
        CompletionList.CompletionData.AddRange(results);
        
        CompletionList.SelectItem(searchTerm, true);
        
        lastSearchTerm = searchTerm;
        lastCompletionLength = CompletionList.CompletionData.Count;
    }
}
