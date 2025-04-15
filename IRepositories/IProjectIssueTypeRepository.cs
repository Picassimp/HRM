using InternalPortal.ApplicationCore.Entities;

namespace InternalPortal.ApplicationCore.Interfaces.Repositories
{
    public interface IProjectIssueTypeRepository : IRepository<ProjectIssueType>
    {
        Task<List<ProjectIssueType>> GetListByProjectIdAsync(int projectId);
        Task<List<ProjectIssueType>> GetByProjectIdsAsync(List<int> projectIds);
    }
}
