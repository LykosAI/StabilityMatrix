using System;
using System.ComponentModel;
using System.Reactive.Linq;
using System.Threading;
using Avalonia.Input;
using Avalonia.Interactivity;
using StabilityMatrix.Avalonia.Models;

namespace StabilityMatrix.Avalonia.Controls;

public class ResizableUserControlBase : UserControlBase
{
    protected virtual Action? OnResizeFactorChanged => null;
    protected virtual double MaxResizeFactor => 2.0d;
    protected virtual double MinResizeFactor => 0.5d;

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        if (DataContext is not IResizable viewModel)
            return;

        Observable
            .FromEventPattern<PropertyChangedEventArgs>(viewModel, nameof(PropertyChanged))
            .Where(x => x.EventArgs.PropertyName == nameof(viewModel.ResizeFactor))
            .Throttle(TimeSpan.FromMilliseconds(5))
            .ObserveOn(SynchronizationContext.Current!)
            .Subscribe(_ =>
            {
                OnResizeFactorChanged?.Invoke();
            });
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        if (e.KeyModifiers != KeyModifiers.Control)
            return;

        if (DataContext is not IResizable resizable)
            return;

        if (e.Delta.Y > 0)
        {
            if (resizable.ResizeFactor >= MaxResizeFactor)
                return;
            resizable.ResizeFactor += 0.05d;
        }
        else
        {
            if (resizable.ResizeFactor <= MinResizeFactor)
                return;
            resizable.ResizeFactor -= 0.05d;
        }

        OnResizeFactorChanged?.Invoke();

        e.Handled = true;
    }
}
