using System.Drawing;
using QRCoder;

namespace ComerziaPrinterTester.TSPL
{
    public interface IQrGenerator
    {
        /// <summary>
        /// Generates QR rendering commands (Native TSPL command or BITMAP commands).
        /// </summary>
        void AppendQr(TsplBuilder builder, int x, int y, string data, int sizeDots, int rotation = 0);
    }

    /// <summary>
    /// Implements QR Code using native TSPL standard command:
    /// QRCODE x,y,ecc_level,cell_width,mode,rotation,[model,mask,]"content"
    /// Example: QRCODE 100,100,M,4,A,0,"https://comerzia.com"
    /// </summary>
    public class NativeTsplQrGenerator : IQrGenerator
    {
        public void AppendQr(TsplBuilder builder, int x, int y, string data, int sizeDots, int rotation = 0)
        {
            // TSPL cell_width typically 1 to 10 dots (4 is common for ~203 DPI)
            int cellWidth = Math.Clamp(sizeDots / 30, 2, 8);
            builder.AppendRawCommand($"QRCODE {x},{y},M,{cellWidth},A,{rotation},\"{data}\"");
        }
    }

    /// <summary>
    /// Generates QR Code using QRCoder library into a bitmap and renders via TSPL BITMAP command.
    /// Useful for fallback if firmware lacks native QR parser.
    /// </summary>
    public class BitmapQrGenerator : IQrGenerator
    {
        public void AppendQr(TsplBuilder builder, int x, int y, string data, int sizeDots, int rotation = 0)
        {
            using var qrGenerator = new QRCodeGenerator();
            using var qrCodeData = qrGenerator.CreateQrCode(data, QRCodeGenerator.ECCLevel.M);
            using var qrCode = new QRCode(qrCodeData);
            
            // Pixels per module calculation
            int ppm = Math.Max(2, sizeDots / 33);
            using Bitmap qrBitmap = qrCode.GetGraphic(ppm, Color.Black, Color.White, drawQuietZones: false);
            
            if (rotation == 90)
            {
                using Bitmap rotated = (Bitmap)qrBitmap.Clone();
                rotated.RotateFlip(RotateFlipType.Rotate90FlipNone);
                builder.AppendBitmap(x - rotated.Width, y, rotated);
            }
            else
            {
                builder.AppendBitmap(x, y, qrBitmap);
            }
        }
    }
}
