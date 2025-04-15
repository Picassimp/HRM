using InternalPortal.ApplicationCore.Entities;

namespace InternalPortal.ApplicationCore.Interfaces.Repositories
{
    public interface IGlobalConfigurationRepository : IRepository<GlobalConfiguration> 
    {
        Task<GlobalConfiguration?> GetByNameAsync(string name);
        Task<List<GlobalConfiguration>> GetByMultiNameAsync(List<string> names);
    }
}
