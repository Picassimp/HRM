using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Models.ProjectManagement;

namespace InternalPortal.ApplicationCore.Interfaces.Repositories
{
    public interface IClientRepository : IRepository<Client>
    {
        Task<List<CompanyDataModel>> GetCompanyDataFilterAsync(int userId);
        Task<List<ProjectDataModel>> GetClientFilterDataAsync(int userId);
        Task<List<Client>> GetListCompanyAsync();
        Task<List<Client>> ManagerGetAllCompanyAsync(int userId);
        Task<List<Client>> GetExistClientAsync(int? companyId, string company, int? userId);
        Task<List<CommonUserProjectsRaw>> ManagerGetDataProjectsAsync(int userId);
    }
}
