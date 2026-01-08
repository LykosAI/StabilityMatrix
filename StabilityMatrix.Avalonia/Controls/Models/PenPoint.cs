using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using SkiaSharp;

namespace StabilityMatrix.Avalonia.Controls.Models;

/// <summary>
/// Custom JSON converter for PenPoint to handle serialization of ulong coordinates
/// and legacy double-based formats.
/// </summary>
public class PenPointJsonConverter : JsonConverter<PenPoint>
{
    public override PenPoint Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    )
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            return default;

        ulong x = 0;
        ulong y = 0;
        double? pressure = null;
        double radius = 1; // Default radius, legacy format stored per-point
        bool isPen = true; // Default to true for rendering

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                break;

            if (reader.TokenType == JsonTokenType.PropertyName)
            {
                var propertyName = reader.GetString();
                reader.Read();

                switch (propertyName?.ToLowerInvariant())
                {
                    case "x":
                        // Handle both double and ulong formats
                        if (reader.TokenType == JsonTokenType.Number)
                        {
                            if (reader.TryGetUInt64(out var ulongX))
                                x = ulongX;
                            else if (reader.TryGetDouble(out var doubleX))
                                x = Convert.ToUInt64(doubleX);
                        }
                        break;

                    case "y":
                        // Handle both double and ulong formats
                        if (reader.TokenType == JsonTokenType.Number)
                        {
                            if (reader.TryGetUInt64(out var ulongY))
                                y = ulongY;
                            else if (reader.TryGetDouble(out var doubleY))
                                y = Convert.ToUInt64(doubleY);
                        }
                        break;

                    case "pressure":
                        if (reader.TokenType == JsonTokenType.Number)
                        {
                            pressure = reader.GetDouble();
                        }
                        break;

                    case "ispen":
                        // Legacy format had IsPen serialized - read it but we'll set true anyway
                        if (reader.TokenType == JsonTokenType.True || reader.TokenType == JsonTokenType.False)
                        {
                            isPen = reader.GetBoolean();
                        }
                        break;

                    case "radius":
                        // Legacy format had Radius on each point - read it for backward compatibility
                        // GetEffectiveRadius() on PenPath will check Points[0].Radius as fallback
                        if (reader.TokenType == JsonTokenType.Number)
                        {
                            radius = reader.GetDouble();
                        }
                        break;

                    default:
                        reader.Skip();
                        break;
                }
            }
        }

        return new PenPoint(x, y)
        {
            Pressure = pressure,
            IsPen = isPen,
            Radius = radius,
        };
    }

    public override void Write(Utf8JsonWriter writer, PenPoint value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("x", value.X);
        writer.WriteNumber("y", value.Y);
        if (value.Pressure.HasValue)
        {
            writer.WriteNumber("pressure", value.Pressure.Value);
        }
        writer.WriteEndObject();
    }
}

[JsonConverter(typeof(PenPointJsonConverter))]
public readonly record struct PenPoint(ulong X, ulong Y)
{
    public PenPoint(double x, double y)
        : this(Convert.ToUInt64(x), Convert.ToUInt64(y)) { }

    public PenPoint(SKPoint skPoint)
        : this(Convert.ToUInt64(skPoint.X), Convert.ToUInt64(skPoint.Y)) { }

    /// <summary>
    /// Radius of the point.
    /// </summary>
    /// <remarks>
    /// Legacy property for backward compatibility. New paths store Radius at the PenPath level.
    /// </remarks>
    [JsonIgnore]
    public double Radius { get; init; } = 1;

    /// <summary>
    /// Optional pressure of the point. If null, the pressure is unknown.
    /// </summary>
    public double? Pressure { get; init; }

    /// <summary>
    /// True if the point was created by a pen, false if it was created by a mouse.
    /// </summary>
    /// <remarks>
    /// Runtime-only property for pressure-sensitive rendering. Not persisted.
    /// </remarks>
    [JsonIgnore]
    public bool IsPen { get; init; }

    public SKPoint ToSKPoint() => new(X, Y);
}
