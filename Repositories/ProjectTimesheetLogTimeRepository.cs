using Dapper;
using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Interfaces.Repositories;
using InternalPortal.ApplicationCore.Models.ProjectManagement;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace InternalPortal.Infrastructure.Persistence.Repositories
{
    public class ProjectTimesheetLogTimeRepository : EfRepository<ProjectTimesheetLogTime>, IProjectTimesheetLogTimeRepository
    {
        public ProjectTimesheetLogTimeRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<ProjectTimesheetLogTime> GetByLogworkId(string logworkId)
        {
            return await DbSet.FirstOrDefaultAsync(t => t.LogWorkId == logworkId);
        }

        public async Task<List<AzureDevOpsDataResponse>> GetLogTimeAzureAsync(string taskId,int projectId)
        {
            string query = @"Internal_AzureDevOps_GetLogTime";
            var parameters = new DynamicParameters(
                 new
                 {
                     taskId,
                     projectId
                 }
              );
            var res = await Context.Database.GetDbConnection()
             .QueryAsync<AzureDevOpsDataResponse>(query, parameters,
                 commandType: CommandType.StoredProcedure);
            return res.ToList();
        }
    }
}
