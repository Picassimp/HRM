using Dapper;
using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Helpers;
using InternalPortal.ApplicationCore.Interfaces.Repositories;
using InternalPortal.ApplicationCore.Models.CriteriaModel;
using InternalPortal.ApplicationCore.Models.PagingModel;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace InternalPortal.Infrastructure.Persistence.Repositories
{
    public class WorkFromHomeApplicationRepository : EfRepository<WorkFromHomeApplication>, IWorkFromHomeApplicationRepository
    {
        public WorkFromHomeApplicationRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<List<WorkFromHomeApplicationPagingRawModel>> GetAllWithPagingAsync(WorkFromHomeApplicationCriteriaModel model, int userId)
        {
            string query = @"Internal_WorkFromHomeApplication_GetPaging";

            var parameters = new DynamicParameters(
                new
                {
                    countSkip = model.PageIndex,
                    pageSize = model.PageSize,
                    userId,
                    type = (int)model.Type!,
                    status = model.Status,
                    searchUserId = model.SearchUserId,
                    searchReviewerId = model.SearchReviewerId,
                    fromDate = model.FromDate,
                    toDate = model.ToDate,
                    isNoPaging = model.IsNoPaging,
                    keysort = model.Keysort,
                    orderByDescending = model.OrderByDescending
                });

            var response = await Context.Database.GetDbConnection().QueryAsync<WorkFromHomeApplicationPagingRawModel>(query, parameters, commandType: CommandType.StoredProcedure);

            return response.ToList();
        }

        public async Task<List<WorkFromHomeApplicationPagingMobileModelRaw>> GetAllWithPagingMobileAsync(WorkFromHomeApplicationSearchMobileModel searchModel, int userId)
        {
            string query = @"Internal_GetWorkFromHomeApplicationPagingMobile";

            var parameters = new DynamicParameters(
                new
                {
                    countSkip = searchModel.PageIndex,
                    pageSize = searchModel.PageSize,
                    userId = userId,
                    type = searchModel.Type,
                    status = searchModel.Status,
                    searchUserId = searchModel.SearchUserId,
                    searchReviewerId = searchModel.SearchReviewerId,
                    fromDate = searchModel.FromDate,
                    toDate = searchModel.ToDate,
                    IsNoPaging = 1
                }
            );

            var res = await Context.Database.GetDbConnection()
            .QueryAsync<WorkFromHomeApplicationPagingMobileModelRaw>(query, parameters,
                commandType: CommandType.StoredProcedure);

            return res.ToList();
        }

        public async Task<List<WorkFromHomeApplication>> GetByUserIdAndDateAsync(int userId, DateTime fromDate, DateTime toDate)
        {
            return await DbSet.Where(w => w.UserId == userId && !(w.FromDate > toDate || w.ToDate < fromDate)).ToListAsync();
        }

        public async Task<double> GetTotalWFHDayInMonthByUserIdAndDateAsync(int userId, DateTime date)
        {
            var query = @"Internal_WorkFromHomeApplication_GetTotalWFHDayInMonth";

            var parameters = new DynamicParameters(
                new
                {
                    userId,
                    date,
                    beginMonth = date.FirstDayOfMonth(),
                    endMonth = date.LastDayOfMonth(),
                }
            );

            var response = await Context.Database.GetDbConnection().QueryAsync<double>(query, parameters, commandType: CommandType.StoredProcedure);

            return response.FirstOrDefault();
        }
    }
}
