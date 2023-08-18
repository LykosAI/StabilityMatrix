using System;
using System.Threading;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Media;
using Avalonia.Styling;

namespace StabilityMatrix.Avalonia.Animations;

public class BetterDrillInNavigationTransition : BaseTransitionInfo
{
    /// <summary>
    /// Gets or sets whether the animation should drill in (false) or drill out (true)
    /// </summary>
    public bool IsReversed { get; set; } = false; //Zoom out if true

    public override TimeSpan Duration { get; set; } = TimeSpan.FromMilliseconds(400);

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
                        new Setter(ScaleTransform.ScaleXProperty, IsReversed ? 1.5 : 0.0),
                        new Setter(ScaleTransform.ScaleYProperty, IsReversed ? 1.5 : 0.0)
                    },
                    Cue = new Cue(0d)
                },
                new KeyFrame
                {
                    Setters =
                    {
                        new Setter(Visual.OpacityProperty, 1.0),
                        new Setter(ScaleTransform.ScaleXProperty, IsReversed ? 1.0 : 1.0),
                        new Setter(ScaleTransform.ScaleYProperty, IsReversed ? 1.0 : 1.0)
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
