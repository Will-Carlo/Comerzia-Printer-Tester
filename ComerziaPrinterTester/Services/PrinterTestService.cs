using System.Text;
using ComerziaPrinterTester.Models;
using ComerziaPrinterTester.TSPL;

namespace ComerziaPrinterTester.Services
{
    public class PrinterTestService
    {
        private readonly PrinterTcpClient _tcpClient;

        public PrinterTestService(PrinterTcpClient? tcpClient = null)
        {
            _tcpClient = tcpClient ?? new PrinterTcpClient();
        }

        public async Task<(bool Success, string Message, long ElapsedMs)> TestConnectionAsync(string ip, int port)
        {
            return await _tcpClient.TestConnectionAsync(ip, port);
        }

        public byte[] GenerateTsplBytes(LabelConfiguration config, PrintTestData data, IQrGenerator? qrGenerator = null)
        {
            var builder = new TsplBuilder();
            builder.BuildLabel(config, data, qrGenerator);
            return builder.ToBytes();
        }

        public string GenerateTsplPreviewText(LabelConfiguration config, PrintTestData data, IQrGenerator? qrGenerator = null)
        {
            var builder = new TsplBuilder();
            builder.BuildLabel(config, data, qrGenerator);
            return builder.ToReadableString();
        }

        public async Task<(bool Success, string Message, int BytesSent)> PrintLabelAsync(
            string ip,
            int port,
            LabelConfiguration config,
            PrintTestData data,
            IQrGenerator? qrGenerator = null,
            Action<string>? logAction = null)
        {
            byte[] bytes = GenerateTsplBytes(config, data, qrGenerator);
            return await _tcpClient.SendBytesAsync(ip, port, bytes, logAction);
        }

        public async Task<(bool Success, string Message, int BytesSent)> SendRawTsplAsync(
            string ip,
            int port,
            string rawTspl,
            Action<string>? logAction = null)
        {
            // TSPL standard expects CRLF line endings
            string normalized = rawTspl.Replace("\r\n", "\n").Replace("\n", "\r\n");
            if (!normalized.EndsWith("\r\n"))
            {
                normalized += "\r\n";
            }

            Encoding enc;
            try
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                enc = Encoding.GetEncoding("windows-1252");
            }
            catch
            {
                enc = Encoding.ASCII;
            }

            byte[] bytes = enc.GetBytes(normalized);
            return await _tcpClient.SendBytesAsync(ip, port, bytes, logAction);
        }

        /// <summary>
        /// Generates multi-label test sequence:
        /// Label 1: Text + Barcode + QR
        /// Label 2: Long Text + Price + Barcode
        /// Label 3: Logo + Text + Price + QR
        /// Label 4: Logo + Text + Barcode + QR
        /// </summary>
        public async Task<(bool Success, string Message, int TotalBytesSent)> PrintTestSheetAsync(
            string ip,
            int port,
            LabelConfiguration baseConfig,
            PrintTestData baseData,
            IQrGenerator? qrGenerator = null,
            Action<string>? logAction = null)
        {
            logAction?.Invoke("=== INICIANDO HOJA DE PRUEBA MULTI-ETIQUETA ===");

            var testLabels = new List<PrintTestData>
            {
                // Etiqueta 1: Texto + Barcode + QR
                new PrintTestData
                {
                    ProductName = "PRUEBA 1: BASICA",
                    PriceText = "",
                    Barcode = "200000001001",
                    BarcodeType = baseData.BarcodeType,
                    QrData = "https://comerzia.com/test1",
                    CodeMode = CodeDisplayMode.Barcode,
                    IncludeLogo = false,
                    IncludeName = true,
                    IncludePrice = false
                },
                // Etiqueta 2: Texto más largo + Precio + Barcode
                new PrintTestData
                {
                    ProductName = "PRUEBA 2: REFRESCO COCA COLA 500ML RETORNABLE",
                    PriceText = "Bs. 18.50",
                    Barcode = "7771234567890",
                    BarcodeType = baseData.BarcodeType,
                    QrData = "",
                    CodeMode = CodeDisplayMode.Barcode,
                    IncludeLogo = false,
                    IncludeName = true,
                    IncludePrice = true
                },
                // Etiqueta 3: Logo + Texto + Precio + QR
                new PrintTestData
                {
                    ProductName = "PRUEBA 3: LOGO + QR",
                    PriceText = "Bs. 99.00",
                    Barcode = "",
                    BarcodeType = baseData.BarcodeType,
                    QrData = "https://comerzia.com/promo/99",
                    CodeMode = CodeDisplayMode.QrCode,
                    LogoImage = baseData.LogoImage,
                    IncludeLogo = baseData.LogoImage != null,
                    IncludeName = true,
                    IncludePrice = true
                },
                // Etiqueta 4: Logo + Texto + Barcode
                new PrintTestData
                {
                    ProductName = "PRUEBA 4: COMPLETA",
                    PriceText = "Bs. 25.50",
                    Barcode = baseData.Barcode,
                    BarcodeType = baseData.BarcodeType,
                    QrData = baseData.QrData,
                    CodeMode = baseData.CodeMode,
                    LogoImage = baseData.LogoImage,
                    IncludeLogo = baseData.LogoImage != null,
                    IncludeName = true,
                    IncludePrice = true
                }
            };

            int totalBytes = 0;
            int labelIndex = 1;

            foreach (var labelData in testLabels)
            {
                logAction?.Invoke($"Preparando Etiqueta {labelIndex}/{testLabels.Count}...");
                var singleConfig = new LabelConfiguration
                {
                    WidthMm = baseConfig.WidthMm,
                    HeightMm = baseConfig.HeightMm,
                    GapMm = baseConfig.GapMm,
                    Quantity = 1,
                    Dpi = baseConfig.Dpi
                };

                byte[] labelBytes = GenerateTsplBytes(singleConfig, labelData, qrGenerator);
                var result = await _tcpClient.SendBytesAsync(ip, port, labelBytes, logAction);
                
                if (!result.Success)
                {
                    logAction?.Invoke($"Fallo en etiqueta {labelIndex}: {result.Message}");
                    return (false, $"Fallo en etiqueta {labelIndex}: {result.Message}", totalBytes);
                }

                totalBytes += result.BytesSent;
                labelIndex++;
                await Task.Delay(400); // Small pause between commands
            }

            logAction?.Invoke($"=== HOJA DE PRUEBA FINALIZADA: {totalBytes} bytes transmitidos ===");
            return (true, $"Hoja de prueba completada ({testLabels.Count} etiquetas impresas).", totalBytes);
        }
    }
}
