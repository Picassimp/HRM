using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Models.ProjectModels;

namespace InternalPortal.ApplicationCore.Interfaces.Repositories
{
    public interface IProjectStageRepository : IRepository<ProjectStage>
    {
        Task<ProjectStage?> GetByProjectIdAndStageNameAsync(int projectId, string projectStageName);
        Task<bool> CheckExistByStartAndEndDateAsync(int projectId, DateTime startDate, DateTime endDate, int? projectStageId = null);
        Task<bool> CheckExistInProcessStageAsync(int projectId, int? projectStageId = null);
        Task<List<ProjectStageResponseRawModel>> GetAllByProjectIdAsync(int projectId);
        Task<List<ProjectStage>> GetByProjectIdsAsync(List<int> projectIds);
    }
}
