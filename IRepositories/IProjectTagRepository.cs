using InternalPortal.ApplicationCore.Entities;

namespace InternalPortal.ApplicationCore.Interfaces.Repositories
{
    public interface IProjectTagRepository : IRepository<ProjectTag>
    {
        Task<List<ProjectTag>> GetListByProjectIdAsync(int projectId);
        Task<List<ProjectTag>> GetByProjectIdsAsync(List<int> projectIds);
    }
}
