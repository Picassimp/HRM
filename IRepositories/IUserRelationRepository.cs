using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Models.ProjectManagement;

namespace InternalPortal.ApplicationCore.Interfaces.Repositories
{
    public interface IUserRelationRepository : IRepository<UserRelation>
    {
        Task<List<MemberOfUserResponse>> GetAllRelationDtoModelAsync();
    }
}
