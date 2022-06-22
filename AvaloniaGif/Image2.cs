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
using System.Threading.Tasks;
using System.Net.Http;
using System.Application.Services;
using Avalonia.Metadata;
using Avalonia.Utilities;
using System.Application;

#nullable enable

namespace AvaloniaGif
{
    public class Image2 : Control
    {
        public static readonly StyledProperty<object> SourceProperty = AvaloniaProperty.Register<Image2, object>(nameof(Source));

        public static readonly StyledProperty<object> FallbackSourceProperty = AvaloniaProperty.Register<Image2, object>(nameof(FallbackSource));

        //public static readonly StyledProperty<IterationCount> IterationCountProperty = AvaloniaProperty.Register<Image2, IterationCount>(nameof(IterationCount));

        public static readonly StyledProperty<bool> AutoStartProperty = AvaloniaProperty.Register<Image2, bool>(nameof(AutoStart));

        public static readonly StyledProperty<int> DecodeWidthProperty = AvaloniaProperty.Register<Image2, int>(nameof(DecodeWidth));
        public static readonly StyledProperty<int> DecodeHeightProperty = AvaloniaProperty.Register<Image2, int>(nameof(DecodeHeight));

        public static readonly StyledProperty<StretchDirection> StretchDirectionProperty = AvaloniaProperty.Register<Image2, StretchDirection>(nameof(StretchDirection), StretchDirection.Both);

        public static readonly StyledProperty<Stretch> StretchProperty = AvaloniaProperty.Register<Image2, Stretch>(nameof(Stretch));

        public static readonly StyledProperty<BitmapInterpolationMode> QualityProperty = AvaloniaProperty.Register<Image2, BitmapInterpolationMode>(nameof(Quality), BitmapInterpolationMode.HighQuality);

        private GifInstance? gifInstance;
        private ApngInstance? apngInstance;
        private IBitmap? backingRTB;
        private ImageType imageType;
        static Image2()
        {
            SourceProperty.Changed.Subscribe(SourceChanged);
            //IterationCountProperty.Changed.Subscribe(IterationCountChanged);
            AutoStartProperty.Changed.Subscribe(AutoStartChanged);
            DecodeWidthProperty.Changed.Subscribe(DecodeWidthChanged);
            DecodeHeightProperty.Changed.Subscribe(DecodeHeightChanged);
            AffectsRender<Image2>(SourceProperty, StretchProperty, StretchDirectionProperty);
            AffectsArrange<Image2>(SourceProperty, StretchProperty, StretchDirectionProperty);
            AffectsMeasure<Image2>(SourceProperty, StretchProperty, StretchDirectionProperty);
        }

        [Content]
        public object Source
        {
            get => GetValue(SourceProperty);
            set => SetValue(SourceProperty, value);
        }

        public object FallbackSource
        {
            get => GetValue(FallbackSourceProperty);
            set => SetValue(FallbackSourceProperty, value);
        }

        //public IterationCount IterationCount
        //{
        //    get => GetValue(IterationCountProperty);
        //    set => SetValue(IterationCountProperty, value);
        //}

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
            if (e.Sender is not Image2)
                return;
        }

