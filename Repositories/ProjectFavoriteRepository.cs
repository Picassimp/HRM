using Dapper;
using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Interfaces.Repositories;
using InternalPortal.ApplicationCore.Models.ProjectManagement;
using InternalPortal.ApplicationCore.Models.ProjectManagement.ProjectFavorite;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace InternalPortal.Infrastructure.Persistence.Repositories
{
    public class ProjectFavoriteRepository : EfRepository<ProjectFavorite>, IProjectFavoriteRepository
    {
        public ProjectFavoriteRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<List<ProjectFavoriteResponse>> GetListByUserIdAsync(int userId)
        {
            var query = "Internal_ProjectFavorite_GetByUserId";
            var parameters = new DynamicParameters(
                new
                {
                    @userId = userId
                }
            );
            var response = await Context.Database.GetDbConnection().QueryAsync<ProjectFavoriteResponse>(query, parameters, commandType: CommandType.StoredProcedure);
            return response.ToList();
        }

        public async Task<List<ProjectDropdownResponse>> GetProjectDropdownByUserIdAsync(int userId)
        {
            var query = "Internal_Project_GetDropdownByUserId";
            var parameters = new DynamicParameters(
                new
                {
                    @userId = userId
                }
            );
            var response = await Context.Database.GetDbConnection().QueryAsync<ProjectDropdownResponse>(query, parameters, commandType: CommandType.StoredProcedure);
            return response.ToList();
        }
    }
}
