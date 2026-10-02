using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace ComerziaPrinterTester.Utils
{
    public static class ThermalImageConverter
    {
        /// <summary>
        /// Automatically trims whitespace and transparent margins surrounding an image using fast bitmap lock.
        /// </summary>
        public static Bitmap AutoCropImage(Bitmap srcBmp, byte luminanceThreshold = 240)
        {
            try
            {
                // Ensure 32bpp format for reliable direct pixel scanning
                using var bmp = new Bitmap(srcBmp.Width, srcBmp.Height, PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.DrawImage(srcBmp, 0, 0, srcBmp.Width, srcBmp.Height);
                }

                int w = bmp.Width;
                int h = bmp.Height;

                BitmapData data = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                int minX = w, minY = h, maxX = -1, maxY = -1;

                try
                {
                    unsafe
                    {
                        byte* ptr = (byte*)data.Scan0;
                        int stride = data.Stride;

                        for (int y = 0; y < h; y++)
                        {
                            byte* row = ptr + (y * stride);
                            for (int x = 0; x < w; x++)
                            {
                                int idx = x * 4;
                                byte b = row[idx];
                                byte gVal = row[idx + 1];
                                byte r = row[idx + 2];
                                byte a = row[idx + 3];

                                bool isVisible = a > 25;
                                int lum = (int)(r * 0.299 + gVal * 0.587 + b * 0.114);
                                bool isDark = lum < luminanceThreshold;

                                if (isVisible && isDark)
                                {
                                    if (x < minX) minX = x;
                                    if (x > maxX) maxX = x;
                                    if (y < minY) minY = y;
                                    if (y > maxY) maxY = y;
                                }
                            }
                        }
                    }
                }
                finally
                {
                    bmp.UnlockBits(data);
                }

                if (maxX < minX || maxY < minY)
                {
                    return new Bitmap(srcBmp);
                }

                int cropW = (maxX - minX) + 1;
                int cropH = (maxY - minY) + 1;

                var cropped = new Bitmap(cropW, cropH, PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(cropped))
                {
                    g.CompositingMode = CompositingMode.SourceCopy;
                    g.DrawImage(bmp, new Rectangle(0, 0, cropW, cropH), new Rectangle(minX, minY, cropW, cropH), GraphicsUnit.Pixel);
                }

                return cropped;
            }
            catch
            {
                return new Bitmap(srcBmp);
            }
        }

        /// <summary>
        /// Resizes an image keeping aspect ratio within maxWidth and maxHeight.
        /// </summary>
        public static Bitmap ResizeImage(Image image, int maxWidth, int maxHeight)
        {
            if (maxWidth < 8) maxWidth = 8;
            if (maxHeight < 8) maxHeight = 8;

            Bitmap croppedBmp = (image is Bitmap b) ? AutoCropImage(b) : new Bitmap(image);

            try
            {
                double ratioX = (double)maxWidth / croppedBmp.Width;
                double ratioY = (double)maxHeight / croppedBmp.Height;
                double ratio = Math.Min(ratioX, ratioY);

                int newWidth = Math.Max(8, (int)Math.Round(croppedBmp.Width * ratio));
                newWidth = Math.Max(8, (newWidth / 8) * 8); // Align to multiple of 8 for TSPL
                int newHeight = Math.Max(8, (int)Math.Round(croppedBmp.Height * ratio));

                var destRect = new Rectangle(0, 0, newWidth, newHeight);
                var destImage = new Bitmap(newWidth, newHeight, PixelFormat.Format32bppArgb);

                using (var graphics = Graphics.FromImage(destImage))
                {
                    graphics.CompositingMode = CompositingMode.SourceCopy;
                    graphics.CompositingQuality = CompositingQuality.HighQuality;
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.SmoothingMode = SmoothingMode.HighQuality;
                    graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                    using var wrapMode = new ImageAttributes();
                    wrapMode.SetWrapMode(WrapMode.TileFlipXY);
                    graphics.DrawImage(croppedBmp, destRect, 0, 0, croppedBmp.Width, croppedBmp.Height, GraphicsUnit.Pixel, wrapMode);
                }

                return destImage;
            }
            finally
            {
                croppedBmp.Dispose();
            }
        }

        /// <summary>
        /// Converts a bitmap into monochrome TSPL BITMAP raw byte array (mode 0: OVERWRITE).
        /// TSPL BITMAP format:
        /// 1 byte represents 8 horizontal pixels. 
        /// In mode 0: 0 = Black (prints dot), 1 = White (no dot).
        /// </summary>
        public static byte[] ImageToTsplBitmapData(Bitmap srcBitmap, byte luminanceThreshold = 128)
        {
            int width = srcBitmap.Width;
            int height = srcBitmap.Height;
            int widthBytes = (width + 7) / 8;
            byte[] rawBytes = new byte[widthBytes * height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Color pixel = srcBitmap.GetPixel(x, y);
                    // Standard luminance calculation: 0.299 R + 0.587 G + 0.114 B
                    int luminance = (int)(pixel.R * 0.299 + pixel.G * 0.587 + pixel.B * 0.114);

                    // Consider alpha channel: transparent or white = 1 (blank/white), dark = 0 (black/print)
                    bool isBlack = (pixel.A > 50) && (luminance < luminanceThreshold);

                    int byteIndex = y * widthBytes + (x / 8);
                    int bitIndex = 7 - (x % 8);

                    if (isBlack)
                    {
                        // In TSPL standard mode 0: 0 is black, 1 is white.
                        // So if black, clear the bit (0). If white, keep 1.
                        // Default initialized array is 0, so let's handle setting/clearing explicitly.
                    }
                    else
                    {
                        rawBytes[byteIndex] |= (byte)(1 << bitIndex);
                    }
                }
            }

            return rawBytes;
        }
    }
}
