namespace NetMon
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.NetworkInformation;
    using System.Threading.Tasks;

    public class Program
    {
        private const int PingTimeoutMilliseconds = 1000;

        private static readonly List<string> ByteSuffices =
            new List<string> { " B", " kB", " MB", " GB", " TB", " PB" };

        private static readonly string LogFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.log";

        public static async Task<int> Main(string[] args)
        {
            var logger = new Logger(LogFile);

            logger.WriteMessage(new string('-', 50));
            logger.WriteMessage($"Starting up NetMon...");
            logger.WriteMessage(new string('-', 50));

            if (args.Length < 2)
            {
                logger.WriteError($"Invalid number of arguments ({args.Length}).");

                return 1;
            }

            if (!Uri.TryCreate(args[0], UriKind.Absolute, out Uri endpoint))
            {
                logger.WriteError($"Invalid URI '{args[0]}'.");

                return 2;
            }

            if (!int.TryParse(args[1], out int delayMilliseconds) || delayMilliseconds < 100)
            {
                logger.WriteError($"Invalid delay '{args[1]}'.");

                return 2;
            }

            var delay = TimeSpan.FromMilliseconds(delayMilliseconds);

            IEnumerable<IMonitor> monitors =
                new List<IMonitor>
                {
                    new NetworkAvailabilityMonitor(logger),
                    new NetworkLatencyMonitor(logger, endpoint),
                    new NetworkStatisticsMonitor(logger),
                };

            foreach (IMonitor monitor in monitors)
            {
                monitor.Initialize();
            }

            while (true)
            {
                await Task.Delay(delay);

                foreach (IMonitor monitor in monitors)
                {
                    bool shouldContinue =
                        await monitor.UpdateAsync();

                    if (!shouldContinue)
                    {
                        break;
                    }
                }
            }
        }

        private static string FormatBytes(long bytes, int precision = 0)
        {
            const long BytesPerKilobyte = 1024;

            double bytesDecimal = bytes;
            int i;

            for (i = 0; bytesDecimal > BytesPerKilobyte; i++)
            {
                bytesDecimal /= BytesPerKilobyte;
            }

            return Math.Round(bytesDecimal, precision) + ByteSuffices[i];
        }

        private interface ILogger
        {
            void WriteError(string text);

            void WriteMessage(string text);
        }

        private interface IMonitor
        {
            void Initialize();

            Task<bool> UpdateAsync();
        }

        private class Logger : ILogger
        {
            private readonly string logFile;

            public Logger(string logFile)
            {
                this.logFile = logFile ?? throw new ArgumentNullException(nameof(logFile));
            }

            public void WriteError(string text)
            {
                this.WriteTextRaw($"[E] {text}");
            }

            public void WriteMessage(string text)
            {
                this.WriteTextRaw($"[M] {text}");
            }

            private void WriteTextRaw(string text)
            {
                var now = DateTime.Now;
                string time =
                    $"{now.ToString("d", System.Threading.Thread.CurrentThread.CurrentCulture)} {now.ToString("HH:mm:ss.fffffff")}";

                File.AppendAllLines(this.logFile, text.Split('\n').Select(l => $"{time}  {l}"));
                Console.WriteLine(text);
            }
        }

        private class NetworkAvailabilityMonitor : IMonitor
        {
            private readonly ILogger logger;

            public NetworkAvailabilityMonitor(ILogger logger)
            {
                this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            }

            public void Initialize()
            {
                NetworkChange.NetworkAvailabilityChanged +=
                    new NetworkAvailabilityChangedEventHandler(this.OnNetworkAvailabilityChanged);
            }

            public Task<bool> UpdateAsync()
            {
                bool isNetworkAvailable = NetworkInterface.GetIsNetworkAvailable();

                if (!isNetworkAvailable)
                {
                    this.logger.WriteMessage("Network unavailable.");

                    return Task.FromResult(false);
                }

                return Task.FromResult(true);
            }

            private void OnNetworkAvailabilityChanged(object sender, NetworkAvailabilityEventArgs args)
            {
                string statusString = args.IsAvailable ? "Available" : "Unavailable";

                this.logger.WriteMessage($"Network changed. ({statusString})");
            }
        }

        private class NetworkLatencyMonitor : IMonitor
        {
            private readonly ILogger logger;

            private readonly Uri endpointUri;

            private IPAddress ipAddress;

            public NetworkLatencyMonitor(ILogger logger, Uri endpointUri)
            {
                this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
                this.endpointUri = endpointUri ?? throw new ArgumentNullException(nameof(endpointUri));
            }

            public void Initialize()
            {
                IPAddress[] hostEntry = Dns.GetHostAddresses(this.endpointUri.Host);

                if (hostEntry.Length < 1)
                {
                    throw new InvalidOperationException($"Unable to resolve '{this.endpointUri.Host}'.");
                }

                this.ipAddress = hostEntry[0];

                this.logger.WriteMessage($"Resolved '{this.endpointUri.Host}' to '{this.ipAddress}'");
            }

            public async Task<bool> UpdateAsync()
            {
                PingReply pingReply;

                try
                {
                    var ping = new Ping();

                    pingReply = await ping.SendPingAsync(this.ipAddress, PingTimeoutMilliseconds);
                }
                catch (Exception e)
                {
                    this.logger.WriteMessage($"Unable to ping {this.ipAddress}: {e.Message}");

                    return true;
                }

                if (pingReply.Status == IPStatus.TimedOut)
                {
                    this.logger.WriteMessage($"Ping to {this.ipAddress} timed out.");

                    return false;
                }

                if (pingReply.Status == IPStatus.Success)
                {
                    this.logger.WriteMessage(
                        $"Ping to {this.ipAddress} returned in {pingReply.RoundtripTime} ms.");
                }
                else
                {
                    this.logger.WriteMessage(
                        $"Ping to {this.ipAddress} had status '{pingReply.Status}' after {pingReply.RoundtripTime} ms.");
                }

                return true;
            }
        }

        private class NetworkStatisticsMonitor : IMonitor
        {
            private readonly ILogger logger;

            private long currentBytesIn;

            private long currentBytesOut;

            private long currentPacketsLost;

            private bool initialValuesSet;

            public NetworkStatisticsMonitor(ILogger logger)
            {
                this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            }

            public void Initialize()
            {
                this.currentBytesIn = this.currentBytesOut = this.currentPacketsLost = 0L;
                this.initialValuesSet = false;
            }

            public Task<bool> UpdateAsync()
            {
                NetworkInterface[] networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();

                long bytesIn = 0,
                     bytesOut = 0,
                     packetsLost = 0;

                foreach (NetworkInterface networkInterface in networkInterfaces)
                {
                    IPInterfaceProperties properties = networkInterface.GetIPProperties();

                    bool isInternet =
                        networkInterface.OperationalStatus == OperationalStatus.Up
                        && networkInterface.NetworkInterfaceType != NetworkInterfaceType.Ppp
                        && networkInterface.NetworkInterfaceType != NetworkInterfaceType.Loopback
                        && (properties.IsDnsEnabled || properties.IsDynamicDnsEnabled);

                    if (!isInternet)
                    {
                        continue;
                    }

                    IPv4InterfaceStatistics statistics = networkInterface.GetIPv4Statistics();

                    bytesIn += statistics.BytesReceived;
                    bytesOut += statistics.BytesSent;
                    packetsLost += statistics.IncomingPacketsDiscarded + statistics.IncomingPacketsWithErrors;
                }

                if (initialValuesSet)
                {
                    bytesIn -= currentBytesIn;
                    bytesOut -= currentBytesOut;
                    packetsLost -= currentPacketsLost;

                    currentBytesIn += bytesIn;
                    currentBytesOut += bytesOut;
                    currentPacketsLost += packetsLost;
                }
                else
                {
                    currentBytesIn = bytesIn;
                    currentBytesOut = bytesOut;
                    currentPacketsLost = packetsLost;

                    bytesIn = 0;
                    bytesOut = 0;
                    packetsLost = 0;

                    initialValuesSet = true;
                }

                this.logger.WriteMessage(
                    $"Bytes in: {FormatBytes(bytesIn, 2)}, bytes out: {FormatBytes(bytesOut, 2)}, packets lost: {packetsLost}.");

                return Task.FromResult(true);
            }
        }
    }
}
