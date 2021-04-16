using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;

namespace SprinterPublishing
{
    static class Program
    {
        [STAThread]
        static void Main( string[] args )
        {
            APNG png = new APNG();
            png.Load(@"animated.png");
            for (int i = 0; i < png.NumEmbeddedPNG; i++)
            {
                Bitmap image = png.ToBitmap(i);
                image.Save("frame" + i + ".png", ImageFormat.Png);
            }

            for (int i = 0; i < png.NumEmbeddedPNG; i++)
            {
                png[i].Save("frame" + i + ".png", ImageFormat.Png);
            }
        }
    }
}