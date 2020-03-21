namespace NetMon
{
    using System;
    using System.IO;
    using System.Linq;

    public class Logger : ILogger
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
}
