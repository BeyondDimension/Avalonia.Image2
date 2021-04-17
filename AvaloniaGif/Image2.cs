using System;
using System.IO;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.Platform;
using System.Net;
using Avalonia.Visuals.Media.Imaging;

namespace AvaloniaGif
{
    public class Image2 : Control
    {
        public static readonly StyledProperty<object> SourceProperty = AvaloniaProperty.Register<Image2, object>("Source");
        public static readonly StyledProperty<IterationCount> IterationCountProperty = AvaloniaProperty.Register<Image2, IterationCount>("IterationCount");
        public static readonly StyledProperty<bool> AutoStartProperty = AvaloniaProperty.Register<Image2, bool>("AutoStart");

        public static readonly StyledProperty<int> DecodeWidthProperty = AvaloniaProperty.Register<Image2, int>("DecodeWidth");
        public static readonly StyledProperty<int> DecodeHeightProperty = AvaloniaProperty.Register<Image2, int>("DecodeHeight");

        public static readonly StyledProperty<StretchDirection> StretchDirectionProperty = AvaloniaProperty.Register<Image2, StretchDirection>("StretchDirection");
        public static readonly StyledProperty<Stretch> StretchProperty = AvaloniaProperty.Register<Image2, Stretch>("Stretch");
        public static readonly StyledProperty<BitmapInterpolationMode> QualityProperty = AvaloniaProperty.Register<Image2, BitmapInterpolationMode>("Quality");

        private GifInstance gifInstance;
        private ApngInstance apngInstance;
        private Bitmap backingRTB;
        private ImageType imageType;
        static Image2()
        {
            SourceProperty.Changed.Subscribe(SourceChanged);
            IterationCountProperty.Changed.Subscribe(IterationCountChanged);
            AutoStartProperty.Changed.Subscribe(AutoStartChanged);
            DecodeWidthProperty.Changed.Subscribe(DecodeWidthChanged);
            DecodeHeightProperty.Changed.Subscribe(DecodeHeightChanged);
            AffectsRender<Image2>(SourceProperty);
            AffectsArrange<Image2>(SourceProperty);
            AffectsMeasure<Image2>(SourceProperty);
        }

        public object Source
        {
            get => GetValue(SourceProperty);
            set => SetValue(SourceProperty, value);
        }

        public IterationCount IterationCount
        {
            get => GetValue(IterationCountProperty);
            set => SetValue(IterationCountProperty, value);
        }

        public bool AutoStart
        {
            get => GetValue(AutoStartProperty);
            set => SetValue(AutoStartProperty, value);
        }

        public int DecodeHeight
        {
            get => GetValue(DecodeHeightProperty);
            set => SetValue(DecodeHeightProperty, value);
        }

        public int DecodeWidth
        {
            get => GetValue(DecodeWidthProperty);
            set => SetValue(DecodeWidthProperty, value);
        }

        public StretchDirection StretchDirection
        {
            get => GetValue(StretchDirectionProperty);
            set => SetValue(StretchDirectionProperty, value);
        }

        public Stretch Stretch
        {
            get => GetValue(StretchProperty);
            set => SetValue(StretchProperty, value);
        }

        public BitmapInterpolationMode Quality
        {
            get => GetValue(QualityProperty);
            set => SetValue(QualityProperty, value);
        }

        private static void DecodeWidthChanged(AvaloniaPropertyChangedEventArgs e)
        {
            var image = e.Sender as Image2;
            if (image == null)
                return;
        }

        private static void DecodeHeightChanged(AvaloniaPropertyChangedEventArgs e)
        {
            var image = e.Sender as Image2;
            if (image == null)
                return;
        }

        private static void AutoStartChanged(AvaloniaPropertyChangedEventArgs e)
        {
            var image = e.Sender as Image2;
            if (image == null)
                return;
        }

        private static void IterationCountChanged(AvaloniaPropertyChangedEventArgs e)
        {
            var image = e.Sender as Image2;
            if (image == null)
                return;
        }

        public override void Render(DrawingContext context)
        {
            void RenderBitmap(Bitmap bitmap)
            {
                if (bitmap is not null && Bounds.Width > 0 && Bounds.Height > 0)
                {
                    var viewPort = new Rect(Bounds.Size);
                    var sourceSize = bitmap.Size;

                    var scale = Stretch.CalculateScaling(Bounds.Size, sourceSize, StretchDirection);
                    var scaledSize = sourceSize * scale;
                    var destRect = viewPort
                        .CenterRect(new Rect(scaledSize))
                        .Intersect(viewPort);

                    var sourceRect = new Rect(sourceSize)
                        .CenterRect(new Rect(destRect.Size / scale));

                    var interpolationMode = RenderOptions.GetBitmapInterpolationMode(this);
                    context.DrawImage(bitmap, sourceRect, destRect, interpolationMode);
                }
            }

            if (backingRTB is RenderTargetBitmap b)
            {
                if (imageType == ImageType.gif)
                {
                    if (gifInstance?.GetBitmap() is WriteableBitmap source && b is not null)
                    {
                        using var ctx = b.CreateDrawingContext(null);
                        var ts = new Rect(source.Size);
                        ctx.DrawBitmap(source.PlatformImpl, 1, ts, ts);
                        RenderBitmap(b);
                    }
                }
                else if (imageType == ImageType.png)
                {
                    if (apngInstance?.GetBitmap() is Bitmap source && b is not null)
                    {
                        using var ctx = b.CreateDrawingContext(null);
                        var ts = new Rect(source.Size);
                        ctx.DrawBitmap(source.PlatformImpl, 1, ts, ts);
                        RenderBitmap(b);
                    }
                }
                RenderBitmap(backingRTB);
                Dispatcher.UIThread.Post(InvalidateVisual, DispatcherPriority.Background);
                return;
            }
            RenderBitmap(backingRTB);
        }

