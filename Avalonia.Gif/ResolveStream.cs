using Avalonia.Platform;
using Avalonia.Threading;

namespace Avalonia.Gif;

public static class ResolveStream
{
    public static Stream? ResolveObjectToStream(object? obj, Image2 img)
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
            else if (String2.IsHttpUrl(rawUri))
            {
                var isCache = img.EnableCache;
                Task2.InBackground(async () =>
                {
                    var imageHttpClientService = Ioc.Get_Nullable<IImageHttpClientService>();
                    if (imageHttpClientService == null)
                        return;

                    value = await imageHttpClientService.GetImageMemoryStreamAsync(rawUri, cache: isCache);
                    if (value == null)
                        return;
                    var isImage = System.IO.FileFormats.FileFormat.IsImage(value, out var _);
                    if (!isImage)
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
                if (AssetLoader.Exists(uri))
                    value = AssetLoader.Open(uri);
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
                if (AssetLoader.Exists(uri))
                    value = AssetLoader.Open(uri);
            }
        }
        else if (obj is Stream stream)
        {
            value = stream;
        }
        else if (obj is byte[] bytes)
        {
            value = new MemoryStream(bytes);
        }

        if (value == null || !value.CanRead || value.Length == 0)
            return null;

        value.Position = 0;

        return value;
    }
}
