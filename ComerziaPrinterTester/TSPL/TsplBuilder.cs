using System.Drawing;
using System.Text;
using ComerziaPrinterTester.Models;
using ComerziaPrinterTester.Utils;

namespace ComerziaPrinterTester.TSPL
{
    public class TsplBuilder
    {
        private readonly List<byte[]> _commandChunks = new();
        private readonly StringBuilder _textBuffer = new();
        private readonly Encoding _encoding;

        public TsplBuilder()
        {
            try
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                _encoding = Encoding.GetEncoding("windows-1252");
            }
            catch
            {
                _encoding = Encoding.ASCII;
            }
        }

        public TsplBuilder Clear()
        {
            _commandChunks.Clear();
            _textBuffer.Clear();
            return this;
        }

        private void FlushTextBuffer()
        {
            if (_textBuffer.Length > 0)
            {
                byte[] bytes = _encoding.GetBytes(_textBuffer.ToString());
                _commandChunks.Add(bytes);
                _textBuffer.Clear();
            }
        }

        public TsplBuilder AppendRawCommand(string command)
        {
            _textBuffer.AppendLine(command);
            return this;
        }

        public TsplBuilder InitializeLabel(LabelConfiguration config)
        {
            AppendRawCommand($"SIZE {config.WidthMm} mm,{config.HeightMm} mm");
            AppendRawCommand($"GAP {config.GapMm} mm,0 mm");
            AppendRawCommand("DIRECTION 1");
            AppendRawCommand("REFERENCE 0,0");
            AppendRawCommand("CLS");
            return this;
        }

        public TsplBuilder AppendText(int x, int y, string font, int rotation, int xMultiplication, int yMultiplication, string content)
        {
            // TSPL TEXT command: TEXT x,y,"font",rotation,x-multiplication,y-multiplication,"content"
            // Escape any quotes
            string escaped = content.Replace("\"", "\\\"");
            AppendRawCommand($"TEXT {x},{y},\"{font}\",{rotation},{xMultiplication},{yMultiplication},\"{escaped}\"");
            return this;
        }

        public TsplBuilder AppendBarcode(int x, int y, string codeType, int heightDots, int humanReadable, int rotation, int narrowDots, int wideDots, string content)
        {
            // TSPL BARCODE command: BARCODE x,y,"code_type",height,human_readable,rotation,narrow,wide,"content"
            // Common code_types: "128", "EAN13", "39", "93", etc.
            string escaped = content.Replace("\"", "");
            AppendRawCommand($"BARCODE {x},{y},\"{codeType}\",{heightDots},{humanReadable},{rotation},{narrowDots},{wideDots},\"{escaped}\"");
            return this;
        }

        public TsplBuilder AppendQr(int x, int y, string qrData, int sizeDots, int rotation = 0, IQrGenerator? qrGenerator = null)
        {
            qrGenerator ??= new NativeTsplQrGenerator();
            qrGenerator.AppendQr(this, x, y, qrData, sizeDots, rotation);
            return this;
        }

        public TsplBuilder AppendQr(int x, int y, string qrData, int sizeDots, IQrGenerator? qrGenerator)
            => AppendQr(x, y, qrData, sizeDots, 0, qrGenerator);

        public TsplBuilder AppendBitmap(int x, int y, Bitmap bitmap)
        {
            FlushTextBuffer();

            int width = bitmap.Width;
            int height = bitmap.Height;
            int widthBytes = (width + 7) / 8;

            byte[] bitmapData = ThermalImageConverter.ImageToTsplBitmapData(bitmap);

            // TSPL BITMAP command: BITMAP X,Y,width_in_bytes,height,mode,bitmap_data
            // mode 0: OVERWRITE
            string header = $"BITMAP {x},{y},{widthBytes},{height},0,";
            byte[] headerBytes = _encoding.GetBytes(header);

            _commandChunks.Add(headerBytes);
            _commandChunks.Add(bitmapData);
            _commandChunks.Add(_encoding.GetBytes("\r\n"));

            return this;
        }

        public TsplBuilder AppendPrint(int copies = 1)
        {
            AppendRawCommand($"PRINT {copies}");
            return this;
        }

