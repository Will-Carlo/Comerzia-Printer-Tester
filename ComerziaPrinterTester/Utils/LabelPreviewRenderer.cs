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
            var (pricePrefix, priceAmount) = PriceHelper.ParsePrice(data.PriceText);
            bool hasPrice = data.IncludePrice && !string.IsNullOrWhiteSpace(priceAmount);
            bool hasLogo = data.IncludeLogo && data.LogoImage != null;
            bool hasTop = hasPrice || hasLogo;
            bool hasName = data.IncludeName && !string.IsNullOrWhiteSpace(data.ProductName);
            bool hasBarcode = data.CodeMode == CodeDisplayMode.Barcode && data.IncludeBarcode && !string.IsNullOrWhiteSpace(data.Barcode);
            bool hasQr = data.CodeMode == CodeDisplayMode.QrCode && data.IncludeQr && !string.IsNullOrWhiteSpace(data.QrData);
            bool hasCode = hasBarcode || hasQr;

            // Proportional vertical budgeting guarantees zero element cutoff regardless of dimensions
            int topMargin = Math.Max(2, (int)(drawHeight * 0.03));
            int bottomMargin = Math.Max(2, (int)(drawHeight * 0.03));
            int gap = Math.Max(2, (int)(drawHeight * 0.025));

            int activeCount = (hasTop ? 1 : 0) + (hasName ? 1 : 0) + (hasCode ? 1 : 0);
            int totalGaps = activeCount > 1 ? (activeCount - 1) * gap : 0;
            int usableH = Math.Max(10, drawHeight - topMargin - bottomMargin - totalGaps);

            int topRowH = 0;
            int nameH = 0;
            int codeH = 0;

            if (hasTop && hasName && hasCode)
            {
                topRowH = (int)(usableH * 0.32);
                nameH = (int)(usableH * 0.20);
                codeH = usableH - topRowH - nameH;
            }
            else if (hasTop && hasCode && !hasName)
            {
                topRowH = (int)(usableH * 0.38);
                codeH = usableH - topRowH;
            }
            else if (hasName && hasCode && !hasTop)
            {
                nameH = (int)(usableH * 0.26);
                codeH = usableH - nameH;
            }
            else if (hasTop && hasName && !hasCode)
            {
                topRowH = (int)(usableH * 0.58);
                nameH = usableH - topRowH;
            }
            else
            {
                if (hasTop) topRowH = usableH;
                else if (hasName) nameH = usableH;
                else if (hasCode) codeH = usableH;
            }

            int curY = topMargin;

            // ==========================================
            // 1. TOP ROW: PRECIO (Left) & LOGO (Right)
            // ==========================================
            if (hasTop)
            {
                // Top Right: Logo
                int logoH = topRowH;
                if (hasLogo && data.LogoImage != null)
                {
                    int maxLogoW = Math.Max(16, (int)((drawWidth / 2) - (8 * scale)));
                    int userTargetH = data.LogoHeightMm > 0 ? (int)(data.LogoHeightMm * (config.Dpi / 25.4) * scale) : topRowH;
                    int finalLogoH = Math.Min(topRowH, userTargetH);

                    using var resized = ThermalImageConverter.ResizeImage(data.LogoImage, maxLogoW, finalLogoH);
                    int lx = drawWidth - (int)(6 * scale) - resized.Width;
                    int ly = curY + Math.Max(0, (topRowH - resized.Height) / 2);
                    g.DrawImage(resized, lx, ly);
                    logoH = resized.Height;
                }

                // Top Left: Price (Amount in full height, "BS" at 1/3 height bottom-aligned)
                if (hasPrice)
                {
                    int priceAreaH = (hasLogo && data.LogoImage != null) ? Math.Min(topRowH, Math.Max(12, logoH)) : topRowH;

                    float basePriceFontSize = Math.Max(8f, priceAreaH * 0.90f);
                    float bsFontSize = Math.Max(5.5f, basePriceFontSize / 3.0f);

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
                        actualPriceFontSize = Math.Max(7.5f, basePriceFontSize * fitRatio);
                        bsFontSize = Math.Max(5f, actualPriceFontSize / 3.0f);
                    }

                    using var priceFont = new Font("Segoe UI", actualPriceFontSize, FontStyle.Bold, GraphicsUnit.Pixel);
                    using var bsFont = new Font("Segoe UI", bsFontSize, FontStyle.Bold, GraphicsUnit.Pixel);
                    using var priceBrush = new SolidBrush(Color.Black);

                    float curX = (float)(6 * scale);
                    float priceY = curY + Math.Max(0, (topRowH - priceAreaH) / 2);

                    var sf = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };
                    var numSize = g.MeasureString(priceAmount, priceFont);
                    var amountRect = new RectangleF(curX, priceY, numSize.Width, priceAreaH);
                    g.DrawString(priceAmount, priceFont, priceBrush, amountRect, sf);

                    curX += numSize.Width + (2 * (float)scale);

                    var bsSize = g.MeasureString(pricePrefix, bsFont);
                    float bsY = priceY + priceAreaH - bsSize.Height - (1 * (float)scale);
                    g.DrawString(pricePrefix, bsFont, priceBrush, curX, bsY);
                }

                curY += topRowH + gap;
            }

            // ==========================================
            // 2. MIDDLE: PRODUCT NAME (Centered)
            // ==========================================
            if (hasName)
            {
                float fontSize = Math.Clamp(nameH * 0.60f, 7f, 13f);
                using var nameFont = new Font("Segoe UI", fontSize, FontStyle.Bold);
                using var textBrush = new SolidBrush(Color.FromArgb(20, 20, 20));
                var sf = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center,
                    Trimming = StringTrimming.EllipsisCharacter
                };

                var textRect = new RectangleF(4, curY, drawWidth - 8, nameH);
                g.DrawString(data.ProductName, nameFont, textBrush, textRect, sf);
                curY += nameH + gap;
            }

            // ==========================================
            // 3. BOTTOM: BARCODE OR QR (MUTUALLY EXCLUSIVE)
            // ==========================================
            if (hasBarcode)
            {
                string tsplCode = data.BarcodeType.ToUpper() switch
                {
                    "CODE128" or "128" => "128",
                    "EAN13" or "EAN-13" => "EAN13",
                    "CODE39" or "39" => "39",
                    _ => "128"
                };

                int vWidthDots = config.HeightMm > config.WidthMm ? config.HeightDots : config.WidthDots;
                int modules = BarcodeHelper.CalculateBarcodeModules(data.Barcode, tsplCode);

                int narrow = 2;
                if (modules * 3 <= vWidthDots - 20)
                {
                    narrow = 3;
                }
                else if (modules * 2 <= vWidthDots - 16)
                {
                    narrow = 2;
                }
                else
                {
                    narrow = 1;
                }

                int bcTotalWidthDots = modules * narrow;
                int bcXDots = Math.Max(8, (vWidthDots - bcTotalWidthDots) / 2);

                int textHeight = Math.Min(16, Math.Max(9, (int)(codeH * 0.32)));
                int bcHeight = Math.Max(10, codeH - textHeight);
                int bcWidth = Math.Max(20, (int)(bcTotalWidthDots * scale));
                int bcX = (int)(bcXDots * scale);

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

                // Human readable text below barcode - vertically centered within textHeight box
                float codeFontSize = Math.Clamp(textHeight * 0.70f, 6.5f, 10f);
                using var codeFont = new Font("Consolas", codeFontSize, FontStyle.Regular);
                var sf = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };
                g.DrawString(data.Barcode, codeFont, Brushes.Black, new RectangleF(bcX, curY + bcHeight, bcWidth, textHeight), sf);
            }
            else if (hasQr)
            {
                int maxQr = Math.Min(drawWidth - (int)(12 * scale), codeH);
                int qrSize = Math.Max(16, maxQr);
                int qrX = (drawWidth - qrSize) / 2;
                int qrY = curY + Math.Max(0, (codeH - qrSize) / 2);

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
