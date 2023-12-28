using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;

namespace StabilityMatrix.Avalonia.Controls;

public class BetterComboBox : ComboBox
{
    // protected override Type StyleKeyOverride { get; } = typeof(CheckBox);

    public static readonly DirectProperty<BetterComboBox, IDataTemplate?> SelectionBoxItemTemplateProperty =
        AvaloniaProperty.RegisterDirect<BetterComboBox, IDataTemplate?>(
            nameof(SelectionBoxItemTemplate),
            v => v.SelectionBoxItemTemplate,
            (x, v) => x.SelectionBoxItemTemplate = v
        );

    private IDataTemplate? _selectionBoxItemTemplate;

    public IDataTemplate? SelectionBoxItemTemplate
    {
        get => _selectionBoxItemTemplate;
        private set => SetAndRaise(SelectionBoxItemTemplateProperty, ref _selectionBoxItemTemplate, value);
    }

    /// <inheritdoc />
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
    }
}
