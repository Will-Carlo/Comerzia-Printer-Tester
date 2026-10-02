using System;

namespace ComerziaPrinterTester.Utils
{
    public static class PriceHelper
    {
        /// <summary>
        /// Parses the raw price text into (Prefix: "BS", Amount: "25.50").
        /// If the input is empty or contains no numeric/amount content, returns ("", "").
        /// Automatically strips redundant "Bs.", "BS", "bs", etc. from the amount.
        /// </summary>
        public static (string Prefix, string Amount) ParsePrice(string? rawPrice)
        {
            if (string.IsNullOrWhiteSpace(rawPrice))
            {
                return ("", "");
            }

            string trimmed = rawPrice.Trim();
            string amount = trimmed;

            if (amount.StartsWith("bs.", StringComparison.OrdinalIgnoreCase))
            {
                amount = amount.Substring(3).Trim();
            }
            else if (amount.StartsWith("bs", StringComparison.OrdinalIgnoreCase))
            {
                amount = amount.Substring(2).Trim();
            }
            else if (amount.StartsWith("b$", StringComparison.OrdinalIgnoreCase))
            {
                amount = amount.Substring(2).Trim();
            }
            else if (amount.StartsWith("$"))
            {
                amount = amount.Substring(1).Trim();
            }

            if (string.IsNullOrWhiteSpace(amount))
            {
                return ("", "");
            }

            return ("BS", amount);
        }
    }
}
