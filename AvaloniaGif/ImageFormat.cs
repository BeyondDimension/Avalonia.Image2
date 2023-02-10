using System.Text;

namespace AvaloniaGif;

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
    public static ImageType GetImageType(this byte[] bytes)
    {
        // see http://www.mikekunz.com/image_file_header.html  
        var bmp = Encoding.ASCII.GetBytes("BM");     // BMP
        var gif = Encoding.ASCII.GetBytes("GIF");    // GIF
        var png = new byte[] { 137, 80, 78, 71 };    // PNG
        var tiff = "II*"u8.ToArray();         // TIFF
        var tiff2 = "MM*"u8.ToArray();         // TIFF
        var jpeg = new byte[] { 255, 216, 255, 224 }; // jpeg
        var jpeg2 = new byte[] { 255, 216, 255, 225 }; // jpeg canon

        if (bmp.SequenceEqual(bytes.Take(bmp.Length)))
            return ImageType.bmp;

        if (gif.SequenceEqual(bytes.Take(gif.Length)))
            return ImageType.gif;

        if (png.SequenceEqual(bytes.Take(png.Length)))
            return ImageType.png;

        if (tiff.SequenceEqual(bytes.Take(tiff.Length)))
            return ImageType.tiff;

        if (tiff2.SequenceEqual(bytes.Take(tiff2.Length)))
            return ImageType.tiff;

        if (jpeg.SequenceEqual(bytes.Take(jpeg.Length)))
            return ImageType.jpeg;

        if (jpeg2.SequenceEqual(bytes.Take(jpeg2.Length)))
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
        byte[] typedata = new byte[4];
        stream.Read(typedata, 0, 4);
        TryReset(stream);

        return typedata.GetImageType();
    }
}
