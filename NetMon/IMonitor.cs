namespace NetMon
{
    using System.Threading.Tasks;

    public interface IMonitor
    {
        void Initialize();

        Task<bool> UpdateAsync();
    }
}
