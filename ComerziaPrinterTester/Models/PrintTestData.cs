using System.Drawing;

namespace ComerziaPrinterTester.Models
{
    public enum CodeDisplayMode
    {
        Barcode,
        QrCode
    }

    public class PrintTestData
    {
        public string ProductName { get; set; } = "Producto de prueba";
        public string PriceText { get; set; } = "25.50";
        public string Barcode { get; set; } = "2000000012345";
        public string BarcodeType { get; set; } = "128"; // TSPL Code 128
        public string QrData { get; set; } = "https://comerzia.com/test";
        public string? LogoFilePath { get; set; }
        public Bitmap? LogoImage { get; set; }
        public int LogoHeightMm { get; set; } = 10; // Configurable logo height in mm (0 = auto)
        public CodeDisplayMode CodeMode { get; set; } = CodeDisplayMode.Barcode;

        public bool IncludeLogo { get; set; } = true;
        public bool IncludeName { get; set; } = true;
        public bool IncludePrice { get; set; } = true;
        public bool IncludeBarcode => CodeMode == CodeDisplayMode.Barcode && !string.IsNullOrWhiteSpace(Barcode);
        public bool IncludeQr => CodeMode == CodeDisplayMode.QrCode && !string.IsNullOrWhiteSpace(QrData);
    }

    public class AppConnectionSettings
    {
        public string IpAddress { get; set; } = "192.168.1.100";
        public int Port { get; set; } = 9100;
        public int WidthMm { get; set; } = 50;
        public int HeightMm { get; set; } = 30;
        public int GapMm { get; set; } = 2;
        public int Quantity { get; set; } = 1;
        public int LogoHeightMm { get; set; } = 10;
        public string CodeMode { get; set; } = "Barcode";
        public string ProductName { get; set; } = "Producto de prueba";
        public string PriceText { get; set; } = "25.50";
        public string Barcode { get; set; } = "2000000012345";
        public string BarcodeType { get; set; } = "CODE128";
        public string QrData { get; set; } = "https://comerzia.com/test";
        public string? LogoFilePath { get; set; }
        public string LastRawTspl { get; set; } = "";
    }
}
