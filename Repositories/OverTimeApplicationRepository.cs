using Dapper;
using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Interfaces.Repositories;
using InternalPortal.ApplicationCore.Models;
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
    public class OverTimeApplicationRepository : EfRepository<OverTimeApplication>, IOverTimeApplicationRepository
    {
        public OverTimeApplicationRepository(ApplicationDbContext context) : base(context)
        {
        }
        public async Task<List<OverTimePagingModel>> GetAllWithPagingAsync(OverTimeCriteriaModel requestModel, int userId)
        {
            string query = @"Internal_OvertTimeApplication_GetPaging";

            var parameters = new DynamicParameters(
                new
                {
                    countSkip = requestModel.PageIndex,
                    pageSize = requestModel.PageSize,
                    userId,
                    type = requestModel.Type,   // 0. Find by owner, 1. Find by Reviewer.
                    status = requestModel.Status,
                    searchUserId = requestModel.SearchUserId,
                    searchReviewerId = requestModel.SearchReviewerId,
                    fromDate = requestModel.FromDate,
                    toDate = requestModel.ToDate,
                    isNoPaging = requestModel.IsNoPaging,
                    keysort = requestModel.Keysort,
                    orderByDescending = requestModel.OrderByDescending
                }
            );

            var response = await Context.Database.GetDbConnection().QueryAsync<OverTimePagingModel>(query, parameters, commandType: CommandType.StoredProcedure);

            return response.ToList();
        }

        public async Task<List<OverTimePagingMobileModelRaw>> GetAllWithPagingForMobileAsync(OverTimeCriteriaModel requestModel, int userId)
        {
            string query = @"Internal_GetOverTimeApplicationPagingMobile";

            var parameters = new DynamicParameters(
                new
                {
                    countSkip = requestModel.PageIndex,
                    pageSize = requestModel.PageSize,
                    userId = userId,
                    type = requestModel.Type,   // 0. Find by owner, 1. Find by Reviewer.
                    status = requestModel.Status,
                    searchUserId = requestModel.SearchUserId,
                    searchReviewerId = requestModel.SearchReviewerId,
                    fromDate = requestModel.FromDate,
                    toDate = requestModel.ToDate,
                    isNoPaging = 1
                }
            );

            var res = await Context.Database.GetDbConnection()
            .QueryAsync<OverTimePagingMobileModelRaw>(query, parameters,
                commandType: CommandType.StoredProcedure);

            return res.ToList();
        }
    }
}
