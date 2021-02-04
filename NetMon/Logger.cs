namespace NetMon
{
    using System;
    using System.IO;
    using System.Linq;

    public class Logger : ILogger
    {
        private readonly string logFileName;

        public Logger(string logFileName)
        {
            this.logFileName = logFileName ?? throw new ArgumentNullException(nameof(logFileName));
        }

        public void WriteError(string text)
        {
            this.WriteTextRaw("E", text);
        }

        public void WriteMessage(string text)
        {
            this.WriteTextRaw("M", text);
        }

        private void WriteTextRaw(string prefix, string text)
        {
            var now = DateTime.Now;

            string timestamp =
                $"{now.ToString("d", System.Threading.Thread.CurrentThread.CurrentCulture)} {now:HH:mm:ss.fffffff}";

            string fileName =
                $"{logFileName}.{now:yyyy.MM.dd}.log";

            File.AppendAllLines(
                fileName,
                text.Replace("\r", string.Empty).Split('\n').Select(l => $"{timestamp}  [{prefix}] {l}"));

            Console.WriteLine(text);
        }
    }
}
