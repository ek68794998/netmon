namespace NetMon
{
    using System;
    using System.Net.NetworkInformation;
    using System.Threading.Tasks;

    public class NetworkAvailabilityMonitor : IMonitor
    {
        private readonly ILogger logger;

        private bool isNetworkAvailable = true;

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
            if (!this.isNetworkAvailable)
            {
                this.logger.WriteMessage("Network unavailable.");

                return Task.FromResult(false);
            }

            return Task.FromResult(true);
        }

        private void OnNetworkAvailabilityChanged(object sender, NetworkAvailabilityEventArgs args)
        {
            this.isNetworkAvailable = args.IsAvailable;

            string statusString = this.isNetworkAvailable ? "Available" : "Unavailable";

            this.logger.WriteMessage($"Network changed. ({statusString})");
        }
    }
}
