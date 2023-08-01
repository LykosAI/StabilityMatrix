using System;

namespace StabilityMatrix.Avalonia.Models;

public interface IRemovableListItem
{
    public event EventHandler ParentListRemoveRequested;
}
