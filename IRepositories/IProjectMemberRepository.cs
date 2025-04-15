using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Models.ProjectManagement;

namespace InternalPortal.ApplicationCore.Interfaces.Repositories
{
    public interface IProjectMemberRepository : IRepository<ProjectMember>
    {
        Task<List<ProjectMember>> GetListProjectMemberAsync();
        Task<List<ProjectMembersGetResponse>> GetProjectMemberAsync(Project projectId);
        Task<List<ProjectMember>> GetListProjectMemberAsync(int projectId);
        Task<ProjectMember> GetUserProjectAsync(int projectId, int userId, bool isActive);
        Task<List<MemberTimesheetFilterResponse>> GetMemberFiterAsync(int projectId);
        Task<List<ProjectsOfUserResponse>> GetProjectsByUserIdAsync(int userId);
    }
}
