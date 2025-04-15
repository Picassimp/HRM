using Dapper;
using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Enums;
using InternalPortal.ApplicationCore.Interfaces.Repositories;
using InternalPortal.ApplicationCore.Models.ProjectManagement;
using InternalPortal.ApplicationCore.Models.ProjectManagement.SyncIssueTypeModel;
using InternalPortal.ApplicationCore.Models.PurchaseOrder;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Text.RegularExpressions;

namespace InternalPortal.Infrastructure.Persistence.Repositories
{
    public class ProjectRepository : EfRepository<Project>, IProjectRepository
    {
        public ProjectRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<List<Project>> GetExistingProjectAsync(ProjectCreateRequest model, int userId)
        {
            var existingProject = await DbSet
                .Where(p => p.Name == model.Name && p.ClientId == model.ClientId && p.CreatedByUserId == userId).ToListAsync();
            return existingProject;
        }

        public async Task<List<Project>> GetListProjectAsync()
        {
            var listProject = await DbSet.Where(o => o.IsActive && !o.IsDeleted).ToListAsync();
            return listProject;
        }

        public async Task<List<Project>> GetListProjectAsync(int clientId)
        {
            var projects = await DbSet.Where(p => p.ClientId.HasValue && p.ClientId == clientId).ToListAsync();
            return projects;
        }

        public async Task<List<ProjectDataModel>> GetProjectDataFilterAsync(int userId)
        {
            // Lấy danh sách dự án mà user có role là Ownwer/ProjectManager
            var projectDataFilter = await DbSet
                .Where(p => !p.IsDeleted
                    && p.ProjectMembers.Any(o => !o.IsDeleted
                                            && o.UserInternalId == userId
                                            && (o.Role == (int)EProjectRole.Owner || o.Role == (int)EProjectRole.ProjectManager)))
                .Select(p => new ProjectDataModel
                {
                    Id = p.Id,
                    Name = p.Name,
                    IsActive = p.IsActive
                }).ToListAsync();

            return projectDataFilter;
        }

        public async Task<List<ProjectDataModel>> GetAllProjectsByUserIdAsync(int userId)
        {
            // Lấy danh sách dự án mà user có tham gia
            var projectDataFilter = await DbSet.Where(p => !p.IsDeleted && p.IsActive && p.ProjectMembers.Any(o => !o.IsDeleted && o.IsActive && o.UserInternalId == userId))
                .Select(p => new ProjectDataModel
                {
                    Id = p.Id,
                    Name = p.Name,
                    IsActive = p.IsActive,
                    Integration = p.Integration,
                    Role = p.ProjectMembers.FirstOrDefault(o => o.UserInternalId == userId)!.Role
                }).ToListAsync();

            return projectDataFilter;
        }

        public async Task<List<ProjectModel>> GetProjectsAsync(int userId)
        {
            var projects = await DbSet
                .Where(c => !c.IsDeleted && c.ProjectMembers.Any(o => !o.IsDeleted && o.IsActive
                                                && o.UserInternalId == userId 
                                                && (o.Role == (int)EProjectRole.Owner || o.Role == (int)EProjectRole.ProjectManager)))
                .Select(p => new ProjectModel
                {
                    Id = p.Id,
                    Name = p.Name,
                    ClientId = p.ClientId,
                    ClientName = p.ClientId.HasValue ? p.Client!.Company : null,
                    IsClientActive = p.ClientId.HasValue && p.Client!.IsActive,
                    ProjectMemberIds = p.ProjectMembers.Select(pm => pm.Id).ToList(),
                    IsActive = p.IsActive
                }).ToListAsync();
            return projects;
        }

        public async Task<List<UserDataModel>> GetUserDataFilterAsync(int userId)
        {
            string query = @"Internal_Project_ManagerGetMemberOfProject";
            var parameters = new DynamicParameters(
                 new
                 {
                     userId
                 }
              );
            var res = await Context.Database.GetDbConnection()
             .QueryAsync<UserDataModel>(query, parameters,
                 commandType: CommandType.StoredProcedure);
            return res.ToList();
        }

        public async Task<List<ProjectReportRawResponse>> ManagerGetChartDataAsync(int managerId, ManagerProjectChartRequest request)
        {
            var query = @"Internal_Manager_Report_Timesheet";
            var parameters = new DynamicParameters(
            new
            {
                @managerId = managerId,
                @startDate = request.StartDate,
                @endDate = request.EndDate,
                @companyIds = request.CompanyIds,
                @projectIds = request.ProjectIds,
                @userIds = request.UserIds,
                @groupBy = !string.IsNullOrEmpty(request.GroupBy) ? "CreatedDate," + request.GroupBy : "CreatedDate",
                @projectStageIds = request.ProjectStageIds,
                @issueTypes = request.IssueTypes,
                @tags = request.Tags
            }
            );
            var response = await Context.Database.GetDbConnection()
            .QueryAsync<ProjectReportRawResponse>(query, parameters,
                commandType: CommandType.StoredProcedure);
            return response.ToList();
        }

