namespace NetMon
{
    public interface ILogger
    {
        void WriteError(string text);

        void WriteMessage(string text);
    }
}
