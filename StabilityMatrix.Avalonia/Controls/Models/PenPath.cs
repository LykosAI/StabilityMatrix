using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using SkiaSharp;
using StabilityMatrix.Core.Converters.Json;

namespace StabilityMatrix.Avalonia.Controls.Models;

/// <summary>
/// Type of path - determines how the path is rendered.
/// </summary>
public enum PenPathType
{
    /// <summary>
    /// Freehand brush strokes (default).
    /// </summary>
    Freehand,

    /// <summary>
    /// Filled rectangle shape.
    /// </summary>
    Rectangle,

    /// <summary>
    /// Filled ellipse/oval shape.
    /// </summary>
    Ellipse,

    /// <summary>
    /// Bitmap image (used for flood fill results).
    /// </summary>
    Bitmap,
}

/// <summary>
/// Custom JSON converter for PenPath that handles both legacy (JSON array)
/// and new (compressed base64 string) formats for backwards compatibility.
/// </summary>
public class PenPathJsonConverter : JsonConverter<PenPath>
{
    public override PenPath Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            return default;

        var penPath = new PenPath();

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                return penPath;

            if (reader.TokenType != JsonTokenType.PropertyName)
                continue;

            var propertyName = reader.GetString()?.ToLowerInvariant();
            reader.Read();

            switch (propertyName)
            {
                case "points":
                    // Handle both legacy (array) and new (compressed string) formats
                    if (reader.TokenType == JsonTokenType.String)
                    {
                        // New compressed format
                        var compressed = reader.GetString();
                        var decompressedPoints = PenPath.DecompressPointsPublic(compressed);
                        penPath = penPath with { Points = decompressedPoints ?? [] };
                    }
                    else if (reader.TokenType == JsonTokenType.StartArray)
                    {
                        // Legacy format - manually deserialize array of PenPoint objects
                        // (Can't use JsonSerializer.Deserialize due to source-gen context limitations)
                        var points = new List<PenPoint>();
                        var penPointConverter = new PenPointJsonConverter();

                        while (reader.Read())
                        {
                            if (reader.TokenType == JsonTokenType.EndArray)
                                break;

                            if (reader.TokenType == JsonTokenType.StartObject)
                            {
                                var point = penPointConverter.Read(ref reader, typeof(PenPoint), options);
                                points.Add(point);
                            }
                        }

                        penPath = penPath with { Points = points };
                    }
                    break;

                case "fillcolor":
                    var colorConverter = new SKColorJsonConverter();
                    var color = colorConverter.Read(ref reader, typeof(SKColor), options);
                    penPath = penPath with { FillColor = color };
                    break;

                case "iserase":
                    penPath = penPath with { IsErase = reader.GetBoolean() };
                    break;

                case "pathtype":
                    // Handle both string and number formats for backward compatibility
                    if (reader.TokenType == JsonTokenType.String)
                    {
                        if (Enum.TryParse<PenPathType>(reader.GetString(), out var pathType))
                            penPath = penPath with { PathType = pathType };
                    }
                    else if (reader.TokenType == JsonTokenType.Number)
                    {
                        var pathTypeInt = reader.GetInt32();
                        if (Enum.IsDefined(typeof(PenPathType), pathTypeInt))
                            penPath = penPath with { PathType = (PenPathType)pathTypeInt };
                    }
                    break;

                case "bounds":
                    var rectConverter = new SKRectJsonConverter();
                    var bounds = rectConverter.Read(ref reader, typeof(SKRect), options);
                    penPath = penPath with { Bounds = bounds };
                    break;

                case "isstrokeonly":
                    penPath = penPath with { IsStrokeOnly = reader.GetBoolean() };
                    break;

                case "strokewidth":
                    penPath = penPath with { StrokeWidth = (float)reader.GetDouble() };
                    break;

                case "radius":
                    penPath = penPath with { Radius = (float)reader.GetDouble() };
                    break;

                default:
                    reader.Skip();
                    break;
            }
        }

        return penPath;
    }

    public override void Write(Utf8JsonWriter writer, PenPath value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        // Write FillColor
        var colorConverter = new SKColorJsonConverter();
        writer.WritePropertyName("fillColor");
        colorConverter.Write(writer, value.FillColor, options);

        writer.WriteBoolean("isErase", value.IsErase);
        writer.WriteString("pathType", value.PathType.ToString());

        // Write Bounds
        var rectConverter = new SKRectJsonConverter();
        writer.WritePropertyName("bounds");
        rectConverter.Write(writer, value.Bounds, options);

        writer.WriteBoolean("isStrokeOnly", value.IsStrokeOnly);
        writer.WriteNumber("strokeWidth", value.StrokeWidth);
        writer.WriteNumber("radius", value.Radius);

        // Write points in compressed format
        var compressedPoints = PenPath.CompressPointsPublic(value.Points);
        if (compressedPoints != null)
        {
            writer.WriteString("points", compressedPoints);
        }

        writer.WriteEndObject();
    }
}

