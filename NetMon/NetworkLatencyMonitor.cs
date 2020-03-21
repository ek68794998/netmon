namespace NetMon
{
    using System;
    using System.Net;
    using System.Net.NetworkInformation;
    using System.Threading.Tasks;

    public class NetworkLatencyMonitor : IMonitor
    {
        private const int PingTimeoutMilliseconds = 1000;

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
            }
            else if (pingReply.Status == IPStatus.Success)
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
}
