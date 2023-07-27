using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.Platform;
using Avalonia.Metadata;
using System.Diagnostics;
using LibAPNG.Chunks;
using Avalonia.Rendering.Composition;
using Avalonia.VisualTree;
using System.Numerics;
using Avalonia.Logging;

namespace Avalonia.Gif;

public class Image2 : Control, IDisposable
{
    public static readonly StyledProperty<object> SourceProperty = AvaloniaProperty.Register<Image2, object>(nameof(Source));

    public static readonly StyledProperty<object> FallbackSourceProperty = AvaloniaProperty.Register<Image2, object>(nameof(FallbackSource));

    //public static readonly StyledProperty<IterationCount> IterationCountProperty = AvaloniaProperty.Register<Image2, IterationCount>(nameof(IterationCount));

    public static readonly StyledProperty<bool> IsFailedProperty = AvaloniaProperty.Register<Image2, bool>(nameof(IsFailed), false);

    public static readonly StyledProperty<bool> AutoStartProperty = AvaloniaProperty.Register<Image2, bool>(nameof(AutoStart), true);

    public static readonly StyledProperty<int> DecodeWidthProperty = AvaloniaProperty.Register<Image2, int>(nameof(DecodeWidth));

    public static readonly StyledProperty<int> DecodeHeightProperty = AvaloniaProperty.Register<Image2, int>(nameof(DecodeHeight));

    public static readonly StyledProperty<bool> EnableCacheProperty = AvaloniaProperty.Register<Image2, bool>(nameof(EnableCache), true);

    public static readonly StyledProperty<StretchDirection> StretchDirectionProperty = AvaloniaProperty.Register<Image2, StretchDirection>(nameof(StretchDirection), StretchDirection.Both);

    public static readonly StyledProperty<Stretch> StretchProperty = AvaloniaProperty.Register<Image2, Stretch>(nameof(Stretch));

    private IImageInstance? gifInstance;
    private CompositionCustomVisual? _customVisual;
    private Bitmap? backingRTB;
    private ImageType imageType;
    private bool isSimplePNG;
    private bool disposedValue;

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

    public bool IsFailed
    {
        get => GetValue(IsFailedProperty);
        set => SetValue(IsFailedProperty, value);
    }

    public bool EnableCache
    {
        get => GetValue(EnableCacheProperty);
        set => SetValue(EnableCacheProperty, value);
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

    static void IterationCountChanged(AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Sender is not Image2)
            return;
    }


    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        switch (change.Property.Name)
        {
            case nameof(Source):
                SourceChanged(change);
                InvalidateArrange();
                InvalidateMeasure();
                Update();
                break;
            case nameof(Stretch):
            case nameof(StretchDirection):
                InvalidateArrange();
                InvalidateMeasure();
                Update();
                break;
            case nameof(IterationCount):
                IterationCountChanged(change);
                break;
            case nameof(Bounds):
                Update();
                break;
        }

        base.OnPropertyChanged(change);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (FallbackSource != null && backingRTB == null)
        {
            var value = ResolveStream.ResolveObjectToStream(FallbackSource, this);
            if (value != null)
            {
                backingRTB = DecodeImage(value);
                value.Dispose();
            }
        }
        if (imageType == ImageType.gif || !isSimplePNG)
        {
            var compositor = ElementComposition.GetElementVisual(this)?.Compositor;
            if (compositor == null || _customVisual?.Compositor == compositor)
                return;
            _customVisual = compositor.CreateCustomVisual(new CustomVisualHandler());
            ElementComposition.SetElementChildVisual(this, _customVisual);
            _customVisual.SendHandlerMessage(CustomVisualHandler.StartMessage);

            if (gifInstance is not null)
            {
                _customVisual?.SendHandlerMessage(gifInstance);
            }

            Update();
        }

