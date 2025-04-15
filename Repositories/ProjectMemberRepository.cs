using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Interfaces.Repositories;
using InternalPortal.ApplicationCore.Models.ProjectManagement;
using Microsoft.EntityFrameworkCore;
using InternalPortal.ApplicationCore.Enums;
using Dapper;
using System.Data;

namespace InternalPortal.Infrastructure.Persistence.Repositories
{
    public class ProjectMemberRepository : EfRepository<ProjectMember>, IProjectMemberRepository
    {
        public ProjectMemberRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<List<ProjectMember>> GetListProjectMemberAsync()
        {
            var listProjectMember = await DbSet.Where(o => o.IsActive && !o.IsDeleted).ToListAsync();
            return listProjectMember;
        }

        public async Task<List<ProjectMember>> GetListProjectMemberAsync(int projectId)
        {
            var allMember = await DbSet.Where(o => o.ProjectId == projectId && o.IsActive && !o.IsDeleted).ToListAsync();
            return allMember;
        }

        public async Task<List<MemberTimesheetFilterResponse>> GetMemberFiterAsync(int projectId)
        {
            var query = @"Internal_GetMemberFiter";
            var parameters = new DynamicParameters(
                new
                {
                    @projectId = projectId,
                }
            );
            var response = await Context.Database.GetDbConnection()
            .QueryAsync<MemberTimesheetFilterResponse>(query, parameters,
                commandType: CommandType.StoredProcedure);

            return response.ToList();
        }

        public async Task<List<ProjectMembersGetResponse>> GetProjectMemberAsync(Project project)
        {
            var members = await DbSet
                .Where(pm => pm.ProjectId == project.Id && !pm.IsDeleted).ToListAsync();
            var membersResponse = members
                .Select(pm =>
                {
                    return new ProjectMembersGetResponse()
                    {
                        Id = pm.Id,
                        UserId = pm.UserInternalId,
                        UserName = pm.UserInternal.FullName,
                        IsActive = pm.IsActive,
                        Role = (EProjectRole)pm.Role,
                        CreatedDate = pm.CreatedDate,
                        JiraAccountEmail = pm.JiraAccountEmail ?? "",
                        DevOpsAccountEmail = pm.DevOpsAccountEmail ?? "",
                        DefaultEmail = pm.UserInternal.Email ?? ""
                    };
                }).ToList();
            return membersResponse;
        }

        public async Task<ProjectMember> GetUserProjectAsync(int projectId, int userId, bool isActive)
        {
            var projectMembers = await DbSet
                .Where(pm => pm.ProjectId == projectId && pm.UserInternalId == userId && !pm.IsDeleted).ToListAsync();
            if (isActive)
            {
                return projectMembers.Where(t => t.IsActive).SingleOrDefault();
            }
            return projectMembers.SingleOrDefault();
        }

        public async Task<List<ProjectsOfUserResponse>> GetProjectsByUserIdAsync(int userId)
        {
            var query = "Internal_Supervisor_GetProjectsByUserId";
            var parameters = new DynamicParameters(
                new
                {
                    @userId = userId
                }
            );
            var response = await Context.Database.GetDbConnection().QueryAsync<ProjectsOfUserResponse>(query, parameters, commandType: CommandType.StoredProcedure);
            return response.ToList();
        }
    }
}