        /// <summary>
        /// Builds the label layout from dynamic configuration and data.
        /// When Height > Width (e.g. 31x57 mm), orientation changes to vertical with elements rotated 90° clockwise.
        /// </summary>
        public TsplBuilder BuildLabel(LabelConfiguration config, PrintTestData data, IQrGenerator? qrGenerator = null)
        {
            InitializeLabel(config);

            if (config.HeightMm > config.WidthMm)
            {
                BuildPortraitRotatedLabel(config, data, qrGenerator);
            }
            else
            {
                BuildLandscapeLabel(config, data, qrGenerator);
            }

            AppendPrint(config.Quantity);
            return this;
        }

        /// <summary>
        /// Standard horizontal layout (Width >= Height, e.g. 30x20 mm, 50x30 mm).
        /// </summary>
        private void BuildLandscapeLabel(LabelConfiguration config, PrintTestData data, IQrGenerator? qrGenerator)
        {
            int totalWidthDots = config.WidthDots;
            int totalHeightDots = config.HeightDots;

            var (pricePrefix, priceAmount) = PriceHelper.ParsePrice(data.PriceText);
            bool hasPrice = data.IncludePrice && !string.IsNullOrWhiteSpace(priceAmount);
            bool hasLogo = data.IncludeLogo && data.LogoImage != null;
            bool hasTop = hasPrice || hasLogo;
            bool hasName = data.IncludeName && !string.IsNullOrWhiteSpace(data.ProductName);
            bool hasBarcode = data.CodeMode == CodeDisplayMode.Barcode && data.IncludeBarcode && !string.IsNullOrWhiteSpace(data.Barcode);
            bool hasQr = data.CodeMode == CodeDisplayMode.QrCode && data.IncludeQr && !string.IsNullOrWhiteSpace(data.QrData);
            bool hasCode = hasBarcode || hasQr;

            int marginTop = Math.Max(3, (int)(totalHeightDots * 0.03));
            int marginBottom = Math.Max(3, (int)(totalHeightDots * 0.03));
            int gap = Math.Max(2, (int)(totalHeightDots * 0.025));

            int activeCount = (hasTop ? 1 : 0) + (hasName ? 1 : 0) + (hasCode ? 1 : 0);
            int totalGaps = activeCount > 1 ? (activeCount - 1) * gap : 0;
            int usableH = Math.Max(10, totalHeightDots - marginTop - marginBottom - totalGaps);

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

            int currentY = marginTop;

            // ==========================================
            // 1. TOP ROW: PRECIO (Left) & LOGO (Right)
            // ==========================================
            if (hasTop)
            {
                // TOP RIGHT: LOGO
                if (hasLogo && data.LogoImage != null)
                {
                    int maxLogoWidth = Math.Max(24, (totalWidthDots / 2) - 15);
                    int userLogoDots = data.LogoHeightMm > 0 ? (int)Math.Round(data.LogoHeightMm * (config.Dpi / 25.4)) : topRowH;
                    int finalLogoH = Math.Min(topRowH, userLogoDots);

                    using Bitmap resizedLogo = ThermalImageConverter.ResizeImage(data.LogoImage, maxLogoWidth, finalLogoH);
                    int logoX = Math.Max(totalWidthDots / 2, totalWidthDots - 8 - resizedLogo.Width);
                    int logoY = currentY + Math.Max(0, (topRowH - resizedLogo.Height) / 2);
                    AppendBitmap(logoX, logoY, resizedLogo);
                }

                // TOP LEFT: PRECIO
                if (hasPrice)
                {
                    string priceFont;
                    string bsFont;
                    int priceFontH, bsFontH, priceCharW;
                    int xMul = 1, yMul = 1;

                    if (topRowH >= 60)
                    {
                        priceFont = "4"; xMul = 2; yMul = 2; priceFontH = 64; priceCharW = 48;
                        bsFont = "2"; bsFontH = 20;
                    }
                    else if (topRowH >= 44)
                    {
                        priceFont = "5"; priceFontH = 48; priceCharW = 32;
                        bsFont = "2"; bsFontH = 16;
                    }
                    else if (topRowH >= 30)
                    {
                        priceFont = "4"; priceFontH = 32; priceCharW = 24;
                        bsFont = "1"; bsFontH = 12;
                    }
                    else if (topRowH >= 22)
                    {
                        priceFont = "3"; priceFontH = 24; priceCharW = 16;
                        bsFont = "1"; bsFontH = 12;
                    }
                    else if (topRowH >= 18)
                    {
                        priceFont = "2"; priceFontH = 20; priceCharW = 12;
                        bsFont = "1"; bsFontH = 12;
                    }
                    else
                    {
                        priceFont = "1"; priceFontH = 12; priceCharW = 8;
                        bsFont = "1"; bsFontH = 12;
                    }

                    int priceX = 10;
                    int priceY = currentY + Math.Max(0, (topRowH - priceFontH) / 2);

                    AppendText(priceX, priceY, priceFont, 0, xMul, yMul, priceAmount);

                    int amountWidth = priceAmount.Length * priceCharW;
                    int bsX = priceX + amountWidth + 6;
                    int bsY = priceY + priceFontH - bsFontH;
                    AppendText(bsX, bsY, bsFont, 0, 1, 1, pricePrefix);
                }

                currentY += topRowH + gap;
            }

            // ==========================================
            // 2. MIDDLE: PRODUCT NAME (Centered)
            // ==========================================
            if (hasName)
            {
                string nameFont;
                int charWidth, nameHeight;

                if (nameH >= 24 && totalWidthDots >= 280 && data.ProductName.Length < 25)
                {
                    nameFont = "3"; charWidth = 14; nameHeight = 22;
                }
                else if (nameH >= 18)
                {
                    nameFont = "2"; charWidth = 10; nameHeight = 16;
                }
                else
                {
                    nameFont = "1"; charWidth = 8; nameHeight = 12;
                }

                int textWidth = data.ProductName.Length * charWidth;
                int textX = Math.Max(8, (totalWidthDots - textWidth) / 2);
                int nameY = currentY + Math.Max(0, (nameH - nameHeight) / 2);

                AppendText(textX, nameY, nameFont, 0, 1, 1, data.ProductName);
                currentY += nameH + gap;
            }

            // ==========================================
            // 3. BOTTOM: BARCODE OR QR (MUTUALLY EXCLUSIVE)
            // ==========================================
            if (hasBarcode)
            {
                int textMargin = Math.Min(18, Math.Max(12, (int)(codeH * 0.32)));
                int barcodeHeight = Math.Max(12, codeH - textMargin);

                string tsplCode = data.BarcodeType.ToUpper() switch
                {
                    "CODE128" or "128" => "128",
                    "EAN13" or "EAN-13" => "EAN13",
                    "CODE39" or "39" => "39",
                    _ => "128"
                };

                int modules = BarcodeHelper.CalculateBarcodeModules(data.Barcode, tsplCode);
                
                int narrow = 2;
                if (modules * 3 <= totalWidthDots - 20)
                {
                    narrow = 3;
                }
                else if (modules * 2 <= totalWidthDots - 16)
                {
                    narrow = 2;
                }
                else
                {
                    narrow = 1;
                }

                int bcTotalWidth = modules * narrow;
                int bcX = Math.Max(8, (totalWidthDots - bcTotalWidth) / 2);

                // humanReadable = 2 centers the text below the barcode in TSPL
                AppendBarcode(bcX, currentY, tsplCode, barcodeHeight, 2, 0, narrow, narrow, data.Barcode);
            }
            else if (hasQr)
            {
                int maxAvailableDim = Math.Min(totalWidthDots - 20, codeH);
                int qrSize = Math.Max(18, maxAvailableDim);
                int qrX = (totalWidthDots - qrSize) / 2;
                int qrY = currentY + Math.Max(0, (codeH - qrSize) / 2);

                AppendQr(qrX, qrY, data.QrData, qrSize, 0, qrGenerator);
            }
        }

