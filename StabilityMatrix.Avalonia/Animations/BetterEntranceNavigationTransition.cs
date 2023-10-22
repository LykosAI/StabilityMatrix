using System;
using System.Threading;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Media;
using Avalonia.Styling;

namespace StabilityMatrix.Avalonia.Animations;

public class BetterEntranceNavigationTransition : BaseTransitionInfo
{
    public override TimeSpan Duration { get; set; } = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// Gets or sets the Horizontal Offset used when animating
    /// </summary>
    public double FromHorizontalOffset { get; set; } = 0;

    /// <summary>
    /// Gets or sets the Vertical Offset used when animating
    /// </summary>
    public double FromVerticalOffset { get; set; } = 100;

    public override async void RunAnimation(Animatable ctrl, CancellationToken cancellationToken)
    {
        var animation = new Animation
        {
            Easing = new SplineEasing(0.1, 0.9, 0.2, 1.0),
            Children =
            {
                new KeyFrame
                {
                    Setters =
                    {
                        new Setter(Visual.OpacityProperty, 0.0),
                        new Setter(TranslateTransform.XProperty, FromHorizontalOffset),
                        new Setter(TranslateTransform.YProperty, FromVerticalOffset)
                    },
                    Cue = new Cue(0d)
                },
                new KeyFrame
                {
                    Setters =
                    {
                        new Setter(Visual.OpacityProperty, 1d),
                        new Setter(TranslateTransform.XProperty, 0.0),
                        new Setter(TranslateTransform.YProperty, 0.0)
                    },
                    Cue = new Cue(1d)
                }
            },
            Duration = Duration,
            FillMode = FillMode.Forward
        };

        await animation.RunAsync(ctrl, cancellationToken);

        if (ctrl is Visual visualCtrl)
        {
            visualCtrl.Opacity = 1;
        }
    }
}
