namespace NetMon
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public class Program
    {
        private static readonly string LogFileName = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;

        public static async Task<int> Main(string[] args)
        {
            var logger = new Logger(LogFileName);

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
    }
}