        base.OnAttachedToVisualTree(e);
    }

    public override void Render(DrawingContext context)
    {
        if (backingRTB is not Bitmap bitmap) return;

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
            context.DrawImage(bitmap, sourceRect, destRect);
        }
    }

    /// <summary>
    /// Measures the control.
    /// </summary>
    /// <param name="availableSize">The available size.</param>
    /// <returns>The desired size of the control.</returns>
    protected override Size MeasureOverride(Size availableSize)
    {
        if (gifInstance != null)
        {
            var scaling = this.GetVisualRoot()?.RenderScaling ?? 1.0;
            return Stretch.CalculateSize(availableSize, gifInstance.GetSize(scaling),
                StretchDirection);
        }
        else if (backingRTB != null)
        {
            return Stretch.CalculateSize(availableSize, backingRTB.Size, StretchDirection);
        }

        return new Size();
    }

    /// <inheritdoc/>
    protected override Size ArrangeOverride(Size finalSize)
    {
        if (gifInstance != null)
        {
            var scaling = this.GetVisualRoot()?.RenderScaling ?? 1.0;
            var sourceSize = gifInstance.GetSize(scaling);
            return Stretch.CalculateSize(finalSize, sourceSize);
        }
        else if (backingRTB != null)
        {
            var sourceSize = backingRTB.Size;
            return Stretch.CalculateSize(finalSize, sourceSize);
        }
        return new Size();
    }

    private void StopAndDispose()
    {
        backingRTB?.Dispose();
        gifInstance?.Dispose();
        gifInstance?.Dispose();
        _customVisual = null;
    }

    static void SourceChanged(AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Sender is not Image2 image)
            return;

        image.gifInstance?.Dispose();
        image.gifInstance = null;
        image.backingRTB?.Dispose();
        image.backingRTB = null;

        if (e.NewValue == null && image.FallbackSource == null)
            return;

        Stream? value;
        if (e.NewValue is Bitmap bitmap)
        {
            image.IsFailed = false;
            image.backingRTB = bitmap;
            return;
        }
        else
        {
            if (image.FallbackSource != null)
            {
                value = ResolveStream.ResolveObjectToStream(image.FallbackSource, image);
                if (value != null)
                {
                    image.IsFailed = true;
                    image.backingRTB = image.DecodeImage(value);
                    value.Dispose();
                }
            }

            value = ResolveStream.ResolveObjectToStream(e.NewValue, image);
        }

        if (value == null)
            return;

        image.IsFailed = false;
        image.imageType = value.GetImageType();

        if (image.imageType == ImageType.gif)
        {
            var gifInstance = new GifInstance(value);
            gifInstance.IterationCount = IterationCount.Infinite;
            if (gifInstance.GifPixelSize.Width < 1 || gifInstance.GifPixelSize.Height < 1)
                return;
            image.gifInstance = gifInstance;
            image._customVisual?.SendHandlerMessage(image.gifInstance);
            return;
        }
        if (image.imageType == ImageType.png)
        {
            var apngInstance = new ApngInstance(value);
            if (apngInstance.IsSimplePNG)
            {
                image.isSimplePNG = apngInstance.IsSimplePNG;
                apngInstance.Dispose();
                apngInstance = null;
                image.backingRTB = image.DecodeImage(value);
            }
            else
            {
                apngInstance.IterationCount = IterationCount.Infinite;
                if (apngInstance.ApngPixelSize.Width < 1 || apngInstance.ApngPixelSize.Height < 1)
                    return;
                image.gifInstance = apngInstance;
                image._customVisual?.SendHandlerMessage(image.gifInstance);
            }
            return;
        }
        image.backingRTB = image.DecodeImage(value);
    }

    Bitmap? DecodeImage(Stream stream)
    {
        try
        {
            if (stream == null || stream.CanRead == false || stream.Length == 0)
                return null;
            stream.Position = 0;

            var interpolationMode = RenderOptions.GetBitmapInterpolationMode(this);
            if (DecodeWidth > 0)
            {
                return Bitmap.DecodeToWidth(stream, DecodeWidth, interpolationMode);
            }
            else if (DecodeHeight > 0)
            {
                return Bitmap.DecodeToHeight(stream, DecodeHeight, interpolationMode);
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

    private void Update()
    {
        if (_customVisual is null || gifInstance is null)
            return;

        var dpi = this.GetVisualRoot()?.RenderScaling ?? 1.0;
        var sourceSize = gifInstance.GetSize(dpi);
        var viewPort = new Rect(Bounds.Size);

        var scale = Stretch.CalculateScaling(Bounds.Size, sourceSize, StretchDirection);
        var scaledSize = sourceSize * scale;
        var destRect = viewPort
            .CenterRect(new Rect(scaledSize))
            .Intersect(viewPort);

        if (Stretch == Stretch.None)
        {
            _customVisual.Size = new Vector2((float)sourceSize.Width, (float)sourceSize.Height);
        }
        else
        {
            _customVisual.Size = new Vector2((float)destRect.Size.Width, (float)destRect.Size.Height);
        }

        _customVisual.Offset = new Vector3((float)destRect.Position.X, (float)destRect.Position.Y, 0);
    }

    private class CustomVisualHandler : CompositionCustomVisualHandler
    {
        private TimeSpan _animationElapsed;
        private TimeSpan? _lastServerTime;
        private IImageInstance? _currentInstance;
        private bool _running;

        public static readonly object StopMessage = new(), StartMessage = new();

        public override void OnMessage(object message)
        {
            if (message == StartMessage)
            {
                _running = true;
                _lastServerTime = null;
                RegisterForNextAnimationFrameUpdate();
            }
            else if (message == StopMessage)
            {
                _running = false;
            }
            else if (message is IImageInstance instance)
            {
                _currentInstance?.Dispose();
                _currentInstance = instance;
            }
        }

        public override void OnAnimationFrameUpdate()
        {
            if (!_running) return;
            Invalidate();
            RegisterForNextAnimationFrameUpdate();
        }

        public override void OnRender(ImmediateDrawingContext drawingContext)
        {
            if (_running)
            {
                if (_lastServerTime.HasValue) _animationElapsed += (CompositionNow - _lastServerTime.Value);
                _lastServerTime = CompositionNow;
            }

            try
            {
                if (_currentInstance is null || _currentInstance.IsDisposed) return;

                var bitmap = _currentInstance.ProcessFrameTime(_animationElapsed);
                if (bitmap is not null)
                {
                    if (_currentInstance is ApngInstance apngInstance)
                    {
                        var ts = new Rect(_currentInstance.GetSize(1));
                        var ns = new Rect(apngInstance._targetOffset, _currentInstance.GetSize(1));
                        drawingContext.DrawBitmap(bitmap, ts, ns);

                        //if (apngInstance._hasNewFrame)
                        //{
                        //    drawingContext.DrawBitmap2(bitmap, 1, ts, ns, bitmapBlending: BitmapBlendingMode.SourceOver);
                        //}
                        //else
                        //{
                        //    drawingContext.DrawBitmap2(bitmap, 1, ts, ns, bitmapBlending: BitmapBlendingMode.DestinationIn);
                        //}
                    }
                    else
                        drawingContext.DrawBitmap(bitmap, new Rect(_currentInstance.GetSize(1)), GetRenderBounds());
                }
            }
            catch (Exception e)
            {
                Logger.Sink?.Log(LogEventLevel.Error, "Image2 Renderer ", this, e.ToString());

            }
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                // TODO: 释放托管状态(托管对象)
                StopAndDispose();
            }

            // TODO: 释放未托管的资源(未托管的对象)并重写终结器
            // TODO: 将大型字段设置为 null
            disposedValue = true;
        }
    }

    void IDisposable.Dispose()
    {
        // 不要更改此代码。请将清理代码放入“Dispose(bool disposing)”方法中
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
