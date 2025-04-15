using Dapper;
using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Interfaces.Repositories;
using InternalPortal.ApplicationCore.Models.PaymentPlan;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace InternalPortal.Infrastructure.Persistence.Repositories
{
    public class PaymentPlanRepository : EfRepository<PaymentPlan>, IPaymentPlanRepository
    {
        public PaymentPlanRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<List<PaymentPlanPagingResponseRaw>> GetAllWithPagingAsync(PaymentPlanPagingModel model)
        {
            string query = @"Internal_GetPaymentPlanPaging";
            var parameters = new DynamicParameters(
                 new
                 {
                     @countSkip = model.PageIndex,
                     @pageSize = model.PageSize,
                     @keyword = model.Keyword,
                     @purchaseOrderId = model.PurchaseOrderId,
                     @status = model.Status
                 }
              );
            var res = await Context.Database.GetDbConnection()
             .QueryAsync<PaymentPlanPagingResponseRaw>(query, parameters,
                 commandType: CommandType.StoredProcedure);
            return res.ToList();
        }

        public async Task<List<PaymentPlan>> GetByPoIdsAsync(List<int> poIds)
        {
            return await DbSet.Where(o => poIds.Contains(o.PurchaseOrderId)).ToListAsync();
        }
    }
}
