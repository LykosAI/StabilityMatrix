using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace StabilityMatrix.Models;

public partial class LaunchOption : ObservableObject
{
    public string Name { get; init; }
    public string Type { get; init; }

    [ObservableProperty]
    private object optionValue;
    
    public void SetValueFromString(string? value)
    {
        switch (Type)
        {
            case "bool":
                OptionValue = bool.TryParse(value, out var boolValue) && boolValue;
                break;
            case "int":
                OptionValue = int.TryParse(value, out var intValue) ? intValue : 0;
                break;
            case "double":
                OptionValue = double.TryParse(value, out var floatValue) ? floatValue : 0;
                break;
            case "string":
                OptionValue = value ?? "";
                break;
            default:
                throw new ArgumentException($"Unknown option type {Type}");
        }
    }
}
