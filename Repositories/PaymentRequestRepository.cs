using Dapper;
using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Interfaces.Repositories;
using InternalPortal.ApplicationCore.Models.PaymentRequest;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace InternalPortal.Infrastructure.Persistence.Repositories
{
    public class PaymentRequestRepository : EfRepository<PaymentRequest>, IPaymentRequestRepository
    {
        public PaymentRequestRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<List<PaymentRequestPagingResponseRaw>> GetManagerPagingAsync(PaymentRequestPagingModel model, int userId)
        {
            string query = @"Internal_GetManagerPaymentRequestPaging";
            var parameters = new DynamicParameters(
                 new
                 {
                     @countSkip = model.PageIndex,
                     @pageSize = model.PageSize,
                     @userId = userId,
                     @userIds = model.UserIds,
                     @keyword = model.Keyword,
                     @status = model.Status,
                 }
              );
            var res = await Context.Database.GetDbConnection()
             .QueryAsync<PaymentRequestPagingResponseRaw>(query, parameters,
                 commandType: CommandType.StoredProcedure);
            return res.ToList();
        }
        public async Task<List<PaymentRequestPagingResponseRaw>> GetUserPagingAsync(PaymentRequestPagingModel model,int userId)
        {
            string query = @"Internal_GetUserPaymentRequestPaging";
            var parameters = new DynamicParameters(
                 new
                 {
                     @countSkip = model.PageIndex,
                     @pageSize = model.PageSize,
                     @userId = userId,
                     @keyword = model.Keyword,
                     @status = model.Status,
                 }
              );
            var res = await Context.Database.GetDbConnection()
             .QueryAsync<PaymentRequestPagingResponseRaw>(query, parameters,
                 commandType: CommandType.StoredProcedure);
            return res.ToList();
        }
        public async Task<List<PaymentRequestPagingResponseRaw>> GetAccountantPagingAsync(PaymentRequestPagingModel model)
        {
            string query = @"Internal_GetAccountantPaymentRequestPaging";
            var parameters = new DynamicParameters(
                 new
                 {
                     @countSkip = model.PageIndex,
                     @pageSize = model.PageSize,
                     @userId = model.UserIds,
                     @keyword = model.Keyword,
                     @status = model.Status,
                     @type = model.Type,
                     @reviewUserIds = model.ReviewUserIds,
                     @startDate = model.StartDate,
                     @endDate = model.EndDate,
                 }
              );
            var res = await Context.Database.GetDbConnection()
             .QueryAsync<PaymentRequestPagingResponseRaw>(query, parameters,
                 commandType: CommandType.StoredProcedure);
            return res.ToList();
        }
        public async Task<List<PaymentRequestPagingResponseRaw>> GetDirectorPagingAsync(PaymentRequestPagingModel model)
        {
            string query = @"Internal_GetDirectorPaymentRequestPaging";
            var parameters = new DynamicParameters(
                 new
                 {
                     @countSkip = model.PageIndex,
                     @pageSize = model.PageSize,
                     @userId = model.UserIds,
                     @keyword = model.Keyword,
                     @status = model.Status,
                     @type = model.Type,
                     @reviewUserIds = model.ReviewUserIds,
                     @startDate = model.StartDate,
                     @endDate = model.EndDate,
                 }
              );
            var res = await Context.Database.GetDbConnection()
             .QueryAsync<PaymentRequestPagingResponseRaw>(query, parameters,
                 commandType: CommandType.StoredProcedure);
            return res.ToList();
        }

        public async Task<List<PaymentRequest>> GetMultiRequestAsync(List<int> paymentRequestIds)
        {
            return await DbSet.Where(t=>paymentRequestIds.Contains(t.Id)).ToListAsync();
        }
    }
}
