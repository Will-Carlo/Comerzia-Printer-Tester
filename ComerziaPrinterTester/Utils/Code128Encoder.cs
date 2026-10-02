using System.Drawing;
using System.Text;

namespace ComerziaPrinterTester.Utils
{
    /// <summary>
    /// Encodes alphanumeric strings into authentic GS1-128 / Code 128 (Code Set B) pattern.
    /// Generates standard scannable 1D barcodes.
    /// </summary>
    public static class Code128Encoder
    {
        // Code 128 Patterns (107 patterns: 11-bit bars/spaces each, Stop is 13-bit)
        private static readonly string[] Patterns = new string[]
        {
            "212222", "222122", "222221", "121223", "121322", "131222", "122213", "122312", "132212", "221213", // 0-9
            "221312", "231212", "112232", "122132", "122231", "113222", "123122", "123221", "223211", "221132", // 10-19
            "221231", "213212", "223112", "312131", "311222", "321122", "321221", "312212", "322112", "322211", // 20-29
            "212123", "212321", "232121", "111323", "131123", "131321", "112313", "132113", "132311", "211313", // 30-39
            "231113", "231311", "112133", "112331", "132131", "113123", "113321", "133121", "313121", "211331", // 40-49
            "231131", "213113", "213311", "213131", "311123", "311321", "331121", "312113", "312311", "332111", // 50-59
            "314111", "221411", "431111", "111224", "111422", "121124", "121421", "141122", "141221", "112214", // 60-69
            "112412", "122114", "122411", "142112", "142211", "241211", "221114", "413111", "241112", "134111", // 70-79
            "111242", "121142", "121241", "114212", "124112", "124211", "411212", "421112", "421211", "212141", // 80-89
            "214121", "412121", "111143", "111341", "131141", "114113", "114311", "411113", "411311", "113141", // 90-99
            "114131", "311141", "411131", "211412", "211214", "211232", "2331112"                                 // 100-106 (106 = Stop)
        };

        private const int START_CODE_B = 104;
        private const int STOP_CODE = 106;

        public static Bitmap RenderScannableCode128(string text, int targetWidth, int targetHeight)
        {
            if (string.IsNullOrEmpty(text)) text = "0000";

            // Build symbols using Code Set B (ASCII 32 to 127)
            var symbols = new List<int> { START_CODE_B };
            int checksum = START_CODE_B;

            for (int i = 0; i < text.Length; i++)
            {
                int val = text[i] - 32;
                if (val < 0 || val > 95) val = 0; // clamp fallback
                symbols.Add(val);
                checksum += val * (i + 1);
            }

            checksum %= 103;
            symbols.Add(checksum);
            symbols.Add(STOP_CODE);

            // Convert to module pattern (1 = bar, 0 = space)
            var modules = new StringBuilder();
            foreach (var sym in symbols)
            {
                string pat = Patterns[sym];
                bool isBar = true;
                foreach (char c in pat)
                {
                    int runLength = c - '0';
                    modules.Append(isBar ? new string('1', runLength) : new string('0', runLength));
                    isBar = !isBar;
                }
            }

            int moduleCount = modules.Length;
            int bmpWidth = Math.Max(moduleCount * 2, targetWidth);
            float moduleWidth = (float)bmpWidth / moduleCount;

            var bmp = new Bitmap(bmpWidth, targetHeight);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.White);
                using var barBrush = new SolidBrush(Color.Black);

                for (int i = 0; i < moduleCount; i++)
                {
                    if (modules[i] == '1')
                    {
                        float x = i * moduleWidth;
                        g.FillRectangle(barBrush, x, 0, (float)Math.Ceiling(moduleWidth), targetHeight);
                    }
                }
            }

            return bmp;
        }
    }
}
