using InternalPortal.ApplicationCore.Entities;

namespace InternalPortal.ApplicationCore.Interfaces.Repositories
{
    public interface IProjectTimesheetTagRepository : IRepository<ProjectTimesheetTag>
    {
        Task<List<ProjectTimesheetTag>> GetByProjectTimesheetIdsAsync(List<int> ids);
    }
}
