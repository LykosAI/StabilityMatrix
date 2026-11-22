using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.Xaml.Interactivity;
using StabilityMatrix.Avalonia.Behaviors;

namespace StabilityMatrix.UITests;

public class ItemsRepeaterHiddenChangesBehaviorTests
{
    [AvaloniaFact]
    public void ItemsAddedWhileHidden_AreRealizedWhenShownAgain()
    {
        var items = new ObservableCollection<string>(
            Enumerable.Range(0, 40).Select(i => $"old {i}").Reverse()
        );

        // Same shape as the Inference image grid: newest first, uniform tiles, large cache
        var repeater = new ItemsRepeater
        {
            ItemsSource = items,
            VerticalCacheLength = 8,
            Layout = new UniformGridLayout { MinColumnSpacing = 2, MinRowSpacing = 2 },
            ItemTemplate = new FuncDataTemplate<string>((_, _) => new Border { Width = 150, Height = 190 }),
        };
        Interaction.GetBehaviors(repeater).Add(new ItemsRepeaterHiddenChangesBehavior());

        var scrollViewer = new ScrollViewer { Content = repeater };
        var window = new Window
        {
            Width = 340,
            Height = 700,
            Content = scrollViewer,
        };
        window.Show();

        try
        {
            RunLayout();
            Assert.NotNull(repeater.TryGetElement(0));

            scrollViewer.IsVisible = false;
            RunLayout();

            for (var i = 0; i < 20; i++)
            {
                items.Insert(0, $"new {i}");
                RunLayout();
            }

            scrollViewer.IsVisible = true;
            RunLayout();

            items.Insert(0, "after shown");
            RunLayout();

            Assert.Equal(0, scrollViewer.Offset.Y);
            for (var i = 0; i < 4; i++)
            {
                var element = repeater.TryGetElement(i);
                Assert.NotNull(element);
                Assert.Equal(items[i], element.DataContext);
                Assert.True(element.Bounds.Width > 0, $"Item {i} was realized but not arranged");
            }
        }
        finally
        {
            window.Close();
        }
    }

    private static void RunLayout()
    {
        for (var i = 0; i < 3; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
        Dispatcher.UIThread.RunJobs();
    }
}
