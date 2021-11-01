using Avalonia.Animation;
using System;
using LibAPNG;
using System.IO;
using Avalonia.Media.Imaging;
using Avalonia;
using System.Linq;
using Avalonia.Platform;

namespace AvaloniaGif
{
    public class ApngInstance : IDisposable
    {
        public Stream Stream { get; private set; }
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
        private bool _isDisposed;

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
                _targetBitmap = WriteableBitmap.Decode(_apng.DefaultImage.GetStream());
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

            WriteableBitmap ret = null;
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
                    if (_isDisposed) return;
                    _hasNewFrame = _bgWorker.CurentFrame.fcTLChunk.BlendOp == BlendOps.APNGBlendOpSource;
                    _targetOffset = new(_bgWorker.CurentFrame.fcTLChunk.XOffset, _bgWorker.CurentFrame.fcTLChunk.YOffset);
                    _targetBitmap = WriteableBitmap.Decode(_bgWorker.CurentFrame.GetStream());
                }
            }
        }

        private void Run()
        {
            if (!Stream.CanSeek)
                throw new ArgumentException("The stream is not seekable");

            _bgWorker?.SendCommand(BgWorkerCommand.Play);
        }

        public void IterationCountChanged(AvaloniaPropertyChangedEventArgs e)
        {
            var newVal = (IterationCount)e.NewValue;
            IterationCount = newVal;
        }

        public void AutoStartChanged(AvaloniaPropertyChangedEventArgs e)
        {
            var newVal = (bool)e.NewValue;
            this.AutoStart = newVal;
        }


        public void Dispose()
        {
            _isDisposed = true;
            _bgWorker?.SendCommand(BgWorkerCommand.Dispose);
            _targetBitmap?.Dispose();
        }
    }
}
