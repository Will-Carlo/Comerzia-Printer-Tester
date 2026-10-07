using System.Drawing;
using ComerziaPrinterTester.Models;
using ComerziaPrinterTester.Services;
using ComerziaPrinterTester.TSPL;
using ComerziaPrinterTester.Utils;

namespace ComerziaPrinterTester
{
    public partial class MainForm : Form
    {
        private readonly PrinterTestService _printerService;
        private AppConnectionSettings _settings;
        private Bitmap? _loadedLogo;
        private IQrGenerator _currentQrGenerator = new NativeTsplQrGenerator();

        // UI Controls
        private TextBox txtIp = null!;
        private NumericUpDown numPort = null!;
        private Button btnTestConnection = null!;
        private Button btnScanNetwork = null!;
        private Label lblConnectionStatus = null!;

        private NumericUpDown numWidth = null!;
        private NumericUpDown numHeight = null!;
        private NumericUpDown numGap = null!;
        private NumericUpDown numQuantity = null!;
        private ComboBox cmbPresets = null!;

        private TextBox txtProductName = null!;
        private TextBox txtPrice = null!;
        private RadioButton rbBarcode = null!;
        private RadioButton rbQr = null!;
        private TextBox txtBarcode = null!;
        private ComboBox cmbBarcodeType = null!;
        private TextBox txtQrData = null!;
        private ComboBox cmbQrMode = null!;
        private Button btnSelectLogo = null!;
        private Button btnClearLogo = null!;
        private NumericUpDown numLogoHeight = null!;
        private Label lblLogoPath = null!;

        private PictureBox picPreview = null!;

        private Button btnGenerateTspl = null!;
        private Button btnPrintTest = null!;
        private Button btnPrint1 = null!;
        private Button btnPrint5 = null!;
        private Button btnPrint10 = null!;
        private Button btnPrintTestSheet = null!;

        private TextBox txtTsplCommands = null!;
        private TextBox txtRawTspl = null!;
        private Button btnSendRawTspl = null!;

        private TextBox txtLog = null!;
        private Button btnClearLog = null!;

        public MainForm()
        {
            InitializeComponent();
            LoadAppIcon();
            _printerService = new PrinterTestService();
            _settings = ConfigStorage.LoadSettings();

            BuildCustomUi();
            LoadSettingsToUi();
            UpdatePreview();
        }

        private void LoadAppIcon()
        {
            try
            {
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "printer-icon.ico");
                if (File.Exists(iconPath))
                {
                    this.Icon = new Icon(iconPath);
                }
                else
                {
                    string? exePath = Environment.ProcessPath;
                    if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                    {
                        var extracted = Icon.ExtractAssociatedIcon(exePath);
                        if (extracted != null)
                        {
                            this.Icon = extracted;
                        }
                    }
                }
            }
            catch
            {
                // Fallback gracefully
            }
        }

