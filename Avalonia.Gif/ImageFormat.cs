namespace Avalonia.Gif;

internal enum ImageType
{
    bmp,
    jpeg,
    gif,
    tiff,
    png,
    unknown
}

internal static class ImageFormat
{
    public static ImageType GetImageType(this ReadOnlySpan<byte> bytes)
    {
        // see http://www.mikekunz.com/image_file_header.html  
        ReadOnlySpan<byte> bmp = "BM"u8;     // BMP
        ReadOnlySpan<byte> gif = "GIF"u8;    // GIF
        ReadOnlySpan<byte> png = new byte[] { 137, 80, 78, 71 };    // PNG
        ReadOnlySpan<byte> tiff = "II*"u8;         // TIFF
        ReadOnlySpan<byte> tiff2 = "MM*"u8;         // TIFF
        ReadOnlySpan<byte> jpeg = new byte[] { 255, 216, 255, 224 }; // jpeg
        ReadOnlySpan<byte> jpeg2 = new byte[] { 255, 216, 255, 225 }; // jpeg canon

        if (bmp.SequenceEqual(bytes[..bmp.Length]))
            return ImageType.bmp;

        if (gif.SequenceEqual(bytes[..gif.Length]))
            return ImageType.gif;

        if (png.SequenceEqual(bytes[..png.Length]))
            return ImageType.png;

        if (tiff.SequenceEqual(bytes[..tiff.Length]))
            return ImageType.tiff;

        if (tiff2.SequenceEqual(bytes[..tiff2.Length]))
            return ImageType.tiff;

        if (jpeg.SequenceEqual(bytes[..jpeg.Length]))
            return ImageType.jpeg;

        if (jpeg2.SequenceEqual(bytes[..jpeg2.Length]))
            return ImageType.jpeg;

        return ImageType.unknown;
    }

    public static ImageType GetImageType(this Stream stream)
    {
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

        TryReset(stream);
        Span<byte> typedata = new byte[4];
        stream.Read(typedata);
        TryReset(stream);

        ReadOnlySpan<byte> typedata_ = typedata;

        return typedata_.GetImageType();
    }
}