[JsonConverter(typeof(PenPathJsonConverter))]
public readonly record struct PenPath()
{
    public SKColor FillColor { get; init; }

    public bool IsErase { get; init; }

    /// <summary>
    /// Type of path (Freehand, Rectangle, or Ellipse).
    /// </summary>
    public PenPathType PathType { get; init; } = PenPathType.Freehand;

    /// <summary>
    /// Bounding rectangle for shape paths (Rectangle, Ellipse).
    /// For Freehand paths, this is ignored.
    /// </summary>
    public SKRect Bounds { get; init; }

    /// <summary>
    /// If true, draws shape outline only (stroke). If false, fills the shape.
    /// Only applies to Rectangle and Ellipse path types.
    /// </summary>
    public bool IsStrokeOnly { get; init; }

    /// <summary>
    /// Stroke width for stroke-only shapes. Only used when IsStrokeOnly is true.
    /// </summary>
    public float StrokeWidth { get; init; } = 5f;

    /// <summary>
    /// Brush radius for this stroke. All points in the stroke share this radius.
    /// </summary>
    public float Radius { get; init; }

    /// <summary>
    /// Points for rendering. Serialization is handled by the custom JsonConverter.
    /// </summary>
    [JsonIgnore]
    public List<PenPoint> Points { get; init; } = [];

    /// <summary>
    /// Bitmap data for flood fill paths.
    /// </summary>
    [JsonIgnore]
    public SKBitmap? BitmapData { get; init; }

    public SKPath ToSKPath()
    {
        var skPath = new SKPath();

        if (Points.Count <= 0)
        {
            return skPath;
        }

        // First move to the first point
        skPath.MoveTo(Points[0].X, Points[0].Y);

        // Add the rest of the points
        for (var i = 1; i < Points.Count; i++)
        {
            skPath.LineTo(Points[i].X, Points[i].Y);
        }

        return skPath;
    }

    /// <summary>
    /// Gets the effective radius for rendering. Returns Radius if set, otherwise falls back to first point's radius for backward compatibility.
    /// </summary>
    public float GetEffectiveRadius()
    {
        if (Radius > 0)
            return Radius;

        // Backward compatibility: check first point
        if (Points.Count > 0 && Points[0].Radius > 0)
            return (float)Points[0].Radius;

        return 1f; // Default fallback
    }

    /// <summary>
    /// Compresses points to a base64-encoded gzip string. Public for use by JsonConverter.
    /// </summary>
    public static string? CompressPointsPublic(List<PenPoint> points)
    {
        if (points.Count == 0)
            return null;

        // Calculate buffer size: 4 bytes count + 12 bytes per point (3 floats: x, y, pressure)
        var bufferSize = 4 + (points.Count * 12);
        var buffer = ArrayPool<byte>.Shared.Rent(bufferSize);

        try
        {
            var offset = 0;

            // Write point count
            BitConverter.TryWriteBytes(buffer.AsSpan(offset), points.Count);
            offset += 4;

            // Write each point as 3 floats
            foreach (var point in points)
            {
                BitConverter.TryWriteBytes(buffer.AsSpan(offset), (float)point.X);
                offset += 4;
                BitConverter.TryWriteBytes(buffer.AsSpan(offset), (float)point.Y);
                offset += 4;
                BitConverter.TryWriteBytes(buffer.AsSpan(offset), (float)(point.Pressure ?? 1.0));
                offset += 4;
            }

            // Compress with gzip
            using var outputStream = new MemoryStream();
            using (var gzipStream = new GZipStream(outputStream, CompressionLevel.Optimal, leaveOpen: true))
            {
                gzipStream.Write(buffer, 0, offset);
            }

            return Convert.ToBase64String(outputStream.ToArray());
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>
    /// Decompresses points from a base64-encoded gzip string. Public for use by JsonConverter.
    /// </summary>
    public static List<PenPoint>? DecompressPointsPublic(string? compressed)
    {
        if (string.IsNullOrEmpty(compressed))
            return null;

        try
        {
            var compressedBytes = Convert.FromBase64String(compressed);

            using var inputStream = new MemoryStream(compressedBytes);
            using var gzipStream = new GZipStream(inputStream, CompressionMode.Decompress);
            using var outputStream = new MemoryStream();

            gzipStream.CopyTo(outputStream);
            var buffer = outputStream.ToArray();

            if (buffer.Length < 4)
                return null;

            var offset = 0;

            // Read point count
            var count = BitConverter.ToInt32(buffer, offset);
            offset += 4;

            // Validate we have enough data
            if (buffer.Length < 4 + (count * 12))
                return null;

            var points = new List<PenPoint>(count);

            for (var i = 0; i < count; i++)
            {
                var x = BitConverter.ToSingle(buffer, offset);
                offset += 4;
                var y = BitConverter.ToSingle(buffer, offset);
                offset += 4;
                var pressure = BitConverter.ToSingle(buffer, offset);
                offset += 4;

                points.Add(
                    new PenPoint(x, y)
                    {
                        Pressure = pressure >= 0 && pressure <= 1 ? pressure : null,
                        IsPen = true, // Mark as pen point so it renders correctly
                    }
                );
            }

            return points;
        }
        catch
        {
            // If decompression fails, return null (caller will handle as legacy format)
            return null;
        }
    }
}
