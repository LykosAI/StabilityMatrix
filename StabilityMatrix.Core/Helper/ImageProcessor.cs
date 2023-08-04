using SixLabors.ImageSharp;
using StabilityMatrix.Core.Extensions;

namespace StabilityMatrix.Core.Helper;

public static class ImageProcessor
{
    /// <summary>
    /// Get the dimensions of a grid that can hold the given amount of images.
    /// </summary>
    public static (int rows, int columns) GetGridDimensionsFromImageCount(int count)
    {
        if (count <= 1) return (1, 1);
        if (count == 2) return (1, 2);
        
        // Prefer one extra row over one extra column,
        // the row count will be the floor of the square root
        // and the column count will be floor of count / rows
        var rows = (int) Math.Floor(Math.Sqrt(count));
        var columns = (int) Math.Floor((double) count / rows);
        return (rows, columns);
    }
    
    public static Image<TPixel> CreateImageGrid<TPixel>(
        IReadOnlyList<Image<TPixel>> images, 
        int spacing = 0) where TPixel : unmanaged, IPixel<TPixel>
    {
        var (rows, columns) = GetGridDimensionsFromImageCount(images.Count);

        var singleWidth = images[0].Width;
        var singleHeight = images[0].Height;
        
        // Make output image
        var output = new Image<TPixel>(
            singleWidth * columns + spacing * (columns - 1), 
            singleHeight * rows + spacing * (rows - 1));
        
        
        // Draw images
        foreach (var (row, column) in 
                 Enumerable.Range(0, rows).Product(Enumerable.Range(0, columns)))
        {
            // Stop if we have drawn all images
            var index = row * columns + column;
            if (index >= images.Count) break;
                
            // Get image
            var image = images[index];
                
            // Draw image
            output.Mutate(op =>
            {
                op.DrawImage(image,
                    new Point(singleWidth * column + spacing * column,
                        singleHeight * row + spacing * row), 1);
            });
        }

        return output;
    }
}
