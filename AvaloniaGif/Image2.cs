using System;
using System.IO;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.Platform;

namespace AvaloniaGif
{
    public class Image2 : Control
    {
        public static readonly StyledProperty<string> SourceUriRawProperty = AvaloniaProperty.Register<Image2, string>("SourceUriRaw");
        public static readonly StyledProperty<Uri> SourceUriProperty = AvaloniaProperty.Register<Image2, Uri>("SourceUri");
        public static readonly StyledProperty<Stream> SourceStreamProperty = AvaloniaProperty.Register<Image2, Stream>("SourceStream");
        public static readonly StyledProperty<IterationCount> IterationCountProperty = AvaloniaProperty.Register<Image2, IterationCount>("IterationCount");
        public static readonly StyledProperty<bool> AutoStartProperty = AvaloniaProperty.Register<Image2, bool>("AutoStart");
        public static readonly StyledProperty<StretchDirection> StretchDirectionProperty = AvaloniaProperty.Register<Image2, StretchDirection>("StretchDirection");
        public static readonly StyledProperty<Stretch> StretchProperty = AvaloniaProperty.Register<Image2, Stretch>("Stretch");
        private GifInstance gifInstance;
        private ApngInstance apngInstance;
        private Bitmap backingRTB;
        private ImageType imageType;
        static Image2()
        {
            SourceUriRawProperty.Changed.Subscribe(SourceChanged);
            SourceUriProperty.Changed.Subscribe(SourceChanged);
            SourceStreamProperty.Changed.Subscribe(SourceChanged);
            IterationCountProperty.Changed.Subscribe(IterationCountChanged);
            AutoStartProperty.Changed.Subscribe(AutoStartChanged);
            AffectsRender<Image2>(SourceStreamProperty, SourceUriProperty, SourceUriRawProperty);
            AffectsArrange<Image2>(SourceStreamProperty, SourceUriProperty, SourceUriRawProperty);
            AffectsMeasure<Image2>(SourceStreamProperty, SourceUriProperty, SourceUriRawProperty);
        }

        public string SourceUriRaw
        {
            get => GetValue(SourceUriRawProperty);
            set => SetValue(SourceUriRawProperty, value);
        }

        public Uri SourceUri
        {
            get => GetValue(SourceUriProperty);
            set => SetValue(SourceUriProperty, value);
        }

        public Stream SourceStream
        {
            get => GetValue(SourceStreamProperty);
            set => SetValue(SourceStreamProperty, value);
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
            }
            RenderBitmap(backingRTB);
            Dispatcher.UIThread.Post(InvalidateVisual, DispatcherPriority.Background);
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
            if (e.NewValue is string s)
            {
                var suri = new Uri(s);
                if (suri.OriginalString.Trim().StartsWith("resm"))
                {
                    var assetLocator = AvaloniaLocator.Current.GetService<IAssetLoader>();
                    value = assetLocator.Open(suri);
                }
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
                image.backingRTB = new RenderTargetBitmap(image.gifInstance.GifPixelSize, new Vector(96, 96));
            }
            else if (image.imageType == ImageType.png)
            {
                image.apngInstance = new ApngInstance();
                image.apngInstance.SetSource(value);
                image.backingRTB = new RenderTargetBitmap(image.apngInstance.ApngPixelSize, new Vector(96, 96));
            }
            else
            {
                image.backingRTB = new Bitmap(value);
            }
        }
    }
}
