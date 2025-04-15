using Dapper;
using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Enums;
using InternalPortal.ApplicationCore.Helpers;
using InternalPortal.ApplicationCore.Interfaces.Repositories;
using InternalPortal.ApplicationCore.Models.ChromeExtension;
using InternalPortal.ApplicationCore.Models.ProjectManagement;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace InternalPortal.Infrastructure.Persistence.Repositories
{
    public class ProjectTimesheetRepository : EfRepository<ProjectTimeSheet>, IProjectTimesheetRepository
    {
        public ProjectTimesheetRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<List<ProjectTimeSheet>> GetByProjectIdAndUserIdAsync(int projectId, int userId)
        {
            return await DbSet.Where(x => x.ProjectId == projectId && x.ProjectMember.UserInternalId == userId).ToListAsync();
        }

        public async Task<List<ManageProjectTimesheetPagingResponse>> ManagerOrOwnerGetProjectPagingAsync(ProjectTimesheetPagingRequest model)
        {
            string query = @"Internal_GetProjectTimesheetByManagerOrOwner";

            var parameters = new DynamicParameters(
                new
                {
                    projectId = model.ProjectId,
                    userId = model.UserId,
                    startDate = model.StartDate,
                    endDate = model.EndDate
                }
            );

            var response = await Context.Database.GetDbConnection()
            .QueryAsync<ManageProjectTimesheetPagingResponse>(query, parameters,
                commandType: CommandType.StoredProcedure);

            return response.ToList();
        }

        public async Task<List<ProjectTimesheetPagingResponse>> UserGetProjectTimesheetPagingAsync(int userId, ProjectTimesheetUserPagingRequest model)
        {
            string query = @"Internal_GetProjectTimesheetByUser";

            var parameters = new DynamicParameters(
                new
                {
                    userId = userId,
                    projectId = model.ProjectId,
                    startDate = model.StartDate,
                    endDate = model.EndDate
                }
            );

            var response = await Context.Database.GetDbConnection()
            .QueryAsync<ProjectTimesheetPagingResponse>(query, parameters,
                commandType: CommandType.StoredProcedure);

            return response.ToList();
        }

        public async Task<List<ProjectTimesheetSelfPagingResponse>> UserGetProjectTimesheetSelfPagingAsync(int userId, ProjectTimesheetSelfUserPagingRequest model)
        {
            string query = @"Internal_GetProjectTimesheetSelfByUser";

            var parameters = new DynamicParameters(
                new
                {
                    userId,
                    projectId = model.ProjectId,
                    clientId = model.ClientId,
                    startDate = model.StartDate,
                    endDate = model.EndDate
                }
            );

            var response = await Context.Database.GetDbConnection().QueryAsync<ProjectTimesheetSelfPagingResponse>(query, parameters, commandType: CommandType.StoredProcedure);
            return response.ToList();
        }

        public async Task<List<SupervisorProjectTimesheetPagingResponse>> SupervisorGetTimesheetPagingAsync(List<int> userIds, SupervisorProjectTimesheetPagingRequest request)
        {
            string query = "Internal_Supervisor_GetProjectTimesheet";

            var parameters = new DynamicParameters(
                new
                {
                    projectId = request.ProjectId,
                    userIds = userIds.JoinComma(true),
                    startDate = request.StartDate,
                    endDate = request.EndDate
                }
            );

            var response = await Context.Database.GetDbConnection().QueryAsync<SupervisorProjectTimesheetPagingResponse>(query, parameters, commandType: CommandType.StoredProcedure);
            return response.ToList();
        }

        public async Task<List<ChromeExtensionRawResponse>> GetTimesheetForExtensionByUserIdAndDateAsync(int userId, DateTime date)
        {
            string query = "Internal_ChromeExtension_GetProjectTimesheet";

            var parameters = new DynamicParameters(
                new
                {
                    userId,
                    date
                }
            );

            var response = await Context.Database.GetDbConnection().QueryAsync<ChromeExtensionRawResponse>(query, parameters, commandType: CommandType.StoredProcedure);
            return response.ToList();
        }

        public async Task<List<ProjectTimeSheet>> GetRunningByUserIdAsync(int userId)
        {
            return await DbSet.Where(o => o.ProjectMember.UserInternalId == userId && !o.ProjectMember.IsDeleted && o.ProjectMember.IsActive && o.ProcessStatus == (int)EProcessStatus.Running).ToListAsync();
        }

        public async Task<List<ProjectTimeSheet>> GetListByProjectIdAsync(int projectId)
        {
            return await DbSet.Where(o => o.ProjectId == projectId && string.IsNullOrEmpty(o.IssueType)).ToListAsync();
        }

        public Task<List<ProjectTimeSheet>> GetByProjectIdStartDateAndEndDateAsync(int projectId, DateTime startDate, DateTime endDate)
        {
            return DbSet.Where(o => o.ProjectId == projectId && o.CreatedDate.Date >= startDate.Date && o.CreatedDate.Date <= endDate && !string.IsNullOrEmpty(o.TaskId)).ToListAsync();
        }
    }
}
