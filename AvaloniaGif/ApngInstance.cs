using Avalonia.Animation;
using Avalonia.Media.Imaging;
using Avalonia;
using Avalonia.Platform;
using LibAPNG;
using LibAPNG.Chunks;

namespace AvaloniaGif;

public sealed class ApngInstance : IDisposable
{
    public Stream? Stream { get; private set; }
    public IterationCount IterationCount { get; private set; }
    public bool AutoStart { get; private set; } = true;

    public PixelSize ApngPixelSize { get; private set; }
    public bool IsSimplePNG { get; private set; }

    private readonly object _bitmapSync = new();
    private APNG _apng;
    private ApngBackgroundWorker _bgWorker;
    private WriteableBitmap _targetBitmap;
    public Point _targetOffset;
    public bool _hasNewFrame;
    private bool disposedValue;

    public void SetSource(Stream stream)
    {
        Stream = stream;

        if (Stream == null)
        {
            //throw new InvalidDataException("Missing valid URI or Stream.");
            return;
        }

        _apng = new APNG(Stream);
        IsSimplePNG = _apng.IsSimplePNG;

        if (IsSimplePNG)
        {
            _targetBitmap = WriteableBitmap.Decode(Stream);
        }
        else
        {
            var firstFrame = _apng.Frames.First();
            _bgWorker = new ApngBackgroundWorker(_apng);
            var pixSize = new PixelSize(firstFrame.IHDRChunk.Width, firstFrame.IHDRChunk.Height);

            _targetBitmap = new WriteableBitmap(pixSize, new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Unpremul);
            _bgWorker.CurrentFrameChanged += FrameChanged;
            ApngPixelSize = pixSize;
            Run();
        }
    }

    public WriteableBitmap GetBitmap()
    {
        if (_apng.IsSimplePNG)
        {
            return _targetBitmap;
        }

        WriteableBitmap? ret = null;
        lock (_bitmapSync)
        {
            ret = _targetBitmap;
        }
        return ret;
    }

    private void FrameChanged()
    {
        lock (_bitmapSync)
        {
            if (_targetBitmap is WriteableBitmap w)
            {
                if (disposedValue) return;
                _hasNewFrame = _bgWorker.CurentFrame.fcTLChunk.BlendOp == BlendOps.APNGBlendOpSource;
                _targetOffset = new(_bgWorker.CurentFrame.fcTLChunk.XOffset, _bgWorker.CurentFrame.fcTLChunk.YOffset);
                _targetBitmap = WriteableBitmap.Decode(_bgWorker.CurentFrame.GetStream());
            }
        }
    }

    public void Run()
    {
        if (!Stream.CanSeek)
            throw new ArgumentException("The stream is not seekable.");

        _bgWorker?.SendCommand(BgWorkerCommand.Play);
    }

    public void Pause()
    {
        _bgWorker?.SendCommand(BgWorkerCommand.Pause);
    }

    public void IterationCountChanged(AvaloniaPropertyChangedEventArgs e)
    {
        if (e.NewValue is IterationCount newVal)
            IterationCount = newVal;
    }

    public void AutoStartChanged(AvaloniaPropertyChangedEventArgs e)
    {
        if (e.NewValue is bool newVal)
            AutoStart = newVal;
    }

    private void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                // 释放托管状态(托管对象)
                _bgWorker?.SendCommand(BgWorkerCommand.Dispose);
                _targetBitmap?.Dispose();
            }

            // 释放未托管的资源(未托管的对象)并重写终结器
            // 将大型字段设置为 null
            disposedValue = true;
        }
    }

    // // 仅当“Dispose(bool disposing)”拥有用于释放未托管资源的代码时才替代终结器
    // ~ApngInstance()
    // {
    //     // 不要更改此代码。请将清理代码放入“Dispose(bool disposing)”方法中
    //     Dispose(disposing: false);
    // }

    public void Dispose()
    {
        // 不要更改此代码。请将清理代码放入“Dispose(bool disposing)”方法中
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
