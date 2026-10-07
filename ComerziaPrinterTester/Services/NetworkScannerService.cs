using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace ComerziaPrinterTester.Services
{
    public class NetworkScannerService
    {
        public class DiscoveredPrinter
        {
            public string IpAddress { get; set; } = string.Empty;
            public int Port { get; set; } = 9100;
            public long LatencyMs { get; set; }
        }

        /// <summary>
        /// Detects active IPv4 network interface subnet prefixes (e.g. "192.168.0.", "192.168.1.").
        /// </summary>
        public static List<string> GetLocalSubnetPrefixes()
        {
            var prefixes = new HashSet<string>();
            try
            {
                var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(nic => nic.OperationalStatus == OperationalStatus.Up &&
                                  nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    .OrderByDescending(nic => nic.GetIPProperties().GatewayAddresses.Count > 0);

                foreach (var nic in interfaces)
                {
                    var ipProps = nic.GetIPProperties();
                    foreach (var unicast in ipProps.UnicastAddresses)
                    {
                        if (unicast.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            var bytes = unicast.Address.GetAddressBytes();
                            // Standard class C /24 subnet prefix
                            string prefix = $"{bytes[0]}.{bytes[1]}.{bytes[2]}.";
                            prefixes.Add(prefix);
                        }
                    }
                }
            }
            catch
            {
                // Fallback to common home/office subnets
            }

            if (!prefixes.Any())
            {
                prefixes.Add("192.168.0.");
                prefixes.Add("192.168.1.");
            }

            return prefixes.ToList();
        }

        /// <summary>
        /// Performs an ultra-fast parallel scan across the 254 hosts of the specified subnet prefix
        /// searching for devices with open printer ports (default: 9100 RAW).
        /// </summary>
        public static async Task<List<DiscoveredPrinter>> ScanSubnetAsync(
            string subnetPrefix,
            int port = 9100,
            int timeoutMs = 300,
            Action<int, int>? progressCallback = null,
            CancellationToken cancellationToken = default)
        {
            var discovered = new System.Collections.Concurrent.ConcurrentBag<DiscoveredPrinter>();
            var ipList = Enumerable.Range(1, 254).Select(i => $"{subnetPrefix}{i}").ToList();

            int completed = 0;

            await Parallel.ForEachAsync(ipList, new ParallelOptions
            {
                MaxDegreeOfParallelism = 50,
                CancellationToken = cancellationToken
            }, async (ip, token) =>
            {
                if (token.IsCancellationRequested) return;

                var sw = System.Diagnostics.Stopwatch.StartNew();
                try
                {
                    using var client = new TcpClient();
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
                    cts.CancelAfter(timeoutMs);

                    await client.ConnectAsync(ip, port, cts.Token);
                    sw.Stop();

                    if (client.Connected)
                    {
                        discovered.Add(new DiscoveredPrinter
                        {
                            IpAddress = ip,
                            Port = port,
                            LatencyMs = sw.ElapsedMilliseconds
                        });
                    }
                }
                catch
                {
                    // Host not responding or port closed
                }
                finally
                {
                    int current = Interlocked.Increment(ref completed);
                    if (current % 25 == 0 || current == 254)
                    {
                        progressCallback?.Invoke(current, 254);
                    }
                }
            });

            return discovered.OrderBy(d => d.LatencyMs).ToList();
        }
    }
}
