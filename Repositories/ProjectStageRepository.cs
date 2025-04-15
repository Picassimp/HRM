using Dapper;
using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Enums;
using InternalPortal.ApplicationCore.Interfaces.Repositories;
using InternalPortal.ApplicationCore.Models.ProjectModels;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace InternalPortal.Infrastructure.Persistence.Repositories
{
    public class ProjectStageRepository : EfRepository<ProjectStage>, IProjectStageRepository
    {
        public ProjectStageRepository(ApplicationDbContext context) : base(context)
        {
        }

        public Task<bool> CheckExistByStartAndEndDateAsync(int projectId, DateTime startDate, DateTime endDate, int? projectStageId = null)
        {
            if (projectStageId.HasValue)
            {
                return DbSet.AnyAsync(o => o.Id != projectStageId.Value && o.ProjectId == projectId && o.StartDate < endDate && o.EndDate > startDate);
            }
            return DbSet.AnyAsync(o => o.ProjectId == projectId && o.StartDate < endDate && o.EndDate > startDate);
        }

        public async Task<ProjectStage?> GetByProjectIdAndStageNameAsync(int projectId, string projectStageName)
        {
            return await DbSet.FirstOrDefaultAsync(o => o.Name == projectStageName && o.ProjectId == projectId);
        }

        public Task<bool> CheckExistInProcessStageAsync(int projectId, int? projectStageId = null)
        {
            if (projectStageId.HasValue)
            {
                return DbSet.AnyAsync(o => o.Id != projectStageId.Value && o.ProjectId == projectId && o.Status == (int)EProjectStageStatus.InProcess);
            }
            return DbSet.AnyAsync(o => o.ProjectId == projectId && o.Status == (int)EProjectStageStatus.InProcess);
        }

        public async Task<List<ProjectStageResponseRawModel>> GetAllByProjectIdAsync(int projectId)
        {
            var query = "Internal_Project_GetProjectStage";
            var parameters = new DynamicParameters
            (
                new 
                {
                    @projectId = projectId
                }
            );

            var response = await Context.Database.GetDbConnection().QueryAsync<ProjectStageResponseRawModel>(query, parameters, commandType: CommandType.StoredProcedure);
            return response.ToList();
        }
        public async Task<List<ProjectStage>> GetByProjectIdsAsync(List<int> projectIds)
        {
            return await DbSet.Where(ps => projectIds.Contains(ps.ProjectId)).ToListAsync();
        }
    }
}