        /// <summary>
        /// Vertical / Portrait layout (Height > Width, e.g. 31x57 mm).
        /// Treats label dimensions virtually as landscape (57x31 mm) and rotates all elements 90° clockwise.
        /// </summary>
        private void BuildPortraitRotatedLabel(LabelConfiguration config, PrintTestData data, IQrGenerator? qrGenerator)
        {
            int totalWidthDots = config.WidthDots;   // e.g. 248 dots for 31mm
            int totalHeightDots = config.HeightDots; // e.g. 456 dots for 57mm

            // Virtual landscape canvas dimensions
            int vWidthDots = totalHeightDots; // 456 dots (the long dimension)
            int vHeightDots = totalWidthDots; // 248 dots (the short dimension)

            var (pricePrefix, priceAmount) = PriceHelper.ParsePrice(data.PriceText);
            bool hasPrice = data.IncludePrice && !string.IsNullOrWhiteSpace(priceAmount);
            bool hasLogo = data.IncludeLogo && data.LogoImage != null;
            bool hasTop = hasPrice || hasLogo;
            bool hasName = data.IncludeName && !string.IsNullOrWhiteSpace(data.ProductName);
            bool hasBarcode = data.CodeMode == CodeDisplayMode.Barcode && data.IncludeBarcode && !string.IsNullOrWhiteSpace(data.Barcode);
            bool hasQr = data.CodeMode == CodeDisplayMode.QrCode && data.IncludeQr && !string.IsNullOrWhiteSpace(data.QrData);
            bool hasCode = hasBarcode || hasQr;

            int marginTop = Math.Max(3, (int)(vHeightDots * 0.03));
            int marginBottom = Math.Max(3, (int)(vHeightDots * 0.03));
            int gap = Math.Max(2, (int)(vHeightDots * 0.025));

            int activeCount = (hasTop ? 1 : 0) + (hasName ? 1 : 0) + (hasCode ? 1 : 0);
            int totalGaps = activeCount > 1 ? (activeCount - 1) * gap : 0;
            int usableH = Math.Max(10, vHeightDots - marginTop - marginBottom - totalGaps);

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

            int currentY = marginTop;

            // ==========================================
            // 1. TOP ROW: PRECIO (Left) & LOGO (Right)
            // ==========================================
            if (hasTop)
            {
                // TOP RIGHT: LOGO (in virtual landscape)
                if (hasLogo && data.LogoImage != null)
                {
                    int maxLogoWidth = Math.Max(24, (vWidthDots / 2) - 15);
                    int userLogoDots = data.LogoHeightMm > 0 ? (int)Math.Round(data.LogoHeightMm * (config.Dpi / 25.4)) : topRowH;
                    int finalLogoH = Math.Min(topRowH, userLogoDots);

                    using Bitmap resizedLogo = ThermalImageConverter.ResizeImage(data.LogoImage, maxLogoWidth, finalLogoH);

                    int vx = Math.Max(vWidthDots / 2, vWidthDots - 8 - resizedLogo.Width);
                    int vy = currentY + Math.Max(0, (topRowH - resizedLogo.Height) / 2);

                    // Rotate logo bitmap 90° clockwise for thermal printer
                    using Bitmap rotatedLogo = (Bitmap)resizedLogo.Clone();
                    rotatedLogo.RotateFlip(RotateFlipType.Rotate90FlipNone);

                    int px = Math.Max(0, totalWidthDots - vy - rotatedLogo.Width);
                    int py = vx;
                    AppendBitmap(px, py, rotatedLogo);
                }

                // TOP LEFT: PRECIO
                if (hasPrice)
                {
                    string priceFont;
                    string bsFont;
                    int priceFontH, bsFontH, priceCharW;
                    int xMul = 1, yMul = 1;

                    if (topRowH >= 60)
                    {
                        priceFont = "4"; xMul = 2; yMul = 2; priceFontH = 64; priceCharW = 48;
                        bsFont = "2"; bsFontH = 20;
                    }
                    else if (topRowH >= 44)
                    {
                        priceFont = "5"; priceFontH = 48; priceCharW = 32;
                        bsFont = "2"; bsFontH = 16;
                    }
                    else if (topRowH >= 30)
                    {
                        priceFont = "4"; priceFontH = 32; priceCharW = 24;
                        bsFont = "1"; bsFontH = 12;
                    }
                    else if (topRowH >= 22)
                    {
                        priceFont = "3"; priceFontH = 24; priceCharW = 16;
                        bsFont = "1"; bsFontH = 12;
                    }
                    else if (topRowH >= 18)
                    {
                        priceFont = "2"; priceFontH = 20; priceCharW = 12;
                        bsFont = "1"; bsFontH = 12;
                    }
                    else
                    {
                        priceFont = "1"; priceFontH = 12; priceCharW = 8;
                        bsFont = "1"; bsFontH = 12;
                    }

                    int vx_amount = 10;
                    int vy_amount = currentY + Math.Max(0, (topRowH - priceFontH) / 2);

                    int amountWidth = priceAmount.Length * priceCharW;
                    int vx_bs = vx_amount + amountWidth + 6;
                    int vy_bs = vy_amount + priceFontH - bsFontH;

                    int px_amount = Math.Max(priceFontH + 4, totalWidthDots - vy_amount - 4);
                    int py_amount = vx_amount;
                    AppendText(px_amount, py_amount, priceFont, 90, xMul, yMul, priceAmount);

                    int px_bs = Math.Max(bsFontH + 4, totalWidthDots - vy_bs - 4);
                    int py_bs = vx_bs;
                    AppendText(px_bs, py_bs, bsFont, 90, 1, 1, pricePrefix);
                }

                currentY += topRowH + gap;
            }

            // ==========================================
            // 2. MIDDLE: PRODUCT NAME (Centered)
            // ==========================================
            if (hasName)
            {
                string nameFont;
                int charWidth, nameHeight;

                if (nameH >= 24 && vWidthDots >= 280 && data.ProductName.Length < 25)
                {
                    nameFont = "3"; charWidth = 14; nameHeight = 22;
                }
                else if (nameH >= 18)
                {
                    nameFont = "2"; charWidth = 10; nameHeight = 16;
                }
                else
                {
                    nameFont = "1"; charWidth = 8; nameHeight = 12;
                }

                int textWidth = data.ProductName.Length * charWidth;
                int vx = Math.Max(8, (vWidthDots - textWidth) / 2);
                int vy = currentY + Math.Max(0, (nameH - nameHeight) / 2);

                int px = Math.Max(nameHeight + 4, totalWidthDots - vy - 4);
                int py = vx;
                AppendText(px, py, nameFont, 90, 1, 1, data.ProductName);
                currentY += nameH + gap;
            }

            // ==========================================
            // 3. BOTTOM: BARCODE OR QR (MUTUALLY EXCLUSIVE)
            // ==========================================
            if (hasBarcode)
            {
                int textMargin = Math.Min(18, Math.Max(12, (int)(codeH * 0.32)));
                int barcodeHeight = Math.Max(12, codeH - textMargin);

                string tsplCode = data.BarcodeType.ToUpper() switch
                {
                    "CODE128" or "128" => "128",
                    "EAN13" or "EAN-13" => "EAN13",
                    "CODE39" or "39" => "39",
                    _ => "128"
                };

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

                int bcTotalWidth = modules * narrow;
                int vx = Math.Max(8, (vWidthDots - bcTotalWidth) / 2);
                int vy = currentY;

                int px = Math.Max(barcodeHeight + textMargin + 4, totalWidthDots - vy - 4);
                int py = vx;

                AppendBarcode(px, py, tsplCode, barcodeHeight, 2, 90, narrow, narrow, data.Barcode);
            }
            else if (hasQr)
            {
                int maxAvailableDim = Math.Min(vWidthDots - 20, codeH);
                int qrSize = Math.Max(18, maxAvailableDim);
                int vx = (vWidthDots - qrSize) / 2;
                int vy = currentY + Math.Max(0, (codeH - qrSize) / 2);

                int px = Math.Max(qrSize + 4, totalWidthDots - vy - 4);
                int py = vx;

                AppendQr(px, py, data.QrData, qrSize, 90, qrGenerator);
            }
        }

        /// <summary>
        /// Converts the constructed command chunks into a single byte array for network transmission.
        /// </summary>
        public byte[] ToBytes()
        {
            FlushTextBuffer();

            int totalLength = _commandChunks.Sum(c => c.Length);
            byte[] result = new byte[totalLength];
            int offset = 0;

            foreach (var chunk in _commandChunks)
            {
                Buffer.BlockCopy(chunk, 0, result, offset, chunk.Length);
                offset += chunk.Length;
            }

            return result;
        }

        /// <summary>
        /// Returns a string representation of the TSPL script for preview and inspection.
        /// Binary bitmap payloads are represented cleanly.
        /// </summary>
        public string ToReadableString()
        {
            var sb = new StringBuilder();
            FlushTextBuffer();

            foreach (var chunk in _commandChunks)
            {
                string text = _encoding.GetString(chunk);
                if (text.StartsWith("BITMAP "))
                {
                    int commaIndex = text.IndexOf(',', text.IndexOf("BITMAP ") + 7);
                    sb.AppendLine($"[BINARY BITMAP PAYLOAD ({chunk.Length} bytes)]");
                }
                else
                {
                    sb.Append(text);
                }
            }

            return sb.ToString();
        }
    }
}
