using Avalonia.Animation;
using Avalonia.Media.Imaging;

namespace Avalonia.Gif;

public interface IGifInstance : IDisposable
{
    IterationCount IterationCount { get; set; }
    bool AutoStart { get; }
    CancellationTokenSource CurrentCts { get; }
    int GifFrameCount { get; }
    PixelSize GifPixelSize { get; }
    bool IsDisposed { get; }
    WriteableBitmap? ProcessFrameTime(TimeSpan stopwatchElapsed);
}
