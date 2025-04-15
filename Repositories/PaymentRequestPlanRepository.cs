using Dapper;
using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Interfaces.Repositories;
using InternalPortal.ApplicationCore.Models.PaymentRequest;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace InternalPortal.Infrastructure.Persistence.Repositories
{
    public class PaymentRequestPlanRepository : EfRepository<PaymentRequestPlan>, IPaymentRequestPlanRepository
    {
        public PaymentRequestPlanRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<List<PaymentRequestPlan>> GetByPaymentRequestIdAsync(int paymentRequestId)
        {
            return await DbSet.Where(t=>t.PaymentRequestId == paymentRequestId).ToListAsync();
        }

        public async Task<List<PaymentRequestPlanPagingResponseRaw>> GetPagingAsync(PaymentRequestPlanPagingModel model)
        {
            string query = @"Internal_GetPaymentRequestPlanPaging";
            var parameters = new DynamicParameters(
                 new
                 {
                     @countSkip = model.PageIndex,
                     @pageSize = model.PageSize,
                     @keyword = model.Keyword,
                     @status = model.Status,
                     @isUrgent = model.IsUrgent,
                     @startDate = model.StartDate,
                     @endDate = model.EndDate,
                 }
              );
            var res = await Context.Database.GetDbConnection()
             .QueryAsync<PaymentRequestPlanPagingResponseRaw>(query, parameters,
                 commandType: CommandType.StoredProcedure);
            return res.ToList();
        }
    }
}
