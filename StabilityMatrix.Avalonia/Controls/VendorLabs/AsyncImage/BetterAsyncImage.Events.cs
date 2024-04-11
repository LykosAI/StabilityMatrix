using System;
using Avalonia.Interactivity;

namespace StabilityMatrix.Avalonia.Controls.VendorLabs;

public partial class BetterAsyncImage
{
    /// <summary>
    /// Deines the <see cref="Opened"/> event
    /// </summary>
    public static readonly RoutedEvent<RoutedEventArgs> OpenedEvent = RoutedEvent.Register<
        BetterAsyncImage,
        RoutedEventArgs
    >(nameof(Opened), RoutingStrategies.Bubble);

    /// <summary>
    /// Deines the <see cref="Failed"/> event
    /// </summary>
    public static readonly RoutedEvent<global::Avalonia.Labs.Controls.AsyncImage.AsyncImageFailedEventArgs> FailedEvent =
        RoutedEvent.Register<
            BetterAsyncImage,
            global::Avalonia.Labs.Controls.AsyncImage.AsyncImageFailedEventArgs
        >(nameof(Failed), RoutingStrategies.Bubble);

    /// <summary>
    /// Occurs when the image is successfully loaded.
    /// </summary>
    public event EventHandler<RoutedEventArgs>? Opened
    {
        add => AddHandler(OpenedEvent, value);
        remove => RemoveHandler(OpenedEvent, value);
    }

    /// <summary>
    /// Occurs when the image fails to load the uri provided.
    /// </summary>
    public event EventHandler<global::Avalonia.Labs.Controls.AsyncImage.AsyncImageFailedEventArgs>? Failed
    {
        add => AddHandler(FailedEvent, value);
        remove => RemoveHandler(FailedEvent, value);
    }
}