        /// <summary>
        /// Measures the control.
        /// </summary>
        /// <param name="availableSize">The available size.</param>
        /// <returns>The desired size of the control.</returns>
        protected override Size MeasureOverride(Size availableSize)
        {
            var source = backingRTB;
            var result = new Size();

            if (source != null)
            {
                result = Stretch.CalculateSize(availableSize, source.Size, StretchDirection);
            }

            return result;
        }

        /// <inheritdoc/>
        protected override Size ArrangeOverride(Size finalSize)
        {
            var source = backingRTB;

            if (source != null)
            {
                var sourceSize = source.Size;
                var result = Stretch.CalculateSize(finalSize, sourceSize);
                return result;
            }
            else
            {
                return new Size();
            }
        }

        private static void SourceChanged(AvaloniaPropertyChangedEventArgs e)
        {
            var image = e.Sender as Image2;
            if (image == null)
                return;
            if (e.NewValue == null)
                return;

            image.gifInstance?.Dispose();
            image.apngInstance?.Dispose();
            image.backingRTB?.Dispose();
            image.backingRTB = null;

            Stream value = null;
            if (e.NewValue is string rawUri)
            {
                if (rawUri == string.Empty) return;

                Uri uri;
                if (File.Exists(rawUri))
                {
                    value = File.OpenRead(rawUri);
                }
                //在列表中使用此方法性能极差
                else if (rawUri.StartsWith("http://") || rawUri.StartsWith("https://"))
                {
                    using var web = new WebClient();
                    var bt = web.DownloadData(rawUri);
                    using var stream = new MemoryStream(bt);
                    value = stream;
                }
                else
                {
                    uri = new Uri(rawUri);
                    var assets = AvaloniaLocator.Current.GetService<IAssetLoader>();
                    value = assets.Open(uri);
                }

                //if (suri.OriginalString.Trim().StartsWith("resm"))
                //{
                //    var assetLocator = AvaloniaLocator.Current.GetService<IAssetLoader>();
                //    value = assetLocator.Open(suri);
                //}
            }
            else if (e.NewValue is Uri uri)
            {
                if (uri.OriginalString.Trim().StartsWith("resm"))
                {
                    var assetLocator = AvaloniaLocator.Current.GetService<IAssetLoader>();
                    value = assetLocator.Open(uri);
                }
            }
            else if (e.NewValue is Stream stream)
            {
                value = stream;
            }

            if (value == null)
                return;

            image.imageType = value.GetImageType();

            if (image.imageType == ImageType.gif)
            {
                image.gifInstance = new GifInstance();
                image.gifInstance.SetSource(value);
                if (image.gifInstance.GifPixelSize.Width < 1 || image.gifInstance.GifPixelSize.Height < 1)
                    return;
                image.backingRTB = new RenderTargetBitmap(image.gifInstance.GifPixelSize, new Vector(96, 96));
            }
            else if (image.imageType == ImageType.png)
            {
                image.apngInstance = new ApngInstance();
                image.apngInstance.SetSource(value);
                if (image.apngInstance.IsSimplePNG)
                {
                    image.apngInstance.Dispose();
                    image.apngInstance = null;
                    image.backingRTB = DecodeImage(value);
                }
                else
                {
                    if (image.apngInstance.ApngPixelSize.Width < 1 || image.apngInstance.ApngPixelSize.Height < 1)
                        return;
                    image.backingRTB = new RenderTargetBitmap(image.apngInstance.ApngPixelSize, new Vector(96, 96));
                }
            }
            else
            {
                image.backingRTB = DecodeImage(value);
            }

            Bitmap DecodeImage(Stream stream)
            {
                if (image?.DecodeWidth > 0)
                {
                    stream.Position = 0;
                    return Bitmap.DecodeToWidth(stream, image.DecodeWidth, image.Quality);
                }
                else if (image?.DecodeHeight > 0)
                {
                    stream.Position = 0;
                    return Bitmap.DecodeToHeight(stream, image.DecodeHeight, image.Quality);
                }
                return new Bitmap(stream);
            }
        }
    }
}
