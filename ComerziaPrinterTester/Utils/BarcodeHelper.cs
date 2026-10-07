namespace ComerziaPrinterTester.Utils
{
    public static class BarcodeHelper
    {
        /// <summary>
        /// Accurately computes the number of modules (unit dots) that TSPL firmware
        /// generates for the specified barcode and symbology.
        /// </summary>
        public static int CalculateBarcodeModules(string barcode, string barcodeType)
        {
            if (string.IsNullOrWhiteSpace(barcode)) return 60;

            string type = barcodeType.ToUpper();
            if (type.Contains("EAN13") || type == "EAN-13")
            {
                // Standard EAN-13: 3 guard + 42 left + 5 center guard + 42 right + 3 guard = 95 modules
                return 95;
            }
            else if (type.Contains("39"))
            {
                // Code 39: 9 elements per char (3 wide, 6 narrow) + 1 space = ~15 modules per char
                return (barcode.Length + 2) * 15;
            }
            else
            {
                // TSPL Auto Code 128:
                // Consecutive pairs of digits are encoded using Subset C (1 symbol per pair).
                // Alphanumeric characters are encoded using Subset B (1 symbol per char).
                int symbols = 0;
                int i = 0;
                while (i < barcode.Length)
                {
                    if (i + 1 < barcode.Length && char.IsDigit(barcode[i]) && char.IsDigit(barcode[i + 1]))
                    {
                        symbols++;
                        i += 2;
                    }
                    else
                    {
                        symbols++;
                        i++;
                    }
                }

                // Start symbol (11) + Checksum symbol (11) + Stop symbol (13) + Termination bar (2) = 37 modules
                // Plus symbols * 11 modules
                return (symbols * 11) + 37;
            }
        }
    }
}
