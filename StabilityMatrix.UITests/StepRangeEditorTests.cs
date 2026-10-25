using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.ComponentModel;
using FluentAvalonia.UI.Controls;
using StabilityMatrix.Avalonia.Controls;

namespace StabilityMatrix.UITests;

[Collection("TempDir")]
public partial class StepRangeEditorTests
{
    [AvaloniaFact]
    public void Show_KeepsBoundRange()
    {
        var (_, source) = ShowEditor(0.2, 0.8);

        Assert.Equal((0.2, 0.8), (source.Start, source.End));
    }

    [AvaloniaFact]
    public void StartBox_PastEnd_CarriesEndAlong()
    {
        var (editor, source) = ShowEditor(0, 0.5);

        GetBox(editor, "PART_StartBox").Value = 0.6;

        Assert.Equal((0.6, 0.6), (source.Start, source.End));
    }

    [AvaloniaFact]
    public void EndBox_BeforeStart_CarriesStartAlong()
    {
        var (editor, source) = ShowEditor(0.4, 0.8);

        GetBox(editor, "PART_EndBox").Value = 0.3;

        Assert.Equal((0.3, 0.3), (source.Start, source.End));
    }

    [AvaloniaFact]
    public void EndBox_Past100Percent_StaysAt100Percent()
    {
        var (editor, source) = ShowEditor(0, 0.5);

        GetBox(editor, "PART_EndBox").Value = 10;

        Assert.Equal(1, source.End);
    }

    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void Source_SetsBothInEitherOrder_LandsOnNewRange(bool startFirst)
    {
        var (_, source) = ShowEditor(0, 0.5);

        // Past the old range, then before it, as a saved project load would.
        foreach (var (start, end) in new[] { (0.6, 0.9), (0.1, 0.3) })
        {
            if (startFirst)
            {
                source.Start = start;
                source.End = end;
            }
            else
            {
                source.End = end;
                source.Start = start;
            }

            Assert.Equal((start, end), (source.Start, source.End));
        }
    }

    [AvaloniaFact]
    public void ClearedBox_KeepsValueAndOrdering()
    {
        var (editor, source) = ShowEditor(0.2, 0.8);

        var startBox = GetBox(editor, "PART_StartBox");
        startBox.Value = double.NaN;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal((0.2, 0.2), (source.Start, startBox.Value));

        GetBox(editor, "PART_EndBox").Value = 0.1;
        Assert.Equal((0.1, 0.1), (source.Start, source.End));
    }

    [AvaloniaFact]
    public void Source_OutOfRange_IsClampedInSource()
    {
        var (_, source) = ShowEditor(0, 0.5);

        source.End = 5;

        Assert.Equal(1, source.End);
    }

    [AvaloniaFact]
    public void LateDataContext_OutOfRangeSource_IsClampedInSource()
    {
        // An old ControlNet End from when its box allowed up to 1000%, arriving after the template.
        var (editor, source) = ShowEditor(0, 5, dataContextAfterShow: true);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal((1d, 1d), (editor.End, source.End));
    }

    private static (StepRangeEditor, RangeSource) ShowEditor(
        double start,
        double end,
        bool dataContextAfterShow = false
    )
    {
        var source = new RangeSource { Start = start, End = end };
        var editor = new StepRangeEditor
        {
            [!StepRangeEditor.StartProperty] = new Binding(nameof(RangeSource.Start)),
            [!StepRangeEditor.EndProperty] = new Binding(nameof(RangeSource.End)),
        };

        if (!dataContextAfterShow)
            editor.DataContext = source;

        new Window { Content = editor }.Show();

        if (dataContextAfterShow)
            editor.DataContext = source;

        return (editor, source);
    }

    private static NumberBox GetBox(StepRangeEditor editor, string name) =>
        editor.GetVisualDescendants().OfType<NumberBox>().Single(box => box.Name == name);

    private partial class RangeSource : ObservableObject
    {
        [ObservableProperty]
        public partial double Start { get; set; }

        [ObservableProperty]
        public partial double End { get; set; }
    }
}
