using Dapper;
using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Enums;
using InternalPortal.ApplicationCore.Interfaces.Repositories;
using InternalPortal.ApplicationCore.Models.ProjectManagement;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace InternalPortal.Infrastructure.Persistence.Repositories
{
    public class ClientRepository : EfRepository<Client>, IClientRepository
    {
        public ClientRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<List<ProjectDataModel>> GetClientFilterDataAsync(int userId)
        {
            var clientFilterData = await DbSet.Where(c => !c.IsDeleted && c.UserClients.Any(o => o.UserId == userId)).Select(c =>
                new ProjectDataModel
                {
                    Id = c.Id,
                    Name = c.Company,
                    IsActive = c.IsActive
                }).ToListAsync();
            return clientFilterData;
        }

        public async Task<List<CompanyDataModel>> GetCompanyDataFilterAsync(int userId)
        {
            var companyDataFilter = await DbSet.Where(c => (c.Projects.Any(o => o.ProjectMembers.Any(y => y.IsActive 
                                                                                                 && !y.IsDeleted 
                                                                                                 && y.UserInternalId == userId 
                                                                                                 && (y.Role == (int)EProjectRole.Owner 
                                                                                                 || y.Role == (int)EProjectRole.ProjectManager)))
                                                    || !c.CreatedByUserId.HasValue)
                                                    && !c.IsDeleted).Select(c =>
               new CompanyDataModel
               {
                   Id = c.Id,
                   CompanyName = c.Company,
                   IsActive = c.IsActive
               }).ToListAsync();

            return companyDataFilter;
        }

        public async Task<List<Client>> GetExistClientAsync(int? companyId, string company, int? userId)
        {
            var existClient = await DbSet.Where(o => !o.IsDeleted
                                                                && o.CreatedByUserId.HasValue
                                                                && o.CreatedByUserId == userId).ToListAsync();
            if (companyId.HasValue)
            {
                existClient = existClient.Where(t => t.Id != companyId).ToList();
            }
            if(!string.IsNullOrEmpty(company))
            {
                existClient = existClient.Where(t => t.Company == company).ToList();
            }
            if (userId.HasValue)
            {
                existClient = existClient.Where(t => t.CreatedByUserId == userId).ToList();
            }
            return existClient;
        }

        public async Task<List<Client>> GetListCompanyAsync()
        {
            var listCompany = await DbSet.Where(o => o.IsActive && !o.IsDeleted).ToListAsync();
            return listCompany;
        }

        public async Task<List<Client>> ManagerGetAllCompanyAsync(int userId)
        {
            var clients = await DbSet.Where(o => o.CreatedByUserId == userId && !o.IsDeleted).ToListAsync();
            return clients;
        }

        public async Task<List<CommonUserProjectsRaw>> ManagerGetDataProjectsAsync(int userId)
        {
            string query = @"Internal_ManagerGetDataProjects";
            var parameters = new DynamicParameters(
                 new
                 {
                     @userId = userId
                 }
              );
            var res = await Context.Database.GetDbConnection()
             .QueryAsync<CommonUserProjectsRaw>(query, parameters,
                 commandType: CommandType.StoredProcedure);
            return res.ToList();
        }
    }
}
