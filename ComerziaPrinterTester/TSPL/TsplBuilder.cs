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
            int currentY = 8; // Top margin

            // ==========================================
            // 1. TOP ROW: PRECIO (Left) & LOGO (Right)
            // ==========================================
            var (pricePrefix, priceAmount) = PriceHelper.ParsePrice(data.PriceText);
            bool hasPrice = data.IncludePrice && !string.IsNullOrWhiteSpace(priceAmount);
            bool hasLogo = data.IncludeLogo && data.LogoImage != null;

            int topRowHeightDots = 0;
            if (hasPrice || hasLogo)
            {
                int userLogoDots = data.LogoHeightMm > 0 ? (int)Math.Round(data.LogoHeightMm * (config.Dpi / 25.4)) : 0;
                topRowHeightDots = userLogoDots > 0
                    ? Math.Min(totalHeightDots / 3, userLogoDots)
                    : Math.Clamp((int)(totalHeightDots * 0.25), 24, 80);

                // TOP RIGHT: LOGO
                if (hasLogo && data.LogoImage != null)
                {
                    int maxLogoWidth = Math.Max(24, (totalWidthDots / 2) - 15);
                    using Bitmap resizedLogo = ThermalImageConverter.ResizeImage(data.LogoImage, maxLogoWidth, topRowHeightDots);
                    
                    int logoX = Math.Max(totalWidthDots / 2, totalWidthDots - 8 - resizedLogo.Width);
                    int logoY = currentY + Math.Max(0, (topRowHeightDots - resizedLogo.Height) / 2);
                    AppendBitmap(logoX, logoY, resizedLogo);
                }

                // TOP LEFT: PRECIO (Amount in full-height matching image, "BS" beside it at bottom in 1/3 height)
                if (hasPrice)
                {
                    string priceFont;
                    string bsFont;
                    int priceFontH, bsFontH, priceCharW;
                    int xMul = 1, yMul = 1;

                    if (topRowHeightDots >= 56)
                    {
                        priceFont = "4"; xMul = 2; yMul = 2; priceFontH = 64; priceCharW = 48;
                        bsFont = "2"; bsFontH = 20; // ~1/3 of 64
                    }
                    else if (topRowHeightDots >= 40)
                    {
                        priceFont = "5"; priceFontH = 48; priceCharW = 32;
                        bsFont = "2"; bsFontH = 16; // 1/3 of 48
                    }
                    else if (topRowHeightDots >= 26)
                    {
                        priceFont = "4"; priceFontH = 32; priceCharW = 24;
                        bsFont = "1"; bsFontH = 12; // ~1/3 of 32
                    }
                    else
                    {
                        priceFont = "3"; priceFontH = 24; priceCharW = 16;
                        bsFont = "1"; bsFontH = 12;
                    }

                    int priceX = 10;
                    int priceY = currentY + Math.Max(0, (topRowHeightDots - priceFontH) / 2);

                    // 1. Print Price Amount in full font (matching image height)
                    AppendText(priceX, priceY, priceFont, 0, xMul, yMul, priceAmount);

                    // 2. Print "BS" beside it in lower part (1/3 height, bottom-aligned with digits)
                    int amountWidth = priceAmount.Length * priceCharW;
                    int bsX = priceX + amountWidth + 6;
                    int bsY = priceY + priceFontH - bsFontH;
                    AppendText(bsX, bsY, bsFont, 0, 1, 1, pricePrefix);
                }

                currentY += topRowHeightDots + 6;
            }

            // ==========================================
            // 2. MIDDLE: PRODUCT NAME (Centered)
            // ==========================================
            if (data.IncludeName && !string.IsNullOrWhiteSpace(data.ProductName))
            {
                string nameFont = totalWidthDots >= 300 && data.ProductName.Length < 25 ? "3" : "2";
                int charWidth = nameFont == "3" ? 14 : 10;
                int nameHeight = nameFont == "3" ? 22 : 16;

                int textWidth = data.ProductName.Length * charWidth;
                int textX = Math.Max(8, (totalWidthDots - textWidth) / 2);

                AppendText(textX, currentY, nameFont, 0, 1, 1, data.ProductName);
                currentY += nameHeight + 6;
            }

            // ==========================================
            // 3. BOTTOM: BARCODE OR QR (MUTUALLY EXCLUSIVE)
            // ==========================================
            int remainingSpace = totalHeightDots - currentY - 8;

            if (data.CodeMode == CodeDisplayMode.Barcode && data.IncludeBarcode && !string.IsNullOrWhiteSpace(data.Barcode))
            {
                int textMargin = 18;
                int barcodeHeight = Math.Max(18, remainingSpace - textMargin);

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
            else if (data.CodeMode == CodeDisplayMode.QrCode && data.IncludeQr && !string.IsNullOrWhiteSpace(data.QrData))
            {
                int maxAvailableDim = Math.Min(totalWidthDots - 20, remainingSpace);
                int qrSize = Math.Max(24, maxAvailableDim);
                int qrX = (totalWidthDots - qrSize) / 2;
                int qrY = currentY + Math.Max(0, (remainingSpace - qrSize) / 2);

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
            int currentY = 8; // Top margin in virtual landscape

            // ==========================================
            // 1. TOP ROW: PRECIO (Left) & LOGO (Right)
            // ==========================================
            var (pricePrefix, priceAmount) = PriceHelper.ParsePrice(data.PriceText);
            bool hasPrice = data.IncludePrice && !string.IsNullOrWhiteSpace(priceAmount);
            bool hasLogo = data.IncludeLogo && data.LogoImage != null;

            int topRowHeightDots = 0;
            if (hasPrice || hasLogo)
            {
                int userLogoDots = data.LogoHeightMm > 0 ? (int)Math.Round(data.LogoHeightMm * (config.Dpi / 25.4)) : 0;
                topRowHeightDots = userLogoDots > 0
                    ? Math.Min(vHeightDots / 3, userLogoDots)
                    : Math.Clamp((int)(vHeightDots * 0.25), 24, 80);

                // TOP RIGHT: LOGO (in virtual landscape)
                if (hasLogo && data.LogoImage != null)
                {
                    int maxLogoWidth = Math.Max(24, (vWidthDots / 2) - 15);
                    using Bitmap resizedLogo = ThermalImageConverter.ResizeImage(data.LogoImage, maxLogoWidth, topRowHeightDots);

                    int vx = Math.Max(vWidthDots / 2, vWidthDots - 8 - resizedLogo.Width);
                    int vy = currentY + Math.Max(0, (topRowHeightDots - resizedLogo.Height) / 2);

                    // Rotate logo bitmap 90° clockwise for thermal printer
                    using Bitmap rotatedLogo = (Bitmap)resizedLogo.Clone();
                    rotatedLogo.RotateFlip(RotateFlipType.Rotate90FlipNone);

                    int px = Math.Max(0, totalWidthDots - vy - rotatedLogo.Width);
                    int py = vx;
                    AppendBitmap(px, py, rotatedLogo);
                }

                // TOP LEFT: PRECIO (Amount in full-height matching image, "BS" beside it at bottom in 1/3 height)
                if (hasPrice)
                {
                    string priceFont;
                    string bsFont;
                    int priceFontH, bsFontH, priceCharW;
                    int xMul = 1, yMul = 1;

                    if (topRowHeightDots >= 56)
                    {
                        priceFont = "4"; xMul = 2; yMul = 2; priceFontH = 64; priceCharW = 48;
                        bsFont = "2"; bsFontH = 20; // ~1/3 of 64
                    }
                    else if (topRowHeightDots >= 40)
                    {
                        priceFont = "5"; priceFontH = 48; priceCharW = 32;
                        bsFont = "2"; bsFontH = 16; // 1/3 of 48
                    }
                    else if (topRowHeightDots >= 26)
                    {
                        priceFont = "4"; priceFontH = 32; priceCharW = 24;
                        bsFont = "1"; bsFontH = 12; // ~1/3 of 32
                    }
                    else
                    {
                        priceFont = "3"; priceFontH = 24; priceCharW = 16;
                        bsFont = "1"; bsFontH = 12;
                    }

                    int vx_amount = 10;
                    int vy_amount = currentY + Math.Max(0, (topRowHeightDots - priceFontH) / 2);

                    int amountWidth = priceAmount.Length * priceCharW;
                    int vx_bs = vx_amount + amountWidth + 6;
                    int vy_bs = vy_amount + priceFontH - bsFontH;

                    // 1. Print Price Amount in full font
                    int px_amount = Math.Max(priceFontH + 4, totalWidthDots - vy_amount - 4);
                    int py_amount = vx_amount;
                    AppendText(px_amount, py_amount, priceFont, 90, xMul, yMul, priceAmount);

                    // 2. Print "BS" beside it in lower part (1/3 height, bottom-aligned with digits)
                    int px_bs = Math.Max(bsFontH + 4, totalWidthDots - vy_bs - 4);
                    int py_bs = vx_bs;
                    AppendText(px_bs, py_bs, bsFont, 90, 1, 1, pricePrefix);
                }

                currentY += topRowHeightDots + 6;
            }

            // ==========================================
            // 2. MIDDLE: PRODUCT NAME (Centered)
            // ==========================================
            if (data.IncludeName && !string.IsNullOrWhiteSpace(data.ProductName))
            {
                string nameFont = vWidthDots >= 300 && data.ProductName.Length < 25 ? "3" : "2";
                int charWidth = nameFont == "3" ? 14 : 10;
                int nameHeight = nameFont == "3" ? 22 : 16;

                int textWidth = data.ProductName.Length * charWidth;
                int vx = Math.Max(8, (vWidthDots - textWidth) / 2);
                int vy = currentY;

                int px = Math.Max(nameHeight + 4, totalWidthDots - vy - 4);
                int py = vx;
                AppendText(px, py, nameFont, 90, 1, 1, data.ProductName);
                currentY += nameHeight + 6;
            }

            // ==========================================
            // 3. BOTTOM: BARCODE OR QR (MUTUALLY EXCLUSIVE)
            // ==========================================
            int remainingSpace = vHeightDots - currentY - 8;

            if (data.CodeMode == CodeDisplayMode.Barcode && data.IncludeBarcode && !string.IsNullOrWhiteSpace(data.Barcode))
            {
                int textMargin = 18;
                int barcodeHeight = Math.Max(18, remainingSpace - textMargin);

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

                // humanReadable = 2 centers the text below the barcode in TSPL
                AppendBarcode(px, py, tsplCode, barcodeHeight, 2, 90, narrow, narrow, data.Barcode);
            }
            else if (data.CodeMode == CodeDisplayMode.QrCode && data.IncludeQr && !string.IsNullOrWhiteSpace(data.QrData))
            {
                int maxAvailableDim = Math.Min(vWidthDots - 20, remainingSpace);
                int qrSize = Math.Max(24, maxAvailableDim);
                int vx = (vWidthDots - qrSize) / 2;
                int vy = currentY + Math.Max(0, (remainingSpace - qrSize) / 2);

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