        private void BuildCustomUi()
        {
            this.Text = "Comerzia Printer Tester";
            this.BackColor = Color.FromArgb(244, 246, 249);

            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                Padding = new Padding(12),
                AutoScroll = true
            };
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52F));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 64F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 36F));
            this.Controls.Add(mainLayout);

            // Left Side: Config & Controls (Scrollable panel)
            var leftPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Padding = new Padding(0, 0, 10, 0)
            };

            int curY = 0;

            // 1. Connection Group
            var grpConn = CreateGroupBox("1. Conexión TCP/IP", ref curY, 95, leftPanel);
            
            var lblIp = new Label { Text = "IP Impresora:", Location = new Point(15, 26), AutoSize = true };
            txtIp = new TextBox { Text = "192.168.1.100", Location = new Point(98, 23), Width = 112, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };

            btnScanNetwork = new Button
            {
                Text = "🔍 Buscar",
                Location = new Point(215, 21),
                Size = new Size(82, 29),
                BackColor = Color.FromArgb(40, 167, 69),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnScanNetwork.FlatAppearance.BorderSize = 0;
            btnScanNetwork.Click += async (s, e) => await OnScanNetworkClicked();
            
            var lblPort = new Label { Text = "Puerto:", Location = new Point(305, 26), AutoSize = true };
            numPort = new NumericUpDown { Minimum = 1, Maximum = 65535, Value = 9100, Location = new Point(353, 23), Width = 58 };

            btnTestConnection = new Button
            {
                Text = "🔌 Probar",
                Location = new Point(418, 21),
                Size = new Size(95, 29),
                BackColor = Color.FromArgb(0, 122, 255),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnTestConnection.FlatAppearance.BorderSize = 0;
            btnTestConnection.Click += async (s, e) => await OnTestConnectionClicked();

            lblConnectionStatus = new Label
            {
                Text = "⚪ Estado: Sin probar",
                Location = new Point(15, 60),
                AutoSize = true,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(70, 70, 70)
            };

            grpConn.Controls.AddRange(new Control[] { lblIp, txtIp, btnScanNetwork, lblPort, numPort, btnTestConnection, lblConnectionStatus });

            // 2. Label Configuration Group
            var grpConfig = CreateGroupBox("2. Configuración de Etiqueta & Tamaño", ref curY, 110, leftPanel);

            var lblPreset = new Label { Text = "Medidas:", Location = new Point(15, 26), AutoSize = true };
            cmbPresets = new ComboBox
            {
                Location = new Point(85, 23),
                Width = 175,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbPresets.Items.AddRange(new object[] {
                "32 x 12 mm",
                "30 x 20 mm",
                "30 x 30 mm",
                "31 x 57 mm",
                "38 x 62 mm",
                "40 x 30 mm",
                "50 x 25 mm",
                "50 x 30 mm",
                "50 x 50 mm",
                "76 x 25 mm",
                "76 x 38 mm",
                "76 x 50 mm",
                "76 x 76 mm",
                "100 x 50 mm",
                "100 x 76 mm",
                "100 x 100 mm",
                "100 x 152 mm",
                "Personalizado"
            });
            cmbPresets.SelectedIndex = 7; // Default 50 x 30 mm
            cmbPresets.SelectedIndexChanged += OnPresetChanged;

            var lblW = new Label { Text = "Ancho (mm):", Location = new Point(15, 65), AutoSize = true };
            numWidth = new NumericUpDown { Minimum = 15, Maximum = 160, Value = 50, Location = new Point(95, 63), Width = 55 };
            numWidth.ValueChanged += (s, e) => UpdatePreview();

            var lblH = new Label { Text = "Alto (mm):", Location = new Point(160, 65), AutoSize = true };
            numHeight = new NumericUpDown { Minimum = 10, Maximum = 200, Value = 30, Location = new Point(225, 63), Width = 55 };
            numHeight.ValueChanged += (s, e) => UpdatePreview();

            var lblGap = new Label { Text = "GAP (mm):", Location = new Point(290, 65), AutoSize = true };
            numGap = new NumericUpDown { Minimum = 0, Maximum = 10, Value = 2, Location = new Point(360, 63), Width = 50 };

            var lblQty = new Label { Text = "Cant:", Location = new Point(425, 65), AutoSize = true };
            numQuantity = new NumericUpDown { Minimum = 1, Maximum = 100, Value = 1, Location = new Point(465, 63), Width = 50 };

            grpConfig.Controls.AddRange(new Control[] { lblPreset, cmbPresets, lblW, numWidth, lblH, numHeight, lblGap, numGap, lblQty, numQuantity });

            // 3. Test Content Group
            var grpContent = CreateGroupBox("3. Contenido de la Etiqueta", ref curY, 235, leftPanel);

            var lblPrice = new Label { Text = "Precio (Izq):", Location = new Point(15, 25), AutoSize = true };
            txtPrice = new TextBox { Text = "25.50", Location = new Point(105, 22), Width = 135 };
            txtPrice.TextChanged += (s, e) => UpdatePreview();

            var lblLogo = new Label { Text = "Logo (Der):", Location = new Point(255, 25), AutoSize = true };
            btnSelectLogo = new Button { Text = "📁 Cargar", Location = new Point(325, 20), Size = new Size(65, 27) };
            btnSelectLogo.Click += OnSelectLogoClicked;

            btnClearLogo = new Button { Text = "❌ Quitar", Location = new Point(395, 20), Size = new Size(60, 27) };
            btnClearLogo.Click += (s, e) => {
                _loadedLogo?.Dispose();
                _loadedLogo = null;
                _settings.LogoFilePath = null;
                lblLogoPath.Text = "Sin logo";
                UpdatePreview();
            };

            numLogoHeight = new NumericUpDown { Minimum = 4, Maximum = 60, Value = 10, Location = new Point(460, 22), Width = 45 };
            numLogoHeight.ValueChanged += (s, e) => UpdatePreview();

            lblLogoPath = new Label { Text = "Sin logo", Location = new Point(325, 48), AutoSize = true, ForeColor = Color.Gray, Font = new Font("Segoe UI", 8f) };

            var lblProd = new Label { Text = "Nombre:", Location = new Point(15, 68), AutoSize = true };
            txtProductName = new TextBox { Text = "Producto de prueba", Location = new Point(105, 65), Width = 400 };
            txtProductName.TextChanged += (s, e) => UpdatePreview();

            // Switch Barcode vs QR
            var lblCodeType = new Label { Text = "Código:", Location = new Point(15, 102), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
            rbBarcode = new RadioButton { Text = "Código de barras", Location = new Point(105, 100), AutoSize = true, Checked = true };
            rbQr = new RadioButton { Text = "Código QR", Location = new Point(245, 100), AutoSize = true };
            rbBarcode.CheckedChanged += OnCodeModeChanged;
            rbQr.CheckedChanged += OnCodeModeChanged;

            // Barcode inputs
            var lblBc = new Label { Text = "Valor Barcode:", Location = new Point(15, 133), AutoSize = true };
            txtBarcode = new TextBox { Text = "2000000012345", Location = new Point(105, 130), Width = 230 };
            txtBarcode.TextChanged += (s, e) => UpdatePreview();

            var lblBcType = new Label { Text = "Tipo:", Location = new Point(345, 133), AutoSize = true };
            cmbBarcodeType = new ComboBox { Location = new Point(385, 130), Width = 120, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbBarcodeType.Items.AddRange(new object[] { "CODE128", "EAN13", "CODE39" });
            cmbBarcodeType.SelectedIndex = 0;
            cmbBarcodeType.SelectedIndexChanged += (s, e) => UpdatePreview();

            // QR inputs
            var lblQr = new Label { Text = "Valor QR:", Location = new Point(15, 168), AutoSize = true };
            txtQrData = new TextBox { Text = "https://comerzia.com/test", Location = new Point(105, 165), Width = 260 };
            txtQrData.TextChanged += (s, e) => UpdatePreview();

            var lblQrMode = new Label { Text = "Motor:", Location = new Point(375, 168), AutoSize = true };
            cmbQrMode = new ComboBox { Location = new Point(420, 165), Width = 85, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbQrMode.Items.AddRange(new object[] { "TSPL", "Bitmap" });
            cmbQrMode.SelectedIndex = 0;
            cmbQrMode.SelectedIndexChanged += (s, e) => {
                _currentQrGenerator = cmbQrMode.SelectedIndex == 0 ? new NativeTsplQrGenerator() : new BitmapQrGenerator();
                UpdatePreview();
            };

            grpContent.Controls.AddRange(new Control[] {
                lblPrice, txtPrice,
                lblLogo, btnSelectLogo, btnClearLogo, numLogoHeight, lblLogoPath,
                lblProd, txtProductName,
                lblCodeType, rbBarcode, rbQr,
                lblBc, txtBarcode, lblBcType, cmbBarcodeType,
                lblQr, txtQrData, lblQrMode, cmbQrMode
            });

            // 4. Action Buttons Group
            var grpActions = CreateGroupBox("4. Acciones de Impresión Directa", ref curY, 120, leftPanel);

            btnGenerateTspl = CreateStyledButton("⚙️ Ver comandos TSPL", 15, 25, 160, 34, Color.FromArgb(108, 117, 125));
            btnGenerateTspl.Click += (s, e) => OnGenerateTsplClicked();

            btnPrintTest = CreateStyledButton("🖨️ Imprimir prueba", 185, 25, 155, 34, Color.FromArgb(40, 167, 69));
            btnPrintTest.Click += async (s, e) => await OnPrintClicked((int)numQuantity.Value);

            btnPrintTestSheet = CreateStyledButton("📑 IMPRIMIR HOJA DE PRUEBA", 350, 25, 165, 34, Color.FromArgb(111, 66, 193));
            btnPrintTestSheet.Click += async (s, e) => await OnPrintTestSheetClicked();

            btnPrint1 = CreateStyledButton("Imprimir 1", 15, 68, 110, 32, Color.FromArgb(23, 162, 184));
            btnPrint1.Click += async (s, e) => await OnPrintClicked(1);

            btnPrint5 = CreateStyledButton("Imprimir 5", 135, 68, 110, 32, Color.FromArgb(23, 162, 184));
            btnPrint5.Click += async (s, e) => await OnPrintClicked(5);

            btnPrint10 = CreateStyledButton("Imprimir 10", 255, 68, 110, 32, Color.FromArgb(23, 162, 184));
            btnPrint10.Click += async (s, e) => await OnPrintClicked(10);

            grpActions.Controls.AddRange(new Control[] { btnGenerateTspl, btnPrintTest, btnPrintTestSheet, btnPrint1, btnPrint5, btnPrint10 });

            mainLayout.Controls.Add(leftPanel, 0, 0);

            // Right Side Top: Preview & Raw TSPL Tab Control
            var tabControl = new TabControl { Dock = DockStyle.Fill };

            // Tab 1: Preview & Generated TSPL
            var tabPreview = new TabPage { Text = "👁️ Previsualización & Comandos", Padding = new Padding(8) };
            var previewSplit = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 230 };
            
            var pnlPic = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            picPreview = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.CenterImage,
                BackColor = Color.FromArgb(240, 242, 245),
                BorderStyle = BorderStyle.FixedSingle
            };
            picPreview.SizeChanged += (s, e) => UpdatePreview();
            this.Shown += (s, e) => UpdatePreview();
            pnlPic.Controls.Add(picPreview);

            var pnlTsplInspect = new Panel { Dock = DockStyle.Fill };
            var lblInspectTitle = new Label { Text = "Comandos TSPL que se enviarán a la impresora:", Dock = DockStyle.Top, Height = 22, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
            txtTsplCommands = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Both,
                Font = new Font("Consolas", 9.5f),
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.FromArgb(220, 220, 220),
                ReadOnly = true
            };
            pnlTsplInspect.Controls.Add(txtTsplCommands);
            pnlTsplInspect.Controls.Add(lblInspectTitle);

            previewSplit.Panel1.Controls.Add(pnlPic);
            previewSplit.Panel2.Controls.Add(pnlTsplInspect);
            tabPreview.Controls.Add(previewSplit);

            // Tab 2: Raw TSPL manual editor
            var tabRaw = new TabPage { Text = "⌨️ TSPL RAW (Avanzado)", Padding = new Padding(8) };
            var pnlRaw = new Panel { Dock = DockStyle.Fill };
            var lblRawDesc = new Label
            {
                Text = "Escriba o modifique comandos TSPL directamente para probar con la impresora:",
                Dock = DockStyle.Top,
                Height = 25,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            };
            
            txtRawTspl = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Both,
                Font = new Font("Consolas", 10f),
                BackColor = Color.FromArgb(25, 25, 30),
                ForeColor = Color.FromArgb(80, 250, 123),
                Text = "SIZE 50 mm,30 mm\r\nGAP 2 mm,0 mm\r\nDIRECTION 1\r\nCLS\r\nTEXT 20,20,\"3\",0,1,1,\"PRUEBA RAW TSPL\"\r\nTEXT 20,55,\"4\",0,1,1,\"Bs. 12.00\"\r\nBARCODE 20,95,\"128\",40,1,0,2,2,\"12345678\"\r\nPRINT 1\r\n"
            };

            var pnlRawBottom = new Panel { Dock = DockStyle.Bottom, Height = 45, Padding = new Padding(0, 6, 0, 0) };
            btnSendRawTspl = CreateStyledButton("🚀 ENVIAR TSPL RAW", 0, 6, 180, 34, Color.FromArgb(220, 53, 69));
            btnSendRawTspl.Click += async (s, e) => await OnSendRawTsplClicked();
            pnlRawBottom.Controls.Add(btnSendRawTspl);

            pnlRaw.Controls.Add(txtRawTspl);
            pnlRaw.Controls.Add(pnlRawBottom);
            pnlRaw.Controls.Add(lblRawDesc);
            tabRaw.Controls.Add(pnlRaw);

            tabControl.TabPages.Add(tabPreview);
            tabControl.TabPages.Add(tabRaw);
            mainLayout.Controls.Add(tabControl, 1, 0);

            // Bottom Full-width: Logs Panel
            var grpLog = new GroupBox
            {
                Text = "📋 Registro de Comunicación & Diagnóstico (Log)",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            };
            mainLayout.SetColumnSpan(grpLog, 2);

            txtLog = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Both,
                Font = new Font("Consolas", 9f),
                BackColor = Color.FromArgb(20, 24, 33),
                ForeColor = Color.FromArgb(200, 255, 200),
                ReadOnly = true
            };

            var pnlLogHeader = new Panel { Dock = DockStyle.Top, Height = 28 };
            btnClearLog = new Button { Text = "Limpiar Log", Dock = DockStyle.Right, Width = 90 };
            btnClearLog.Click += (s, e) => txtLog.Clear();
            pnlLogHeader.Controls.Add(btnClearLog);

            grpLog.Controls.Add(txtLog);
            grpLog.Controls.Add(pnlLogHeader);
            mainLayout.Controls.Add(grpLog, 0, 1);

            // Auto-save settings on form closing
            this.FormClosing += (s, e) => SaveSettingsFromUi();
        }

        private GroupBox CreateGroupBox(string title, ref int curY, int height, Control parent)
        {
            var grp = new GroupBox
            {
                Text = title,
                Location = new Point(0, curY),
                Size = new Size(530, height),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(33, 37, 41)
            };
            parent.Controls.Add(grp);
            curY += height + 10;
            return grp;
        }

        private Button CreateStyledButton(string text, int x, int y, int w, int h, Color bg)
        {
            var btn = new Button
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(w, h),
                BackColor = bg,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }

        private void AppendLog(string message)
        {
            if (txtLog == null) return;

            if (txtLog.InvokeRequired)
            {
                txtLog.Invoke(new Action(() => AppendLog(message)));
                return;
            }

            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            txtLog.AppendText($"[{timestamp}] {message}\r\n");
        }

        private LabelConfiguration GetCurrentConfig(int? quantityOverride = null)
        {
            return new LabelConfiguration
            {
                WidthMm = (int)numWidth.Value,
                HeightMm = (int)numHeight.Value,
                GapMm = (int)numGap.Value,
                Quantity = quantityOverride ?? (int)numQuantity.Value,
                Dpi = 203
            };
        }

        private PrintTestData GetCurrentData()
        {
            return new PrintTestData
            {
                ProductName = txtProductName.Text.Trim(),
                PriceText = txtPrice.Text.Trim(),
                Barcode = txtBarcode.Text.Trim(),
                BarcodeType = cmbBarcodeType.SelectedItem?.ToString() ?? "CODE128",
                QrData = txtQrData.Text.Trim(),
                LogoImage = _loadedLogo,
                LogoHeightMm = (int)numLogoHeight.Value,
                CodeMode = rbQr.Checked ? CodeDisplayMode.QrCode : CodeDisplayMode.Barcode,
                IncludeLogo = _loadedLogo != null,
                IncludeName = !string.IsNullOrWhiteSpace(txtProductName.Text),
                IncludePrice = !string.IsNullOrWhiteSpace(txtPrice.Text)
            };
        }

        private void OnCodeModeChanged(object? sender, EventArgs e)
        {
            bool isBarcode = rbBarcode.Checked;
            txtBarcode.Enabled = isBarcode;
            cmbBarcodeType.Enabled = isBarcode;

            txtQrData.Enabled = !isBarcode;
            cmbQrMode.Enabled = !isBarcode;

            UpdatePreview();
        }

        private void UpdatePreview()
        {
            if (picPreview == null || txtTsplCommands == null || numWidth == null || numHeight == null)
            {
                return;
            }

            try
            {
                var config = GetCurrentConfig();
                var data = GetCurrentData();

                int canvasW = Math.Max(380, picPreview.ClientSize.Width > 0 ? picPreview.ClientSize.Width : 380);
                int canvasH = Math.Max(240, picPreview.ClientSize.Height > 0 ? picPreview.ClientSize.Height : 240);

                var previewBmp = LabelPreviewRenderer.RenderPreview(config, data, canvasW, canvasH);
                
                var oldImg = picPreview.Image;
                picPreview.Image = previewBmp;
                oldImg?.Dispose();

                // Update text preview
                txtTsplCommands.Text = _printerService.GenerateTsplPreviewText(config, data, _currentQrGenerator);
            }
            catch (Exception ex)
            {
                AppendLog($"Error al actualizar previsualización: {ex.Message}");
            }
        }

        private void OnPresetChanged(object? sender, EventArgs e)
        {
            switch (cmbPresets.SelectedIndex)
            {
                case 0: numWidth.Value = 32; numHeight.Value = 12; break;  // 32 x 12 mm
                case 1: numWidth.Value = 30; numHeight.Value = 20; break;  // 30 x 20 mm
                case 2: numWidth.Value = 30; numHeight.Value = 30; break;  // 30 x 30 mm
                case 3: numWidth.Value = 31; numHeight.Value = 57; break;  // 31 x 57 mm
                case 4: numWidth.Value = 38; numHeight.Value = 62; break;  // 38 x 62 mm
                case 5: numWidth.Value = 40; numHeight.Value = 30; break;  // 40 x 30 mm
                case 6: numWidth.Value = 50; numHeight.Value = 25; break;  // 50 x 25 mm
                case 7: numWidth.Value = 50; numHeight.Value = 30; break;  // 50 x 30 mm
                case 8: numWidth.Value = 50; numHeight.Value = 50; break;  // 50 x 50 mm
                case 9: numWidth.Value = 76; numHeight.Value = 25; break;  // 76 x 25 mm
                case 10: numWidth.Value = 76; numHeight.Value = 38; break; // 76 x 38 mm
                case 11: numWidth.Value = 76; numHeight.Value = 50; break; // 76 x 50 mm
                case 12: numWidth.Value = 76; numHeight.Value = 76; break; // 76 x 76 mm
                case 13: numWidth.Value = 100; numHeight.Value = 50; break;// 100 x 50 mm
                case 14: numWidth.Value = 100; numHeight.Value = 76; break;// 100 x 76 mm
                case 15: numWidth.Value = 100; numHeight.Value = 100; break;// 100 x 100 mm
                case 16: numWidth.Value = 100; numHeight.Value = 152; break;// 100 x 152 mm
                case 17: break; // Personalizado
            }
            UpdatePreview();
        }

        private void OnSelectLogoClicked(object? sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog
            {
                Title = "Seleccionar Logo para Etiqueta Térmica",
                Filter = "Archivos de imagen (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|Todos los archivos (*.*)|*.*"
            };

            if (ofd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    using (var fs = new FileStream(ofd.FileName, FileMode.Open, FileAccess.Read))
                    using (var temp = Image.FromStream(fs))
                    {
                        _loadedLogo?.Dispose();
                        _loadedLogo = new Bitmap(temp);
                    }
                    _settings.LogoFilePath = ofd.FileName;
                    lblLogoPath.Text = Path.GetFileName(ofd.FileName);
                    AppendLog($"Logo cargado exitosamente: {ofd.FileName} ({_loadedLogo.Width}x{_loadedLogo.Height} px)");
                    UpdatePreview();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al cargar imagen: {ex.Message}", "Error de Logo", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    AppendLog($"ERROR al cargar imagen: {ex.Message}");
                }
            }
        }

        private async Task OnTestConnectionClicked()
        {
            string ip = txtIp.Text.Trim();
            int port = (int)numPort.Value;

            AppendLog($"Intentando conectar a {ip}:{port}...");
            lblConnectionStatus.Text = "🟡 Probando conexión...";
            lblConnectionStatus.ForeColor = Color.DarkOrange;
            btnTestConnection.Enabled = false;

            var result = await _printerService.TestConnectionAsync(ip, port);
            btnTestConnection.Enabled = true;

            if (result.Success)
            {
                lblConnectionStatus.Text = $"🟢 Conectado ({result.ElapsedMs} ms)";
                lblConnectionStatus.ForeColor = Color.FromArgb(40, 167, 69);
                AppendLog($"TCP Conectado exitosamente en {result.ElapsedMs} ms.");
            }
            else
            {
                lblConnectionStatus.Text = "🔴 Error de conexión";
                lblConnectionStatus.ForeColor = Color.FromArgb(220, 53, 69);
                AppendLog($"ERROR: {result.Message}");
                MessageBox.Show(result.Message, "Error de Conexión", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private async Task OnScanNetworkClicked()
        {
            btnScanNetwork.Enabled = false;
            btnTestConnection.Enabled = false;
            lblConnectionStatus.Text = "🔍 Buscando en red local...";
            lblConnectionStatus.ForeColor = Color.FromArgb(0, 102, 204);
            AppendLog("Iniciando búsqueda automática de impresoras térmicas en la red local (puerto 9100)...");

            try
            {
                var prefixes = NetworkScannerService.GetLocalSubnetPrefixes();
                List<NetworkScannerService.DiscoveredPrinter> allPrinters = new();

                foreach (var prefix in prefixes)
                {
                    AppendLog($"Escaneando rango {prefix}1 al {prefix}254...");
                    var found = await NetworkScannerService.ScanSubnetAsync(
                        prefix,
                        port: (int)numPort.Value,
                        timeoutMs: 300,
                        progressCallback: (done, total) =>
                        {
                            if (this.IsHandleCreated)
                            {
                                this.BeginInvoke(() =>
                                {
                                    lblConnectionStatus.Text = $"🔍 Escaneando {prefix}* ({done}/{total})...";
                                });
                            }
                        });

                    allPrinters.AddRange(found);
                    if (allPrinters.Count > 0)
                    {
                        break;
                    }
                }

                if (allPrinters.Count > 0)
                {
                    var best = allPrinters.First();
                    txtIp.Text = best.IpAddress;
                    numPort.Value = best.Port;
                    lblConnectionStatus.Text = $"🟢 Impresora detectada en {best.IpAddress}:{best.Port} ({best.LatencyMs}ms)";
                    lblConnectionStatus.ForeColor = Color.FromArgb(40, 167, 69);
                    AppendLog($"¡ÉXITO! Impresora detectada en {best.IpAddress}:{best.Port} con respuesta en {best.LatencyMs}ms.");
                    SaveSettingsFromUi();
                    System.Media.SystemSounds.Asterisk.Play();
                }
                else
                {
                    lblConnectionStatus.Text = "⚠️ No se detectó impresora en puerto 9100";
                    lblConnectionStatus.ForeColor = Color.FromArgb(200, 100, 0);
                    AppendLog("No se encontró ningún dispositivo respondiendo en el puerto 9100 en la red local.");
                    AppendLog("💡 SUGERENCIA IMPORTANTE: Para saber la IP actual de tu impresora física, apágala, mantén presionado el botón FEED y enciéndela sin soltar FEED por 3 segundos hasta que imprima la hoja de 'Self-Test' (ahí verás su IP exacta).");
                    MessageBox.Show(
                        "No se detectó ninguna impresora con el puerto 9100 abierto en la subred local actual.\n\n" +
                        "💡 Cómo obtener la IP exacta físicamente:\n" +
                        "1. Apaga la impresora.\n" +
                        "2. Mantén presionado el botón FEED.\n" +
                        "3. Enciéndela SIN soltar FEED durante 3 segundos hasta que suene o empiece a imprimir.\n" +
                        "4. Se imprimirá un ticket de prueba (Self-Test) con la dirección IP que tiene asignada.",
                        "Impresora no detectada",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                lblConnectionStatus.Text = "❌ Error durante el escaneo";
                lblConnectionStatus.ForeColor = Color.Red;
                AppendLog($"Error al escanear la red: {ex.Message}");
            }
            finally
            {
                btnScanNetwork.Enabled = true;
                btnTestConnection.Enabled = true;
            }
        }

        private void OnGenerateTsplClicked()
        {
            var config = GetCurrentConfig();
            var data = GetCurrentData();
            txtTsplCommands.Text = _printerService.GenerateTsplPreviewText(config, data, _currentQrGenerator);
            AppendLog("Comandos TSPL actualizados en visor.");
        }

        private async Task OnPrintClicked(int quantity)
        {
            string ip = txtIp.Text.Trim();
            int port = (int)numPort.Value;
            var config = GetCurrentConfig(quantity);
            var data = GetCurrentData();

            AppendLog($"Generando TSPL para {quantity} etiqueta(s)...");
            var result = await _printerService.PrintLabelAsync(ip, port, config, data, _currentQrGenerator, AppendLog);

            if (result.Success)
            {
                AppendLog($"Impresión enviada correctamente: {result.Message}");
            }
            else
            {
                MessageBox.Show(result.Message, "Error de Impresión", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task OnPrintTestSheetClicked()
        {
            string ip = txtIp.Text.Trim();
            int port = (int)numPort.Value;
            var config = GetCurrentConfig();
            var data = GetCurrentData();

            var dialog = MessageBox.Show(
                "¿Desea imprimir la hoja de prueba completa (4 etiquetas con diferentes combinaciones)?",
                "Confirmar Hoja de Prueba",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );

            if (dialog != DialogResult.Yes) return;

            var result = await _printerService.PrintTestSheetAsync(ip, port, config, data, _currentQrGenerator, AppendLog);
            if (result.Success)
            {
                MessageBox.Show(result.Message, "Hoja de Prueba Enviada", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show(result.Message, "Error en Hoja de Prueba", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task OnSendRawTsplClicked()
        {
            string ip = txtIp.Text.Trim();
            int port = (int)numPort.Value;
            string raw = txtRawTspl.Text.Trim();

            if (string.IsNullOrWhiteSpace(raw))
            {
                MessageBox.Show("El cuadro de comandos TSPL RAW está vacío.", "Atención", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            AppendLog($"Enviando script TSPL RAW personalizado...");
            var result = await _printerService.SendRawTsplAsync(ip, port, raw, AppendLog);

            if (result.Success)
            {
                AppendLog($"TSPL RAW transmitido con éxito.");
            }
            else
            {
                MessageBox.Show(result.Message, "Error enviando TSPL RAW", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LoadSettingsToUi()
        {
            txtIp.Text = _settings.IpAddress;
            numPort.Value = _settings.Port;
            numWidth.Value = Math.Max(15, _settings.WidthMm);
            numHeight.Value = Math.Max(15, _settings.HeightMm);
            numGap.Value = Math.Max(0, _settings.GapMm);
            numQuantity.Value = Math.Max(1, _settings.Quantity);
            numLogoHeight.Value = Math.Clamp(_settings.LogoHeightMm > 0 ? _settings.LogoHeightMm : 12, (int)numLogoHeight.Minimum, (int)numLogoHeight.Maximum);
            txtProductName.Text = _settings.ProductName;
            txtPrice.Text = _settings.PriceText;
            txtBarcode.Text = _settings.Barcode;
            txtQrData.Text = _settings.QrData;

            if (_settings.CodeMode == "QrCode")
            {
                rbQr.Checked = true;
            }
            else
            {
                rbBarcode.Checked = true;
            }
            OnCodeModeChanged(null, EventArgs.Empty);

            if (!string.IsNullOrEmpty(_settings.LastRawTspl))
            {
                txtRawTspl.Text = _settings.LastRawTspl;
            }

            if (!string.IsNullOrEmpty(_settings.LogoFilePath) && File.Exists(_settings.LogoFilePath))
            {
                try
                {
                    using (var fs = new FileStream(_settings.LogoFilePath, FileMode.Open, FileAccess.Read))
                    using (var temp = Image.FromStream(fs))
                    {
                        _loadedLogo?.Dispose();
                        _loadedLogo = new Bitmap(temp);
                    }
                    lblLogoPath.Text = Path.GetFileName(_settings.LogoFilePath);
                }
                catch { }
            }
        }

        private void SaveSettingsFromUi()
        {
            _settings.IpAddress = txtIp.Text.Trim();
            _settings.Port = (int)numPort.Value;
            _settings.WidthMm = (int)numWidth.Value;
            _settings.HeightMm = (int)numHeight.Value;
            _settings.GapMm = (int)numGap.Value;
            _settings.Quantity = (int)numQuantity.Value;
            _settings.LogoHeightMm = (int)numLogoHeight.Value;
            _settings.LogoFilePath = _settings.LogoFilePath;
            _settings.CodeMode = rbQr.Checked ? "QrCode" : "Barcode";
            _settings.ProductName = txtProductName.Text.Trim();
            _settings.PriceText = txtPrice.Text.Trim();
            _settings.Barcode = txtBarcode.Text.Trim();
            _settings.QrData = txtQrData.Text.Trim();
            _settings.LastRawTspl = txtRawTspl.Text;

            ConfigStorage.SaveSettings(_settings);
        }
    }
}
