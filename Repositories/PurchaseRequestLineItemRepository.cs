using Dapper;
using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Interfaces.Repositories;
using InternalPortal.ApplicationCore.Models.PurchaseOrder.PurchaseRequest;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace InternalPortal.Infrastructure.Persistence.Repositories
{
    public class PurchaseRequestLineItemRepository : EfRepository<PurchaseRequestLineItem>, IPurchaseRequestLineItemRepository
    {
        public PurchaseRequestLineItemRepository(ApplicationDbContext context) : base(context)
        {

        }

        public async Task<List<PurchaseRequestLineItem>> GetByPurchaseRequestIdAsync(int purchaseRequestId)
        {
            return await DbSet.Where(t=>t.PurchaseRequestId == purchaseRequestId).ToListAsync();
        }

        public async Task<List<ItemCreateResponse>> GetItemCreateForEBResponse(string purchaseRequestLineItemIds, int exportBillId)
        {
            string query = @"Internal_GetItemCreateForEB";
            var parameters = new DynamicParameters(
                 new
                 {
                     purchaseRequestLineItemIds,
                     exportBillId,
                 }
              );
            var res = await Context.Database.GetDbConnection()
             .QueryAsync<ItemCreateResponse>(query, parameters,
                 commandType: CommandType.StoredProcedure);
            return res.ToList();
        }

        public async Task<List<ItemCreateResponse>> GetItemCreateResponse(string purchaseRequestLineItemIds,int purchaseOrderId)
        {
            string query = @"Internal_GetItemCreateForPO";
            var parameters = new DynamicParameters(
                 new
                 {
                     purchaseRequestLineItemIds,
                     purchaseOrderId,
                 }
              );
            var res = await Context.Database.GetDbConnection()
             .QueryAsync<ItemCreateResponse>(query, parameters,
                 commandType: CommandType.StoredProcedure);
            return res.ToList();
        }

        public async Task<List<PurchaseRequestLineItemResponseRaw>> GetPurchaseRequestLineItemAsync(int purchaseRequestId, int purchaseOrderId, int exportBillId)
        {
            string query = purchaseOrderId != 0 ? @"Internal_GetItemFromRequest" : @"Internal_GetItemFromRequest_EB";
            var parameters = purchaseOrderId != 0 ? new DynamicParameters(
                 new
                 {
                     purchaseRequestId,
                     purchaseOrderId
                 }
              ) : new DynamicParameters
              (
                 new
                 {
                     purchaseRequestId,
                     exportBillId
                 });
            var res = await Context.Database.GetDbConnection()
             .QueryAsync<PurchaseRequestLineItemResponseRaw>(query, parameters,
                 commandType: CommandType.StoredProcedure);
            return res.ToList();
        }
    }
}