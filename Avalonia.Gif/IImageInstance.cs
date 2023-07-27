using Avalonia.Media.Imaging;

namespace Avalonia.Gif;

public interface IImageInstance : IDisposable
{
    bool IsDisposed { get; }

    double Height { get; }

    double Width { get; }

    Bitmap? ProcessFrameTime(TimeSpan stopwatchElapsed);

    Size GetSize(double scaling);
}
