using Dapper;
using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Interfaces.Repositories;
using InternalPortal.ApplicationCore.Models.CriteriaModel;
using InternalPortal.ApplicationCore.Models.PagingModel;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InternalPortal.Infrastructure.Persistence.Repositories
{
    public class OnsiteApplicationRepository : EfRepository<OnsiteApplication>, IOnsiteApplicationRepository
    {
        public OnsiteApplicationRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<List<OnsiteApplicationPagingRawModel>> GetAllWithPagingAsync(OnsiteApplicationCriteriaModel searchModel, int userId)
        {
            string query = @"Internal_OnsiteApplication_GetPaging";

            var parameters = new DynamicParameters(
                new
                {
                    countSkip = searchModel.PageIndex,
                    pageSize = searchModel.PageSize,
                    userId = userId,
                    type = searchModel.Type,   // 0. Find by owner, 1. Find by Reviewer.
                    status = searchModel.Status,
                    searchUserId = searchModel.SearchUserId,
                    searchReviewerId = searchModel.SearchReviewerId,
                    fromDate = searchModel.FromDate,
                    toDate = searchModel.ToDate,
                    isNoPaging = searchModel.IsNoPaging,
                    keysort = searchModel.Keysort,
                    orderByDescending = searchModel.OrderByDescending,
                    isCharge = searchModel.IsCharge
                }
            );

            var response = await Context.Database.GetDbConnection().QueryAsync<OnsiteApplicationPagingRawModel>(query, parameters, commandType: CommandType.StoredProcedure);

            return response.ToList();
        }

        public async Task<List<OnsiteApplicationMobilePagingModelRaw>> GetAllWithPagingMobileAsync(OnsiteApplicationMobileCriteriaModel searchModel, int userId)
        {
            string query = @"Internal_GetOnsiteApplicationPagingMobile";

            var parameters = new DynamicParameters(
                new
                {
                    countSkip = searchModel.PageIndex,
                    pageSize = searchModel.PageSize,
                    userId = userId,
                    type = searchModel.Type,   // 0. Find by owner, 1. Find by Reviewer.
                    status = searchModel.Status,
                    searchUserId = searchModel.SearchUserId,
                    searchReviewerId = searchModel.SearchReviewerId,
                    fromDate = searchModel.FromDate,
                    toDate = searchModel.ToDate,
                    isNoPaging = 1
                }
            );

            var res = await Context.Database.GetDbConnection()
                .QueryAsync<OnsiteApplicationMobilePagingModelRaw>(query, parameters,
                commandType: CommandType.StoredProcedure);

            return res.ToList();
        }

        public async Task<List<OnsiteApplication>> GetByUserIdAndDateAsync(int userId, DateTime fromDate, DateTime toDate)
        {
            return await DbSet.Where(o => o.UserId == userId && !(o.FromDate > toDate || o.ToDate < fromDate)).ToListAsync();
        }
    }
}
