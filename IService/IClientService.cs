using InternalPortal.ApplicationCore.Models.ProjectManagement;

namespace InternalPortal.ApplicationCore.Interfaces.Business
{
    public interface IClientService
    {
        Task<List<CommonUserProjectsResponse>> ManagerGetDataProjectsAsync(int userId);
    }
}
