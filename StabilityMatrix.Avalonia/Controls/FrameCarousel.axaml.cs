using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using DynamicData.Binding;
using FluentAvalonia.UI.Controls;
using FluentAvalonia.UI.Media.Animation;
using FluentAvalonia.UI.Navigation;
using StabilityMatrix.Avalonia.Animations;
using StabilityMatrix.Core.Extensions;

namespace StabilityMatrix.Avalonia.Controls;

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
public class FrameCarousel : SelectingItemsControl
{
    public static readonly StyledProperty<IDataTemplate?> ContentTemplateProperty = AvaloniaProperty.Register<FrameCarousel, IDataTemplate?>(
        "ContentTemplate");

    public IDataTemplate? ContentTemplate
    {
        get => GetValue(ContentTemplateProperty);
        set => SetValue(ContentTemplateProperty, value);
    }

    public static readonly StyledProperty<Type> SourcePageTypeProperty = AvaloniaProperty.Register<FrameCarousel, Type>(
        "SourcePageType");

    public Type SourcePageType
    {
        get => GetValue(SourcePageTypeProperty);
        set => SetValue(SourcePageTypeProperty, value);
    }

    private Frame? frame;
    private int previousIndex = -1;

    private static readonly FrameNavigationOptions ForwardNavigationOptions
        = new()
        {
            TransitionInfoOverride = new BetterSlideNavigationTransition
            {
                Effect = SlideNavigationTransitionEffect.FromRight,
                FromHorizontalOffset = 200
            }
        };
    
    private static readonly FrameNavigationOptions BackNavigationOptions
        = new()
        {
            TransitionInfoOverride = new BetterSlideNavigationTransition
            {
                Effect = SlideNavigationTransitionEffect.FromLeft,
                FromHorizontalOffset = 200
            }
        };
    
    private static readonly FrameNavigationOptions DirectionlessNavigationOptions
        = new()
        {
            TransitionInfoOverride = new SuppressNavigationTransitionInfo()
        };
    
    private void ItemsView_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (frame is null) return;
        
        // If current size is 0, reset the frame cache
        var count = ItemCount;
        if (count == 0)
        {
            frame.ClearValue(Frame.CacheSizeProperty);
            // Setting this to false clears the page cache and stack caches
            frame.IsNavigationStackEnabled = false;
        }
        else
        {
            frame.SetValue(Frame.CacheSizeProperty, count);
        }
    }
    
    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        
        frame = e.NameScope.Find<Frame>("PART_Frame") 
                ?? throw new NullReferenceException("Frame not found");
        
        frame.NavigationPageFactory = new FrameNavigationFactory(SourcePageType);

        ItemsView.CollectionChanged += ItemsView_CollectionChanged;

        if (SelectedItem is not null)
        {
            frame.NavigateFromObject(SelectedItem, DirectionlessNavigationOptions);
        }
    }

    /// <inheritdoc />
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        
        if (SelectedItem is not null)
        {
            frame!.NavigateFromObject(SelectedItem, DirectionlessNavigationOptions);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == SelectedItemProperty && frame is not null)
        {
            var value = change.GetNewValue<object?>();
            
            if (SelectedIndex > previousIndex)
            {
                // Going forward
                frame.NavigateFromObject(value, ForwardNavigationOptions);
            }
            else if (SelectedIndex < previousIndex)
            {
                // Going back
                frame.NavigateFromObject(value, BackNavigationOptions);
            }
            else
            {
                frame.NavigateFromObject(value, DirectionlessNavigationOptions);
            }
            
            previousIndex = SelectedIndex;
        }
    }
    
    /// <summary>
    /// Moves to the next item in the carousel.
    /// </summary>
    public void Next()
    {
        if (SelectedIndex < ItemCount - 1)
        {
            ++SelectedIndex;
        }
    }

    /// <summary>
    /// Moves to the previous item in the carousel.
    /// </summary>
    public void Previous()
    {
        if (SelectedIndex > 0)
        {
            --SelectedIndex;
        }
    }
    
    internal class FrameNavigationFactory : INavigationPageFactory
    {
        private readonly Type _sourcePageType;
        
        public FrameNavigationFactory(Type sourcePageType)
        {
            _sourcePageType = sourcePageType;
        }
        
        /// <inheritdoc />
        public Control GetPage(Type srcType)
        {
            return (Control) Activator.CreateInstance(srcType)!;
        }

        /// <inheritdoc />
        public Control GetPageFromObject(object target)
        {
            var view = GetPage(_sourcePageType);
            view.DataContext = target;
            return view;
        }
    }
}
