using System.Collections.Generic;

namespace StabilityMatrix.Avalonia.Models.Inference;

public interface IInputImageProvider
{
    IEnumerable<ImageSource> GetInputImages();
}
