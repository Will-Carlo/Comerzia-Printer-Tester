namespace ComerziaPrinterTester.Models
{
    public class LabelConfiguration
    {
        public int WidthMm { get; set; } = 50;
        public int HeightMm { get; set; } = 30;
        public int GapMm { get; set; } = 2;
        public int Quantity { get; set; } = 1;
        public int Dpi { get; set; } = 203;

        // 203 DPI = ~8 dots per mm (203 / 25.4 = 7.992)
        public int WidthDots => (int)Math.Round(WidthMm * (Dpi / 25.4));
        public int HeightDots => (int)Math.Round(HeightMm * (Dpi / 25.4));
        public int GapDots => (int)Math.Round(GapMm * (Dpi / 25.4));
    }
}
