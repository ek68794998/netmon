namespace NetMon
{
    using System;
    using System.Collections.Generic;
    using System.Net.NetworkInformation;
    using System.Threading.Tasks;

    public class NetworkStatisticsMonitor : IMonitor
    {
        private static readonly List<string> ByteSuffices =
            new List<string> { " B", " kB", " MB", " GB", " TB", " PB" };

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
            long bytesIn = 0,
                 bytesOut = 0,
                 packetsLost = 0;

            foreach (NetworkInterface networkInterface in GetInternetNetworkInterfaces())
            {
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

        private static IEnumerable<NetworkInterface> GetInternetNetworkInterfaces()
        {
            var networkInterfaces = new List<NetworkInterface>();

            foreach (NetworkInterface networkInterface in NetworkInterface.GetAllNetworkInterfaces())
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

                networkInterfaces.Add(networkInterface);
            }

            return networkInterfaces;
        }
    }
}
