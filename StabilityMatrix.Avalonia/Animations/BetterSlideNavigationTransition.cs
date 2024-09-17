using System;
using System.Threading;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Media;
using Avalonia.Styling;
using FluentAvalonia.UI.Media.Animation;

namespace StabilityMatrix.Avalonia.Animations;

public class BetterSlideNavigationTransition : BaseTransitionInfo
{
    public override TimeSpan Duration { get; set; } = TimeSpan.FromMilliseconds(167);

    /// <summary>
    /// Gets or sets the type of animation effect to play during the slide transition.
    /// </summary>
    public SlideNavigationTransitionEffect Effect { get; set; } = SlideNavigationTransitionEffect.FromRight;

    /// <summary>
    /// Gets or sets the HorizontalOffset used when animating from the Left or Right
    /// </summary>
    public double FromHorizontalOffset { get; set; } = 56;

    /// <summary>
    /// Gets or sets the VerticalOffset used when animating from the Top or Bottom
    /// </summary>
    public double FromVerticalOffset { get; set; } = 56;

    /// <summary>
    /// Gets or sets the easing function applied to the slide transition.
    /// </summary>
    public Easing Easing { get; set; } = new SplineEasing(0.1, 0.9, 0.2, 1.0);

    public override async void RunAnimation(Animatable ctrl, CancellationToken cancellationToken)
    {
        double length = 0;
        bool isVertical = false;
        switch (Effect)
        {
            case SlideNavigationTransitionEffect.FromLeft:
                length = -FromHorizontalOffset;
                break;
            case SlideNavigationTransitionEffect.FromRight:
                length = FromHorizontalOffset;
                break;
            case SlideNavigationTransitionEffect.FromTop:
                length = -FromVerticalOffset;
                isVertical = true;
                break;
            case SlideNavigationTransitionEffect.FromBottom:
                length = FromVerticalOffset;
                isVertical = true;
                break;
        }

        var animation = new Animation
        {
            Easing = Easing,
            Children =
            {
                new KeyFrame
                {
                    Setters =
                    {
                        new Setter(
                            isVertical ? TranslateTransform.YProperty : TranslateTransform.XProperty,
                            length
                        ),
                        new Setter(Visual.OpacityProperty, 0d)
                    },
                    Cue = new Cue(0d)
                },
                new KeyFrame { Setters = { new Setter(Visual.OpacityProperty, 1d) }, Cue = new Cue(0.05d) },
                new KeyFrame
                {
                    Setters =
                    {
                        new Setter(Visual.OpacityProperty, 1d),
                        new Setter(
                            isVertical ? TranslateTransform.YProperty : TranslateTransform.XProperty,
                            0.0
                        )
                    },
                    Cue = new Cue(1d)
                }
            },
            Duration = Duration,
            FillMode = FillMode.Forward
        };

        await animation.RunAsync(ctrl, cancellationToken);

        if (ctrl is Visual visual)
        {
            visual.Opacity = 1;
        }
    }

    public static BetterSlideNavigationTransition PageSlideFromLeft =>
        new()
        {
            Duration = TimeSpan.FromMilliseconds(300),
            Effect = SlideNavigationTransitionEffect.FromLeft,
            FromHorizontalOffset = 150,
            Easing = new SplineEasing(0.6, 0.4, 0.1, 0.1)
        };

    public static BetterSlideNavigationTransition PageSlideFromRight =>
        new()
        {
            Duration = TimeSpan.FromMilliseconds(300),
            Effect = SlideNavigationTransitionEffect.FromRight,
            FromHorizontalOffset = 150,
            Easing = new SplineEasing(0.6, 0.4, 0.1, 0.1)
        };
}
