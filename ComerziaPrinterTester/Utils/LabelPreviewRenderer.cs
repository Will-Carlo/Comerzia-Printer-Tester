using System.Drawing;
using System.Drawing.Drawing2D;
using ComerziaPrinterTester.Models;
using ComerziaPrinterTester.Utils;
using QRCoder;

namespace ComerziaPrinterTester.Utils
{
    public static class LabelPreviewRenderer
    {
        public static Bitmap RenderPreview(LabelConfiguration config, PrintTestData data, int canvasWidth = 340, int canvasHeight = 220)
        {
            // Create a preview canvas with nice border/margins
            var bmp = new Bitmap(canvasWidth, canvasHeight);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.FromArgb(245, 247, 250));
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                // Determine scale so the label fits into preview with padding
                double scaleX = (canvasWidth - 30.0) / config.WidthDots;
                double scaleY = (canvasHeight - 30.0) / config.HeightDots;
                double scale = Math.Min(scaleX, scaleY);
                if (scale <= 0) scale = 1.0;

                int renderWidth = (int)(config.WidthDots * scale);
                int renderHeight = (int)(config.HeightDots * scale);
                int labelX = (canvasWidth - renderWidth) / 2;
                int labelY = (canvasHeight - renderHeight) / 2;

                // Draw label shadow
                using (var shadowBrush = new SolidBrush(Color.FromArgb(30, 0, 0, 0)))
                {
                    g.FillRectangle(shadowBrush, labelX + 4, labelY + 4, renderWidth, renderHeight);
                }

                // Draw label background
                var labelRect = new Rectangle(labelX, labelY, renderWidth, renderHeight);
                using (var whiteBrush = new SolidBrush(Color.White))
                {
                    g.FillRectangle(whiteBrush, labelRect);
                }

                // Draw label border
                using (var borderPen = new Pen(Color.FromArgb(180, 185, 195), 1.5f))
                {
                    g.DrawRectangle(borderPen, labelRect);
                }

                // If height > width (e.g. 31x57 mm), treat virtually as landscape and rotate content 90° clockwise
                bool isRotated = config.HeightMm > config.WidthMm;
                int drawWidth = isRotated ? renderHeight : renderWidth;
                int drawHeight = isRotated ? renderWidth : renderHeight;

                var state = g.Save();
                if (isRotated)
                {
                    g.TranslateTransform(labelX + renderWidth, labelY);
                    g.RotateTransform(90);
                }
                else
                {
                    g.TranslateTransform(labelX, labelY);
                }

                DrawLabelElements(g, drawWidth, drawHeight, scale, config, data);

                g.Restore(state);

                // Draw dimension watermark
                using var dimFont = new Font("Segoe UI", 7.5f, FontStyle.Regular);
                string orientationNote = isRotated ? " (Orientación vertical girada 90°)" : "";
                g.DrawString($"{config.WidthMm}x{config.HeightMm} mm (GAP {config.GapMm}mm){orientationNote}", dimFont, Brushes.Gray, 6, canvasHeight - 16);
            }

