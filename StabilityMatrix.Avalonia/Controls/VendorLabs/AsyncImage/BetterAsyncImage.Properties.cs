using System;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Labs.Controls;
using Avalonia.Media;
using StabilityMatrix.Avalonia.Controls.VendorLabs.Cache;

namespace StabilityMatrix.Avalonia.Controls.VendorLabs;

public partial class BetterAsyncImage
{
    /// <summary>
    /// Defines the <see cref="PlaceholderSource"/> property.
    /// </summary>
    public static readonly StyledProperty<IImage?> PlaceholderSourceProperty = AvaloniaProperty.Register<
        BetterAsyncImage,
        IImage?
    >(nameof(PlaceholderSource));

    /// <summary>
    /// Defines the <see cref="Source"/> property.
    /// </summary>
    public static readonly StyledProperty<Uri?> SourceProperty = AvaloniaProperty.Register<
        BetterAsyncImage,
        Uri?
    >(nameof(Source));

    /// <summary>
    /// Defines the <see cref="Stretch"/> property.
    /// </summary>
    public static readonly StyledProperty<Stretch> StretchProperty = AvaloniaProperty.Register<
        BetterAsyncImage,
        Stretch
    >(nameof(Stretch), Stretch.Uniform);

    /// <summary>
    /// Defines the <see cref="PlaceholderStretch"/> property.
    /// </summary>
    public static readonly StyledProperty<Stretch> PlaceholderStretchProperty = AvaloniaProperty.Register<
        BetterAsyncImage,
        Stretch
    >(nameof(PlaceholderStretch), Stretch.Uniform);

    /// <summary>
    /// Defines the <see cref="State"/> property.
    /// </summary>
    public static readonly DirectProperty<BetterAsyncImage, AsyncImageState> StateProperty =
        AvaloniaProperty.RegisterDirect<BetterAsyncImage, AsyncImageState>(
            nameof(State),
            o => o.State,
            (o, v) => o.State = v
        );

    /// <summary>
    /// Defines the <see cref="ImageTransition"/> property.
    /// </summary>
    public static readonly StyledProperty<IPageTransition?> ImageTransitionProperty =
        AvaloniaProperty.Register<BetterAsyncImage, IPageTransition?>(
            nameof(ImageTransition),
            new CrossFade(TimeSpan.FromSeconds(0.25))
        );

    /// <summary>
    /// Defines the <see cref="IsCacheEnabled"/> property.
    /// </summary>
    public static readonly DirectProperty<BetterAsyncImage, bool> IsCacheEnabledProperty =
        AvaloniaProperty.RegisterDirect<BetterAsyncImage, bool>(
            nameof(IsCacheEnabled),
            o => o.IsCacheEnabled,
            (o, v) => o.IsCacheEnabled = v
        );
    private bool _isCacheEnabled;

    /// <summary>
    /// Gets or sets the placeholder image.
    /// </summary>
    public IImage? PlaceholderSource
    {
        get => GetValue(PlaceholderSourceProperty);
        set => SetValue(PlaceholderSourceProperty, value);
    }

    /// <summary>
    /// Gets or sets the uri pointing to the image resource
    /// </summary>
    public Uri? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    /// <summary>
    /// Gets or sets a value controlling how the image will be stretched.
    /// </summary>
    public Stretch Stretch
    {
        get { return GetValue(StretchProperty); }
        set { SetValue(StretchProperty, value); }
    }

    /// <summary>
    /// Gets or sets a value controlling how the placeholder will be stretched.
    /// </summary>
    public Stretch PlaceholderStretch
    {
        get { return GetValue(StretchProperty); }
        set { SetValue(StretchProperty, value); }
    }

    /// <summary>
    /// Gets the current loading state of the image.
    /// </summary>
    public AsyncImageState State
    {
        get => _state;
        private set => SetAndRaise(StateProperty, ref _state, value);
    }

    /// <summary>
    /// Gets or sets the transition to run when the image is loaded.
    /// </summary>
    public IPageTransition? ImageTransition
    {
        get => GetValue(ImageTransitionProperty);
        set => SetValue(ImageTransitionProperty, value);
    }

    /// <summary>
    /// Gets or sets whether to use cache for retrieved images
    /// </summary>
    public bool IsCacheEnabled
    {
        get => _isCacheEnabled;
        set => SetAndRaise(IsCacheEnabledProperty, ref _isCacheEnabled, value);
    }

    public static readonly AttachedProperty<IImageCache?> ImageCacheProperty =
        AvaloniaProperty.RegisterAttached<BetterAsyncImage, Control, IImageCache?>("ImageCache", null, true);

    public IImageCache? ImageCache
    {
        get => GetValue(ImageCacheProperty);
        set => SetValue(ImageCacheProperty, value);
    }

    public static void SetImageCache(Control control, IImageCache? value)
    {
        control.SetValue(ImageCacheProperty, value);
    }

    public static IImageCache? GetImageCache(Control control)
    {
        return control.GetValue(ImageCacheProperty);
    }
}
