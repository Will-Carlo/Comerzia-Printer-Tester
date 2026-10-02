namespace ComerziaPrinterTester
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.SuspendLayout();

            // Main Form Settings
            this.ClientSize = new System.Drawing.Size(1080, 780);
            this.MinimumSize = new System.Drawing.Size(1000, 720);
            this.Text = "Comerzia Printer Tester - PhiloPOS 320B (TSPL)";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);

            this.ResumeLayout(false);
        }

        #endregion
    }
}
