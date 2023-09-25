using System.Text.Json.Serialization;
using StabilityMatrix.Core.Converters.Json;

namespace StabilityMatrix.Avalonia.Models.TagCompletion;

[JsonConverter(typeof(DefaultUnknownEnumConverter<TagType>))]
public enum TagType
{
    Unknown,
    Invalid,
    General,
    Artist,
    Copyright,
    Character,
    Species,
    Meta,
    Lore
}

public static class TagTypeExtensions
{
    public static TagType FromE621(int tag)
    {
        return tag switch
        {
            -1 => TagType.Invalid,
            0 => TagType.General,
            1 => TagType.Artist,
            3 => TagType.Copyright,
            4 => TagType.Character,
            5 => TagType.Species,
            6 => TagType.Invalid,
            7 => TagType.Meta,
            8 => TagType.Lore,
            _ => TagType.Unknown
        };
    }
}