        private static void DecodeHeightChanged(AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Sender is not Image2)
                return;
        }

        private static void AutoStartChanged(AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Sender is not Image2)
                return;
        }

        private static void IterationCountChanged(AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Sender is not Image2)
                return;
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            if (FallbackSource != null && backingRTB == null)
            {
                var value = ResolveObjectToStream(FallbackSource, this);
                if (value != null)
                {
                    backingRTB = DecodeImage(value);
                    value.Dispose();
                }
            }

            if (apngInstance != null)
            {
                apngInstance.Run();
            }
            else if (gifInstance != null)
            {
                gifInstance.Run();
            }
            base.OnAttachedToVisualTree(e);
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            if (apngInstance != null)
            {
                apngInstance.Pause();
            }
            else if (gifInstance != null)
            {
                gifInstance.Pause();
            }
            base.OnDetachedFromVisualTree(e);
        }

        public override void Render(DrawingContext context)
        {
            void RenderBitmap(IImage? bitmap)
            {
                if (bitmap is not null && IsVisible && Bounds.Width > 0 && Bounds.Height > 0)
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

                    //var interpolationMode = RenderOptions.GetBitmapInterpolationMode(this);
                    context.DrawImage(bitmap, sourceRect, destRect, Quality);
                }
            }

            if (backingRTB is RenderTargetBitmap b)
            {
                if (imageType == ImageType.gif)
                {
                    if (gifInstance?.ProcessFrameTime(gifInstance.Stopwatch.Elapsed) is WriteableBitmap source && b is not null)
                    {
                        using var ctx = b.CreateDrawingContext(null);
                        var ts = new Rect(source.Size);
                        ctx.DrawBitmap(source.PlatformImpl, 1, ts, ts, Quality);
                    }
                    RenderBitmap(backingRTB);
                    Dispatcher.UIThread.Post(InvalidateVisual, DispatcherPriority.Render);
                    return;
                }
                else if (imageType == ImageType.png && apngInstance != null && !apngInstance.IsSimplePNG)
                {
                    if (apngInstance.GetBitmap() is WriteableBitmap source && b is not null)
                    {
                        using var ctx = b.CreateDrawingContext(null);
                        var ts = new Rect(b.Size);
                        var ns = new Rect(apngInstance._targetOffset, source.Size);
                        //ctx.DrawRectangle(Brushes.Black, null, new Rect(0, 0, apngInstance.ApngPixelSize.Width, apngInstance.ApngPixelSize.Height));

                        if (apngInstance._hasNewFrame)
                        {
                            ctx.PushBitmapBlendMode(BitmapBlendingMode.Source);
                            //ctx.Clear(Colors.Transparent);
                            ctx.DrawBitmap(source.PlatformImpl, 1, ts, ns, Quality);
                        }
                        else
                        {
                            ctx.PushBitmapBlendMode(BitmapBlendingMode.SourceOver);
                            ctx.DrawBitmap(source.PlatformImpl, 1, ts, ns, Quality);
                        }
                    }
                    RenderBitmap(backingRTB);
                    Dispatcher.UIThread.Post(InvalidateVisual, DispatcherPriority.Render);
                    return;
                }
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
            if (e.Sender is not Image2 image)
                return;

            image.gifInstance?.Dispose();
            image.gifInstance = null;
            image.apngInstance?.Dispose();
            image.apngInstance = null;
            image.backingRTB?.Dispose();
            image.backingRTB = null;

            if (e.NewValue == null && image.FallbackSource == null)
                return;

            Stream? value;
            if (e.NewValue is Bitmap bitmap)
            {
                image.backingRTB = bitmap;
                return;
            }
            else
            {
                if (image.FallbackSource != null)
                {
                    value = ResolveObjectToStream(image.FallbackSource, image);
                    if (value != null)
                    {
                        image.backingRTB = image.DecodeImage(value);
                        value.Dispose();
                    }
                }

                value = ResolveObjectToStream(e.NewValue, image);
            }

            if (value == null)
                return;

            image.imageType = value.GetImageType();

            if (image.imageType == ImageType.gif)
            {
                image.gifInstance = new GifInstance(value);
                image.gifInstance.IterationCount = IterationCount.Infinite;
                if (image.AutoStart)
                    image.gifInstance.Run();
                if (image.gifInstance.GifPixelSize.Width < 1 || image.gifInstance.GifPixelSize.Height < 1)
                    return;
                image.backingRTB = new RenderTargetBitmap(image.gifInstance.GifPixelSize, new Vector(96, 96));
                return;
            }
            if (image.imageType == ImageType.png)
            {
                image.apngInstance = new ApngInstance();
                image.apngInstance.SetSource(value);
                if (image.apngInstance.IsSimplePNG)
                {
                    image.apngInstance.Dispose();
                    image.apngInstance = null;
                    image.backingRTB = image.DecodeImage(value);
                }
                else
                {
                    if (image.apngInstance.ApngPixelSize.Width < 1 || image.apngInstance.ApngPixelSize.Height < 1)
                        return;
                    image.backingRTB = new RenderTargetBitmap(image.apngInstance.ApngPixelSize, new Vector(96, 96));
                }
                return;
            }
            image.backingRTB = image.DecodeImage(value);
        }

        private static Stream? ResolveObjectToStream(object? obj, Image2 img)
        {
            Stream? value = null;
            if (obj is string rawUri)
            {
                if (rawUri == string.Empty) return null;

                Uri uri;
                if (File.Exists(rawUri))
                {
                    value = new FileStream(rawUri, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                }
                else if (Browser2.IsHttpUrl(rawUri))
                {
                    Task.Run(async () =>
                    {
                        value = await IHttpService.Instance.GetImageStreamAsync(rawUri);
                        if (value == null)
                            return;
                        Dispatcher.UIThread.Post(() =>
                        {
                            img.Source = value;
                        }, DispatcherPriority.Render);
                    });
                    return null;
                }
                else
                {
                    uri = new Uri(rawUri);
                    var assets = AvaloniaLocator.Current.GetService<IAssetLoader>();
                    if (assets.Exists(uri))
                    {
                        value = assets.Open(uri);
                    }
                }

                //if (suri.OriginalString.Trim().StartsWith("resm"))
                //{
                //    var assetLocator = AvaloniaLocator.Current.GetService<IAssetLoader>();
                //    value = assetLocator.Open(suri);
                //}
            }
            else if (obj is Uri uri)
            {
                if (uri.OriginalString.Trim().StartsWith("resm"))
                {
                    var assetLocator = AvaloniaLocator.Current.GetService<IAssetLoader>();
                    if (assetLocator.Exists(uri))
                    {
                        value = assetLocator.Open(uri);
                    }
                }
            }
            else if (obj is Stream stream)
            {
                value = stream;
            }

            if (value == null)
                return null;

            return value;
        }

        private Bitmap? DecodeImage(Stream stream)
        {
            try
            {
                if (stream == null || stream.CanRead == false || stream.Length == 0)
                    return null;

                if (DecodeWidth > 0)
                {
                    stream.Position = 0;
                    return Bitmap.DecodeToWidth(stream, DecodeWidth, Quality);
                }
                else if (DecodeHeight > 0)
                {
                    stream.Position = 0;
                    return Bitmap.DecodeToHeight(stream, DecodeHeight, Quality);
                }

                //https://github.com/mono/SkiaSharp/issues/1551
                return new Bitmap(stream);
            }
            catch (Exception ex)
            {
                Log.Error(nameof(Image2), ex, nameof(DecodeImage));
                // 为了让程序不闪退无视错误
                return null;
            }
        }
    }
}