            return bmp;
        }

        private static void DrawLabelElements(Graphics g, int drawWidth, int drawHeight, double scale, LabelConfiguration config, PrintTestData data)
        {
            int curY = (int)(6 * scale);

            // ==========================================
            // 1. TOP ROW: PRECIO (Left) & LOGO (Right)
            // ==========================================
            var (pricePrefix, priceAmount) = PriceHelper.ParsePrice(data.PriceText);
            bool hasPrice = data.IncludePrice && !string.IsNullOrWhiteSpace(priceAmount);
            bool hasLogo = data.IncludeLogo && data.LogoImage != null;

            if (hasPrice || hasLogo)
            {
                int targetTopH = data.LogoHeightMm > 0
                    ? (int)(data.LogoHeightMm * (config.Dpi / 25.4) * scale)
                    : (int)(drawHeight * 0.28);

                int topRowH = Math.Clamp(targetTopH, 18, (int)(drawHeight * 0.38));

                // Top Right: Logo
                int logoH = topRowH;
                if (hasLogo && data.LogoImage != null)
                {
                    int maxLogoW = (int)((drawWidth / 2) - (8 * scale));
                    using var resized = ThermalImageConverter.ResizeImage(data.LogoImage, maxLogoW, topRowH);
                    int lx = drawWidth - (int)(6 * scale) - resized.Width;
                    int ly = curY + Math.Max(0, (topRowH - resized.Height) / 2);
                    g.DrawImage(resized, lx, ly);
                    logoH = resized.Height;
                }

                // Top Left: Price (Amount in full height matching image, "BS" beside it at the bottom in 1/3 height)
                if (hasPrice)
                {
                    int priceAreaH = (hasLogo && data.LogoImage != null) ? Math.Max(18, logoH) : topRowH;
                    
                    // Choose font size in pixels so digits visual height matches priceAreaH
                    float basePriceFontSize = Math.Max(12f, priceAreaH * 0.95f);
                    float bsFontSize = Math.Max(7f, basePriceFontSize / 3.0f); // 1/3 of the height

                    // Ensure price + suffix fit within available left-side width
                    int maxPriceW = hasLogo ? (int)((drawWidth / 2) - (10 * scale)) : (int)(drawWidth - (16 * scale));
                    using var testPriceFont = new Font("Segoe UI", basePriceFontSize, FontStyle.Bold, GraphicsUnit.Pixel);
                    using var testBsFont = new Font("Segoe UI", bsFontSize, FontStyle.Bold, GraphicsUnit.Pixel);
                    float wNum = g.MeasureString(priceAmount, testPriceFont).Width;
                    float wBs = g.MeasureString(pricePrefix, testBsFont).Width;
                    float totalPriceW = wNum + wBs + (4 * (float)scale);

                    float actualPriceFontSize = basePriceFontSize;
                    if (totalPriceW > maxPriceW && totalPriceW > 0)
                    {
                        float fitRatio = maxPriceW / totalPriceW;
                        actualPriceFontSize = Math.Max(9f, basePriceFontSize * fitRatio);
                        bsFontSize = Math.Max(6.5f, actualPriceFontSize / 3.0f);
                    }

                    using var priceFont = new Font("Segoe UI", actualPriceFontSize, FontStyle.Bold, GraphicsUnit.Pixel);
                    using var bsFont = new Font("Segoe UI", bsFontSize, FontStyle.Bold, GraphicsUnit.Pixel);
                    using var priceBrush = new SolidBrush(Color.Black);

                    float curX = (float)(6 * scale);
                    float priceY = curY + Math.Max(0, (topRowH - priceAreaH) / 2);

                    // 1. Draw price amount in large (matching image height)
                    var sf = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };
                    var numSize = g.MeasureString(priceAmount, priceFont);
                    var amountRect = new RectangleF(curX, priceY, numSize.Width, priceAreaH);
                    g.DrawString(priceAmount, priceFont, priceBrush, amountRect, sf);

                    curX += numSize.Width + (2 * (float)scale);

                    // 2. Draw "BS" beside it at the bottom (1/3 height, bottom-aligned with digits)
                    var bsSize = g.MeasureString(pricePrefix, bsFont);
                    float bsY = priceY + priceAreaH - bsSize.Height - (2 * (float)scale);
                    g.DrawString(pricePrefix, bsFont, priceBrush, curX, bsY);
                }

                curY += topRowH + (int)(5 * scale);
            }

            // ==========================================
            // 2. MIDDLE: PRODUCT NAME (Centered)
            // ==========================================
            if (data.IncludeName && !string.IsNullOrWhiteSpace(data.ProductName))
            {
                float fontSize = (float)Math.Max(7.5, (drawWidth > 200 && data.ProductName.Length < 25 ? 10 : 8.5) * scale);
                using var nameFont = new Font("Segoe UI", fontSize, FontStyle.Bold);
                using var textBrush = new SolidBrush(Color.FromArgb(20, 20, 20));
                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Near };
                
                int nameH = (int)(18 * scale);
                var textRect = new RectangleF(4, curY, drawWidth - 8, nameH);
                g.DrawString(data.ProductName, nameFont, textBrush, textRect, sf);
                curY += nameH + (int)(4 * scale);
            }

            // ==========================================
            // 3. BOTTOM: BARCODE OR QR (MUTUALLY EXCLUSIVE)
            // ==========================================
            int remainingH = drawHeight - curY - (int)(6 * scale);

            if (data.CodeMode == CodeDisplayMode.Barcode && data.IncludeBarcode && !string.IsNullOrWhiteSpace(data.Barcode))
            {
                int textHeight = (int)(13 * scale);
                int bcHeight = Math.Max(16, remainingH - textHeight);
                int bcWidth = (int)(drawWidth * 0.88); // maximize width
                int bcX = (drawWidth - bcWidth) / 2;

                try
                {
                    using var realBc = Code128Encoder.RenderScannableCode128(data.Barcode, bcWidth, bcHeight);
                    var prevInterp = g.InterpolationMode;
                    g.InterpolationMode = InterpolationMode.NearestNeighbor;
                    g.DrawImage(realBc, bcX, curY, bcWidth, bcHeight);
                    g.InterpolationMode = prevInterp;
                }
                catch
                {
                    using var barBrush = new SolidBrush(Color.Black);
                    g.FillRectangle(barBrush, bcX, curY, bcWidth, bcHeight);
                }

                // Human readable text below barcode
                using var codeFont = new Font("Consolas", (float)Math.Max(7, 7.5 * scale), FontStyle.Regular);
                var sf = new StringFormat { Alignment = StringAlignment.Center };
                g.DrawString(data.Barcode, codeFont, Brushes.Black, new RectangleF(4, curY + bcHeight + 1, drawWidth - 8, textHeight), sf);
            }
            else if (data.CodeMode == CodeDisplayMode.QrCode && data.IncludeQr && !string.IsNullOrWhiteSpace(data.QrData))
            {
                int maxQr = Math.Min(drawWidth - (int)(12 * scale), remainingH);
                int qrSize = Math.Max(20, maxQr);
                int qrX = (drawWidth - qrSize) / 2;
                int qrY = curY + Math.Max(0, (remainingH - qrSize) / 2);

                try
                {
                    using var qrGenerator = new QRCodeGenerator();
                    using var qrCodeData = qrGenerator.CreateQrCode(data.QrData, QRCodeGenerator.ECCLevel.M);
                    using var qrCode = new QRCode(qrCodeData);
                    using Bitmap qrBmp = qrCode.GetGraphic(2, Color.Black, Color.White, drawQuietZones: false);
                    g.DrawImage(qrBmp, qrX, qrY, qrSize, qrSize);
                }
                catch
                {
                    using var pen = new Pen(Color.DarkGray);
                    g.DrawRectangle(pen, qrX, qrY, qrSize, qrSize);
                    g.DrawString("QR", SystemFonts.DefaultFont, Brushes.Black, qrX + 5, qrY + 5);
                }
            }
        }
    }
}
