using System.Text;

namespace ComerziaPrinterTester;

static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
        try
        {
            // Register CodePages provider so windows-1252 / ISO-8859-1 encodings work in .NET Core / .NET 8
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                MessageBox.Show($"Error no controlado: {e.ExceptionObject}", "Error Crítico", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };

            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al iniciar Comerzia Printer Tester:\n\n{ex.Message}\n\nDetalles:\n{ex.StackTrace}", "Error al Iniciar", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }    
}