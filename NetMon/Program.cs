namespace NetMon
{
    using System;
    using System.Net;
    using System.Net.NetworkInformation;
    using System.Threading.Tasks;

    public class Program
    {
        private const int PingTimeoutMilliseconds = 1000;

        public static async Task<int> Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.Error.WriteLine($"Invalid number of arguments ({args.Length}).");

                return 1;
            }

            if (!Uri.TryCreate(args[0], UriKind.Absolute, out Uri endpoint))
            {
                Console.Error.WriteLine($"Invalid URI '{args[0]}'.");

                return 2;
            }

            if (!int.TryParse(args[1], out int delayMilliseconds) || delayMilliseconds < 100)
            {
                Console.Error.WriteLine($"Invalid delay '{args[1]}'.");

                return 2;
            }

            IPAddress[] hostEntry = Dns.GetHostAddresses(endpoint.Host);

            if (hostEntry.Length < 1)
            {
                Console.Error.WriteLine($"Unable to resolve '{endpoint.Host}'.");

                return 3;
            }

            NetworkChange.NetworkAvailabilityChanged +=
                new NetworkAvailabilityChangedEventHandler(OnNetworkAvailabilityChanged);

            IPAddress ipAddress = hostEntry[0];

            var delay = TimeSpan.FromMilliseconds(delayMilliseconds);

            while (true)
            {
                await Task.Delay(delay);

                bool isNetworkAvailable = NetworkInterface.GetIsNetworkAvailable();

                if (!isNetworkAvailable)
                {
                    Console.WriteLine("Network unavailable.");

                    continue;
                }

                var ping = new Ping();

                var pingReply = await ping.SendPingAsync(ipAddress, PingTimeoutMilliseconds);

                Console.WriteLine($"Ping to {pingReply.Address} returned in {pingReply.RoundtripTime} ms. ({pingReply.Status})");
            }
        }

        public static void OnNetworkAvailabilityChanged(object sender, NetworkAvailabilityEventArgs args)
        {
            string statusString = args.IsAvailable ? "Available" : "Unavailable";

            Console.WriteLine($"Network changed. ({statusString})");
        }
    }
}