        public async Task<List<ProjectDetailForReportingRaw>> ManagerGetDataDetailForReportingAsync(int managerId, ManagerProjectDetailRequest request)
        {
            string query = @"Internal_Project_ManagerGetDataDetailForReporting";
            var parameters = new DynamicParameters(
                new
                {
                    @managerId = managerId,
                    @startDate = request.StartDate,
                    @endDate = request.EndDate,
                    @companyIds = request.CompanyIds,
                    @projectIds = request.ProjectIds,
                    @userIds = request.UserIds,
                    @projectStageIds = request.ProjectStageIds,
                    //Loại bỏ phần (projectName) của API khi truyền vào
                    @issueTypes = request.IssueTypes != null ? Regex.Replace(request.IssueTypes, @"\s*\(.*?\)\s*", "") : null,
                    @tags = request.Tags != null ? Regex.Replace(request.Tags, @"\s*\(.*?\)\s*", "") : null
                });
            var res = await Context.Database.GetDbConnection()
                .QueryAsync<ProjectDetailForReportingRaw>(query, parameters,
                    commandType: CommandType.StoredProcedure);

            return res.ToList();
        }

        public async Task<List<DataForFilterLinkingResponse>> ManagerGetDataForFilterLinkingAsync(int userId)
        {
            string query = @"Internal_Project_ManagerGetDataForFilterLinking";
            var parameters = new DynamicParameters(
                 new
                 {
                     userId
                 }
              );
            var res = await Context.Database.GetDbConnection()
                .QueryAsync<DataForFilterLinkingResponse>(query, parameters,
                    commandType: CommandType.StoredProcedure);

            return res.ToList();
        }

        public async Task<List<ProjectReportRawResponse>> ManagerGetReportDataAsync(int managerId, ManagerProjectReportRequest request)
        {
            var query = @"Internal_Manager_Report_Timesheet";
            var parameters = new DynamicParameters(
            new
            {
                @managerId = managerId,
                @startDate = request.StartDate,
                @endDate = request.EndDate,
                @companyIds = request.CompanyIds,
                @projectIds = request.ProjectIds,
                @userIds = request.UserIds,
                @groupBy = request.GroupBy,
                @projectStageIds = request.ProjectStageIds,
                @issueTypes = request.IssueTypes,
                @tags = request.Tags
            }
            );
            var response = await Context.Database.GetDbConnection()
            .QueryAsync<ProjectReportRawResponse>(query, parameters,
                commandType: CommandType.StoredProcedure);
            return response.ToList();
        }

        public async Task<List<CommonUserProjectsRaw>> UserGetProjectAsync(int userId)
        {
            var query = @"Internal_GetMyProjectBeAssigned";
            var parameters = new DynamicParameters(
                new
                {
                    @userId = userId
                }
            );
            var response = await Context.Database.GetDbConnection()
            .QueryAsync<CommonUserProjectsRaw>(query, parameters,
                commandType: CommandType.StoredProcedure);
            return response.ToList();
        }

        public async Task<List<ProjectSyncIssueTypeResponse>> GetForSyncIssueTypeAsync()
        {
            string query = "Internal_Project_GetForSyncIssueType";
            var response = await Context.Database.GetDbConnection().QueryAsync<ProjectSyncIssueTypeResponse>(query, commandType: CommandType.StoredProcedure);
            return response.ToList();
        }

        public async Task<Project?> GetByAzureDevOpsProjectIdAsync(string azureDevOpsProjectId, string projectId)
        {
            return await DbSet.FirstOrDefaultAsync(t => t.AzureDevOpsProjectId == azureDevOpsProjectId && t.AzureDevOpsProject == projectId);
        }

        public async Task<List<PrProjectDropDownResponseModel>> GetByUserIdsAsync(string? userIds)
        {
            if (string.IsNullOrEmpty(userIds))
            {
                return new List<PrProjectDropDownResponseModel>();
            }

            string query = "Internal_PurchaseRequest_GetProjectDropdown";
            var parameters = new DynamicParameters
            (
                new
                {
                    @userIds = userIds
                }
            );
            var response = await Context.Database.GetDbConnection().QueryAsync<PrProjectDropDownResponseModel>(query, parameters, commandType: CommandType.StoredProcedure);
            return response.ToList();
        }
        public async Task<Project?> GetByAzureDevOpsOrganizationAsync(string azureDevOpsOrganization)
        {
            return await DbSet.FirstOrDefaultAsync(t => t.AzureDevOpsOrganization == azureDevOpsOrganization && !string.IsNullOrEmpty(t.AzureDevOpsProjectId));
        }

        public async Task<Project?> GetByProjectNameAndClientIdAsync(string projectName, int clientId)
        {
            return await DbSet.FirstOrDefaultAsync(o => o.IsActive && !o.IsDeleted && o.Name.Equals(projectName) && o.ClientId == clientId);
        }

        public async Task<decimal?> CalculateTotalWorkingHourByProjectIdAsync(int projectId)
        {
            var query = "Internal_Project_CalculateTotalWorkingHour";
            var parameters = new DynamicParameters
            (
                new
                {
                    @projectId = projectId
                }
            );

            return await Context.Database.GetDbConnection().QueryFirstAsync<decimal?>(query, parameters, commandType: CommandType.StoredProcedure);
        }
    }
}
