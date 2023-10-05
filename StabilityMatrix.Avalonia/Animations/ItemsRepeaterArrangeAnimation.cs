using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Rendering.Composition;
using Avalonia.Rendering.Composition.Animations;

namespace StabilityMatrix.Avalonia.Animations;

public class ItemsRepeaterArrangeAnimation : AvaloniaObject
{
    public static readonly AttachedProperty<bool> EnableItemsArrangeAnimationProperty =
        AvaloniaProperty.RegisterAttached<ItemsRepeater, bool>(
            "EnableItemsArrangeAnimation",
            typeof(ItemsRepeaterArrangeAnimation)
        );

    static ItemsRepeaterArrangeAnimation()
    {
        EnableItemsArrangeAnimationProperty.Changed.AddClassHandler<ItemsRepeater>(
            OnEnableItemsArrangeAnimationChanged
        );
    }

    private static void OnEnableItemsArrangeAnimationChanged(
        ItemsRepeater itemsRepeater,
        AvaloniaPropertyChangedEventArgs eventArgs
    )
    {
        if (eventArgs.NewValue is true)
        {
            itemsRepeater.ElementPrepared += OnElementPrepared;
            itemsRepeater.ElementIndexChanged += OnElementIndexChanged;
        }
        else
        {
            // itemsRepeater.Opened -= OnOpened;
        }
    }

    private static void CreateAnimation(Visual item)
    {
        var compositionVisual =
            ElementComposition.GetElementVisual(item) ?? throw new NullReferenceException();

        if (compositionVisual.ImplicitAnimations is { } animations && animations.HasKey("Offset"))
        {
            return;
        }

        var compositor = compositionVisual.Compositor;

        var offsetAnimation = compositor.CreateVector3KeyFrameAnimation();
        offsetAnimation.Target = "Offset";
        // Using the "this.FinalValue" to indicate the last value of the Offset property
        offsetAnimation.InsertExpressionKeyFrame(1.0f, "this.FinalValue");
        offsetAnimation.Duration = TimeSpan.FromMilliseconds(150);

        // Create a new implicit animation collection and bind the offset animation
        var implicitAnimationCollection = compositor.CreateImplicitAnimationCollection();
        implicitAnimationCollection["Offset"] = offsetAnimation;
        compositionVisual.ImplicitAnimations = implicitAnimationCollection;
    }

    private static void OnElementPrepared(object? sender, ItemsRepeaterElementPreparedEventArgs e)
    {
        if (
            sender is not ItemsRepeater itemsRepeater
            || !GetEnableItemsArrangeAnimation(itemsRepeater)
        )
            return;

        CreateAnimation(itemsRepeater);
    }

    private static void OnElementIndexChanged(
        object? sender,
        ItemsRepeaterElementIndexChangedEventArgs e
    )
    {
        if (
            sender is not ItemsRepeater itemsRepeater
            || !GetEnableItemsArrangeAnimation(itemsRepeater)
        )
            return;

        CreateAnimation(itemsRepeater);
    }

    /*private static void OnOpened(object sender, EventArgs e)
    {
        if (sender is not WindowBase windowBase || !GetEnableScaleShowAnimation(windowBase))
            return;

        // Here we explicitly animate the "Scale" property
        // The implementation is the same as `Offset` at the beginning, but just with the Scale property
        windowBase.StartWindowScaleAnimation();
    }*/

    public static bool GetEnableItemsArrangeAnimation(ItemsRepeater element)
    {
        return element.GetValue(EnableItemsArrangeAnimationProperty);
    }

    public static void SetEnableItemsArrangeAnimation(ItemsRepeater element, bool value)
    {
        element.SetValue(EnableItemsArrangeAnimationProperty, value);
    }
}
