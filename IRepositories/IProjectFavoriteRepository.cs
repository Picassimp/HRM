using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Models.ProjectManagement;
using InternalPortal.ApplicationCore.Models.ProjectManagement.ProjectFavorite;

namespace InternalPortal.ApplicationCore.Interfaces.Repositories
{
    public interface IProjectFavoriteRepository : IRepository<ProjectFavorite>
    {
        Task<List<ProjectFavoriteResponse>> GetListByUserIdAsync(int userId);
        Task<List<ProjectDropdownResponse>> GetProjectDropdownByUserIdAsync(int userId);
    }
}
