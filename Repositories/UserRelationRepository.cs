using Dapper;
using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Interfaces.Repositories;
using InternalPortal.ApplicationCore.Models.ProjectManagement;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace InternalPortal.Infrastructure.Persistence.Repositories
{
    public class UserRelationRepository : EfRepository<UserRelation>, IUserRelationRepository
    {
        public UserRelationRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<List<MemberOfUserResponse>> GetAllRelationDtoModelAsync()
        {
            var query = "Internal_Project_UserRelation_GetAll";
            var response = await Context.Database.GetDbConnection().QueryAsync<MemberOfUserResponse>(query, commandType: CommandType.StoredProcedure);
            return response.ToList();
        }
    }
}
