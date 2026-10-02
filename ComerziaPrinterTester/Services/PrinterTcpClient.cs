using System.Net.Sockets;

namespace ComerziaPrinterTester.Services
{
    public class PrinterTcpClient
    {
        public int TimeoutMilliseconds { get; set; } = 4000;

        /// <summary>
        /// Tests a simple TCP connection to the printer without sending payload.
        /// </summary>
        public async Task<(bool Success, string Message, long ElapsedMs)> TestConnectionAsync(string ip, int port)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                using var client = new TcpClient();
                using var cts = new CancellationTokenSource(TimeoutMilliseconds);

                var connectTask = client.ConnectAsync(ip, port);
                var completedTask = await Task.WhenAny(connectTask, Task.Delay(TimeoutMilliseconds, cts.Token));

                if (completedTask != connectTask)
                {
                    return (false, $"Timeout ({TimeoutMilliseconds}ms) al intentar conectar con {ip}:{port}. Verifique si la impresora está encendida y conectada a la red.", sw.ElapsedMilliseconds);
                }

                await connectTask; // Propagates any SocketException

                if (client.Connected)
                {
                    sw.Stop();
                    return (true, $"Conexión TCP exitosa con {ip}:{port} en {sw.ElapsedMilliseconds} ms.", sw.ElapsedMilliseconds);
                }
                else
                {
                    return (false, $"No se pudo establecer conexión TCP con {ip}:{port}.", sw.ElapsedMilliseconds);
                }
            }
            catch (SocketException ex)
            {
                sw.Stop();
                return (false, $"Error de socket ({ex.SocketErrorCode}): {ex.Message}", sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                sw.Stop();
                return (false, $"Error inesperado: {ex.Message}", sw.ElapsedMilliseconds);
            }
        }

        /// <summary>
        /// Connects to the printer via TCP, transmits raw byte array (TSPL commands), flushes, and closes connection cleanly.
        /// </summary>
        public async Task<(bool Success, string Message, int BytesSent)> SendBytesAsync(string ip, int port, byte[] data, Action<string>? logAction = null)
        {
            if (data == null || data.Length == 0)
            {
                return (false, "No hay datos para enviar a la impresora.", 0);
            }

            try
            {
                logAction?.Invoke($"Iniciando conexión TCP a {ip}:{port}...");
                using var client = new TcpClient();
                
                using var cts = new CancellationTokenSource(TimeoutMilliseconds);
                var connectTask = client.ConnectAsync(ip, port);
                var completedTask = await Task.WhenAny(connectTask, Task.Delay(TimeoutMilliseconds, cts.Token));

                if (completedTask != connectTask)
                {
                    throw new TimeoutException($"Tiempo de espera agotado ({TimeoutMilliseconds}ms) al conectar a {ip}:{port}.");
                }

                await connectTask;

                logAction?.Invoke($"Conectado exitosamente. Transmitiendo {data.Length} bytes...");
                using NetworkStream stream = client.GetStream();
                stream.WriteTimeout = TimeoutMilliseconds;
                stream.ReadTimeout = TimeoutMilliseconds;

                await stream.WriteAsync(data, 0, data.Length);
                await stream.FlushAsync();

                logAction?.Invoke($"Datos enviados y buffer vaciado (Flush) correctamente.");
                
                // Gracefully close
                client.Close();
                logAction?.Invoke($"Conexión TCP cerrada.");

                return (true, $"Se enviaron exitosamente {data.Length} bytes a {ip}:{port}.", data.Length);
            }
            catch (SocketException ex)
            {
                string msg = $"Error de red ({ex.SocketErrorCode}): {ex.Message}";
                logAction?.Invoke($"ERROR: {msg}");
                return (false, msg, 0);
            }
            catch (Exception ex)
            {
                string msg = $"Error al enviar datos: {ex.Message}";
                logAction?.Invoke($"ERROR: {msg}");
                return (false, msg, 0);
            }
        }
    }
}
