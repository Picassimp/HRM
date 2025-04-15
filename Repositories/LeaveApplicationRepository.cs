using Dapper;
using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Enums;
using InternalPortal.ApplicationCore.Interfaces.Repositories;
using InternalPortal.ApplicationCore.Models.LeaveApplication;
using Microsoft.EntityFrameworkCore;
using System.Data;
using static InternalPortal.ApplicationCore.ValueObjects.Global;

namespace InternalPortal.Infrastructure.Persistence.Repositories
{
    public class LeaveApplicationRepository : EfRepository<LeaveApplication>, ILeaveApplicationRepository
    {
        public LeaveApplicationRepository(ApplicationDbContext context) : base(context)
        {
            
        }

        public async Task<List<LeaveApplicationStatusModel>> GetByUserIdAndDateAsync(int userId, DateTime fromDate, DateTime toDate)
        {
            var query = "Internal_LeaveApplication_GetByUserIdAndDateRange";
            var parameters = new DynamicParameters(
                new
                {
                    userId = userId,
                    fromDate = fromDate,
                    toDate = toDate,
                }
            );
            var res = await Context.Database.GetDbConnection().QueryAsync<LeaveApplicationStatusModel>(query, parameters, commandType: CommandType.StoredProcedure);
            return res.ToList();
        }

        public async Task<List<LeaveApplication>> GetPendingByUserIdAsync(int userId)
        {
            return await DbSet.Where(o => o.LeaveApplicationType.IsSubTractCumulation && o.ReviewStatus == (int)EReviewStatus.Pending && o.UserId == userId).ToListAsync();
        }

        public async Task<List<LeaveApplication>> GetPendingSickByUserIdAsync(int userId)
        {
            return await DbSet.Where(o => o.LeaveApplicationType.Name == Constant.SICK_LEAVE_APPLICATION_TYPE && o.UserId == userId && o.ReviewStatus == (int)EReviewStatus.Pending).ToListAsync();
        }

        public async Task<List<LeaveApplicationPagingRawModel>> GetAllWithPagingAsync(LeaveApplicationPagingRequest request, int userId)
        {
            string query = "Internal_LeaveApplication_GetPaging";

            var parameters = new DynamicParameters(
                new
                {
                    countSkip = request.PageIndex,
                    pageSize = request.PageSize,
                    userId = userId,
                    type = request.Type,
                    status = request.Status,
                    searchUserId = request.SearchUserId,
                    searchReviewerId = request.SearchReviewerId,
                    fromDate = request.FromDate,
                    toDate = request.ToDate,
                    keysort = request.Keysort,
                    orderByDescending = request.OrderByDescending,
                    isNoPaging = request.IsNoPaging
                }
            );

            var res = await Context.Database.GetDbConnection().QueryAsync<LeaveApplicationPagingRawModel>(query, parameters, commandType: CommandType.StoredProcedure);
            return res.ToList();
        }

        public async Task<List<LeaveApplicationPagingMobileModelRaw>> GetAllWithPagingMobileAsync(LeaveApplicationSearchMobileModel searchModel, int userId)
        {
            string query = @"Internal_GetLeaveApplicationPagingMobile";

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
            .QueryAsync<LeaveApplicationPagingMobileModelRaw>(query, parameters,
                commandType: CommandType.StoredProcedure);

            return res.ToList();
        }
    }
}
