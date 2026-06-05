using SkiaSharp;
using StabilityMatrix.Core.Extensions;

namespace StabilityMatrix.Avalonia.Helpers;

public static class ImageProcessor
{
    /// <summary>
    /// Get the dimensions of a grid that can hold the given amount of images.
    /// </summary>
    public static (int rows, int columns) GetGridDimensionsFromImageCount(int count)
    {
        if (count <= 1)
            return (1, 1);
        if (count == 2)
            return (1, 2);

        // Prefer one extra row over one extra column,
        // the row count will be the floor of the square root
        // and the column count will be floor of count / rows
        var rows = (int)Math.Floor(Math.Sqrt(count));
        var columns = (int)Math.Floor((double)count / rows);
        return (rows, columns);
    }

    public static SKImage CreateImageGrid(IReadOnlyList<SKImage> images, int spacing = 0)
    {
        if (images.Count == 0)
            throw new ArgumentException("Must have at least one image");

        var (rows, columns) = GetGridDimensionsFromImageCount(images.Count);

        var singleWidth = images[0].Width;
        var singleHeight = images[0].Height;

        // Make output image
        using var output = new SKBitmap(
            singleWidth * columns + spacing * (columns - 1),
            singleHeight * rows + spacing * (rows - 1)
        );

        // Draw images
        using var canvas = new SKCanvas(output);

        foreach (var (row, column) in Enumerable.Range(0, rows).Product(Enumerable.Range(0, columns)))
        {
            // Stop if we have drawn all images
            var index = row * columns + column;
            if (index >= images.Count)
                break;

            // Get image
            var image = images[index];

            // Draw image
            var destination = new SKRect(
                singleWidth * column + spacing * column,
                singleHeight * row + spacing * row,
                singleWidth * column + spacing * column + image.Width,
                singleHeight * row + spacing * row + image.Height
            );
            canvas.DrawImage(image, destination);
        }

        return SKImage.FromBitmap(output);
    }
}
