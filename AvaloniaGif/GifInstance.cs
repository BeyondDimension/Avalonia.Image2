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
    public enum ImageFormat
    {
        bmp,
        jpeg,
        gif,
        tiff,
        png,
        unknown
    }

    public class GifInstance : IDisposable
    {
        public Stream Stream { get; private set; }
        public IterationCount IterationCount { get; private set; }
        public bool AutoStart { get; private set; } = true;
        public Progress<int> Progress { get; private set; }

        public ImageFormat ImageType { get; private set; }

        bool _streamCanDispose;
        private readonly object _bitmapSync = new();
        private GifDecoder _gifDecoder;
        private GifBackgroundWorker _bgWorker;
        private Bitmap _targetBitmap;
        private bool _hasNewFrame;
        private bool _isDisposed;

        static void TryReset(Stream s)
        {
            if (s.CanSeek)
            {
                if (s.Position > 0)
                {
                    s.Position = 0;
                }
            }
        }

        public void SetSource(object newValue)
        {
            var sourceUri = newValue as Uri;
            var sourceStr = newValue as Stream;

            Stream stream = null;

            if (sourceUri != null)
            {
                _streamCanDispose = true;
                this.Progress = new Progress<int>();

                if (sourceUri.OriginalString.Trim().StartsWith("resm"))
                {
                    var assetLocator = AvaloniaLocator.Current.GetService<IAssetLoader>();
                    stream = assetLocator.Open(sourceUri);
                }

            }
            else if (sourceStr != null)
            {
                stream = sourceStr;
            }

            Stream = stream;

            if (Stream == null)
            {
                //throw new InvalidDataException("Missing valid URI or Stream.");
                return;
            }

            TryReset(Stream);
            byte[] typedata = new byte[4];
            Stream.Read(typedata, 0, 4);
            TryReset(Stream);

            ImageType = GetImageFormat(typedata);

            if (ImageType == ImageFormat.gif)
            {
                _gifDecoder = new GifDecoder(Stream);
                _bgWorker = new GifBackgroundWorker(_gifDecoder);
                var pixSize = new PixelSize(_gifDecoder.Header.Dimensions.Width, _gifDecoder.Header.Dimensions.Height);

                _targetBitmap = new WriteableBitmap(pixSize, new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
                _bgWorker.CurrentFrameChanged += FrameChanged;
                GifPixelSize = pixSize;
                Run();
            }
            else
            {
                _targetBitmap = new Bitmap(Stream);
            }
        }

        public PixelSize GifPixelSize { get; private set; }

        public Bitmap GetBitmap()
        {
            if (_targetBitmap is WriteableBitmap w)
            {
                WriteableBitmap ret = null;

                lock (_bitmapSync)
                {
                    if (_hasNewFrame)
                    {
                        _hasNewFrame = false;
                        ret = w;
                    }
                }
                return ret;
            }
            return _targetBitmap;
        }

        private void FrameChanged()
        {
            lock (_bitmapSync)
            {
                if (_targetBitmap is WriteableBitmap w)
                {
                    if (_isDisposed) return;
                    _hasNewFrame = true;
                    using var lockedBitmap = w?.Lock();
                    _gifDecoder?.WriteBackBufToFb(lockedBitmap.Address);
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
            _bgWorker?.SendCommand(BgWorkerCommand.Pause);
            _targetBitmap?.Dispose();
        }

        public static ImageFormat GetImageFormat(byte[] bytes)
        {
            // see http://www.mikekunz.com/image_file_header.html  
            var bmp = Encoding.ASCII.GetBytes("BM");     // BMP
            var gif = Encoding.ASCII.GetBytes("GIF");    // GIF
            var png = new byte[] { 137, 80, 78, 71 };    // PNG
            var tiff = new byte[] { 73, 73, 42 };         // TIFF
            var tiff2 = new byte[] { 77, 77, 42 };         // TIFF
            var jpeg = new byte[] { 255, 216, 255, 224 }; // jpeg
            var jpeg2 = new byte[] { 255, 216, 255, 225 }; // jpeg canon

            if (bmp.SequenceEqual(bytes.Take(bmp.Length)))
                return ImageFormat.bmp;

            if (gif.SequenceEqual(bytes.Take(gif.Length)))
                return ImageFormat.gif;

            if (png.SequenceEqual(bytes.Take(png.Length)))
                return ImageFormat.png;

            if (tiff.SequenceEqual(bytes.Take(tiff.Length)))
                return ImageFormat.tiff;

            if (tiff2.SequenceEqual(bytes.Take(tiff2.Length)))
                return ImageFormat.tiff;

            if (jpeg.SequenceEqual(bytes.Take(jpeg.Length)))
                return ImageFormat.jpeg;

            if (jpeg2.SequenceEqual(bytes.Take(jpeg2.Length)))
                return ImageFormat.jpeg;

            return ImageFormat.unknown;
        }
    }
}