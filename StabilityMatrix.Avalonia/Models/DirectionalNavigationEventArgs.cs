using System;
using System.Numerics;

namespace StabilityMatrix.Avalonia.Models;

public class DirectionalNavigationEventArgs : EventArgs
{
    private Vector2 Direction { get; }

    public DirectionalNavigationEventArgs(Vector2 direction)
    {
        Direction = direction;
    }

    public static DirectionalNavigationEventArgs Up => new(new Vector2(0, -1));
    public static DirectionalNavigationEventArgs Down => new(new Vector2(0, 1));

    public bool IsNext => Direction.X > 0 || Direction.Y > 0;
    public bool IsPrevious => Direction.X < 0 || Direction.Y < 0;
}
