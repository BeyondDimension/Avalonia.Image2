using AvaloniaGif.Decoding;
using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Animation;
using System.Threading;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Rendering;
using Avalonia.Logging;
using JetBrains.Annotations;
using System.Text;
using System.Linq;

namespace AvaloniaGif
{
    public class GifInstance : IDisposable
    {
        public Stream Stream { get; private set; }
        public IterationCount IterationCount { get; private set; }
        public bool AutoStart { get; private set; } = true;
        public Progress<int> Progress { get; private set; }


        bool _streamCanDispose;
        private GifDecoder _gifDecoder;
        private GifBackgroundWorker _bgWorker;
        private WriteableBitmap _targetBitmap;
        private bool _hasNewFrame;
        private bool _isDisposed;



        public void SetSource(Stream stream)
        {
            Stream = stream;

            if (Stream == null)
            {
                //throw new InvalidDataException("Missing valid URI or Stream.");
                return;
            }

            _gifDecoder = new GifDecoder(Stream);
            _bgWorker = new GifBackgroundWorker(_gifDecoder);
            var pixSize = new PixelSize(_gifDecoder.Header.Dimensions.Width, _gifDecoder.Header.Dimensions.Height);

            _targetBitmap = new WriteableBitmap(pixSize, new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
            _bgWorker.CurrentFrameChanged += FrameChanged;
            GifPixelSize = pixSize;
            Run();
        }

        public PixelSize GifPixelSize { get; private set; }

        public WriteableBitmap GetBitmap()
        {
            WriteableBitmap ret = null;
            if (_hasNewFrame)
            {
                _hasNewFrame = false;
                ret = _targetBitmap;
            }
            return ret;
        }

        private void FrameChanged()
        {
            if (_targetBitmap is WriteableBitmap w)
            {
                if (_isDisposed) return;
                _hasNewFrame = true;

                using var lockedBitmap = w?.Lock();
                _gifDecoder?.WriteBackBufToFb(lockedBitmap.Address);
            }
        }

        public void Run()
        {
            if (!Stream.CanSeek)
                throw new ArgumentException("The stream is not seekable");

            _bgWorker?.SendCommand(BgWorkerCommand.Play);
        }

        public void Pause()
        {
            _bgWorker?.SendCommand(BgWorkerCommand.Pause);
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