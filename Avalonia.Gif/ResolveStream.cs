﻿using Avalonia.Platform;
using Avalonia.Threading;
using BD.Common.UI.ViewModels;
using Nito.AsyncEx;
using ReactiveUI;

namespace Avalonia.Gif;

public static class ResolveStream
{
    public static async ValueTask<Stream?> ResolveObjectToStream(object? obj, Image2 img, CancellationToken token = default)
    {
        Stream? value = null;
        if (obj is string rawUri)
        {
            if (rawUri == string.Empty) return null;

            if (File.Exists(rawUri))
            {
                value = new FileStream(rawUri, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            }
            else if (String2.IsHttpUrl(rawUri))
            {
                var isCache = img.EnableCache;

                // Android doesn't allow network requests on the main thread, even though we are using async apis.
                if (OperatingSystem.IsAndroid())
                {
                    await Task.Run(async () =>
                    {
                        var imageHttpClientService = Ioc.Get_Nullable<IImageHttpClientService>();
                        if (imageHttpClientService == null)
                            return;

                        value = await imageHttpClientService.GetImageMemoryStreamAsync(rawUri, cache: isCache, cacheFirst: isCache, cancellationToken: token);

                        if (value == null)
                            return;
                    }, CancellationToken.None);
                }
                else
                {
                    var imageHttpClientService = Ioc.Get_Nullable<IImageHttpClientService>();
                    if (imageHttpClientService == null)
                        return null;

                    value = await imageHttpClientService.GetImageMemoryStreamAsync(rawUri, cache: isCache, cacheFirst: isCache, cancellationToken: token);
                }

                if (value == null)
                    return null;

                var isImage = System.IO.FileFormats.FileFormat.IsImage(value, out var _);

                if (!isImage)
                    return null;
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    img.Source = value;
                }, DispatcherPriority.Render, CancellationToken.None);
            }
            else if (Uri.TryCreate(rawUri, UriKind.RelativeOrAbsolute, out var uri))
            {
                try
                {
                    if (AssetLoader.Exists(uri))
                        value = AssetLoader.Open(uri);
                }
                catch
                { }
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
        else if (obj is ImageSource.ClipStream stream2)
        {
            value = stream2.Stream;
        }
        //else if (obj is Task<ImageSource.ClipStream?> task)
        //{
        //    try
        //    {
        //        value = (await task.WaitAsync(token))?.Stream;
        //    }
        //    catch
        //    { 
        //        return null; 
        //    }
        //}

        if (value == null || !value.CanRead || value.Length == 0)
            return null;

        value.Position = 0;

        return value;
    }
}
