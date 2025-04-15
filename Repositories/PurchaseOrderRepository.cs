using Dapper;
using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Helpers;
using InternalPortal.ApplicationCore.Interfaces.Repositories;
using InternalPortal.ApplicationCore.Models.PurchaseOrder.PurchaseOrder;
using InternalPortal.ApplicationCore.Models.PurchaseOrder.PurchaseRequest;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace InternalPortal.Infrastructure.Persistence.Repositories
{
    public class PurchaseOrderRepository : EfRepository<PurchaseOrder>, IPurchaseOrderRepository
    {
        public PurchaseOrderRepository(ApplicationDbContext context) : base(context)
        {
        }     

        public async Task<List<PurchaseOrderDtoModel>> GetPoDtoByPrIdsAsync(List<int> prIds)
        {
            string query = "Internal_PurchaseRequest_GetPoDtoByPrIds";

            var parameters = new DynamicParameters(
                new
                {
                    @prIds = prIds.JoinComma(true)
                }
            );

            var res = await Context.Database.GetDbConnection().QueryAsync<PurchaseOrderDtoModel>(query, parameters, commandType: CommandType.StoredProcedure);
            return res.ToList();
        }
        public async Task<List<POPRLineItemResponseRaw>> GetPOPRLineItemAsync(int purchaseOrderDetailId, int purchaseOrderId)
        {
            string query = @"Internal_GetDetailForPODetail";
            var parameters = new DynamicParameters(
                 new
                 {
                     purchaseOrderDetailId,
                     purchaseOrderId
                 }
              );
            var res = await Context.Database.GetDbConnection()
             .QueryAsync<POPRLineItemResponseRaw>(query, parameters,
                 commandType: CommandType.StoredProcedure);
            return res.ToList();
        }

        public async Task<List<PurchaseOrderProductDetailRaw>> GetPurchaseOrderDetailAsync(int purchaseOrderId)
        {
            string query = @"Internal_GetPurchaseOrderDetail";
            var parameters = new DynamicParameters(
                 new
                 {
                     purchaseOrderId
                 }
              );
            var res = await Context.Database.GetDbConnection()
             .QueryAsync<PurchaseOrderProductDetailRaw>(query, parameters,
                 commandType: CommandType.StoredProcedure);
            return res.ToList();
        }

        public async Task<List<PurchaseOrderPagingReponseRaw>> GetPurchaseOrderPagingAsync(PurchaseOrderPagingModel model)
        {
            string query = @"Internal_GetPurchaseOrderPaging";
            var parameters = new DynamicParameters(
                 new
                 {
                     @countSkip = model.PageIndex,
                     @pageSize = model.PageSize,
                     @keyword = model.Keyword,
                     @vendorIds = model.VendorIds,
                     @departmentIds = model.DepartmentIds,
                     @projectIds = model.ProjectIds,
                     @status = model.Status,
                     @isCompensationPO = model.IsCompensationPO,
                 }
              );
            var res = await Context.Database.GetDbConnection()
             .QueryAsync<PurchaseOrderPagingReponseRaw>(query, parameters,
                 commandType: CommandType.StoredProcedure);
            return res.ToList();
        }
        public async Task<List<PurchaseOrderTotalPriceModel>> GetPurchaseOrderTotalPriceAsync(int purchaseOrderId)
        {
            string query = @"Internal_GetPOTotalPrice";
            var parameters = new DynamicParameters(
                 new
                 {
                     @purchaseOrderId = purchaseOrderId
                 }
              );
            var res = await Context.Database.GetDbConnection()
             .QueryAsync<PurchaseOrderTotalPriceModel>(query, parameters,
                 commandType: CommandType.StoredProcedure);
            return res.ToList();
        }

        public async Task<List<PurchaseOrderValidateModel>> GetPoPriceByIdsAsync(List<int> poIds)
        {
            string query = "Internal_PurchaseOrder_GetTotalPriceByPoIds";
            var parameters = new DynamicParameters(
            new
            {
                    @ids = poIds.JoinComma(true)
                }
            );

            var res = await Context.Database.GetDbConnection().QueryAsync<PurchaseOrderValidateModel>(query, parameters, commandType: CommandType.StoredProcedure);
            return res.ToList();
        }

        public async Task UpdateStatusAsync(List<int> poIds)
        {
            string query = "Internal_PurchaseOrder_UpdateStatus";
            var parameters = new DynamicParameters(
                new
                {
                    @ids = poIds.JoinComma(true)
                }
            );

            await Context.Database.GetDbConnection().ExecuteAsync(query, parameters, commandType: CommandType.StoredProcedure);
        }

        public async Task<List<ExportBillDtoModel>> GetEbDtoByPrIdsAsync(List<int> prIds)
        {
            string query = "Internal_PurchaseRequest_GetEbDtoByPrIds";

            var parameters = new DynamicParameters(
                new
                {
                    @prIds = prIds.JoinComma(true)
                }
            );

            var res = await Context.Database.GetDbConnection().QueryAsync<ExportBillDtoModel>(query, parameters, commandType: CommandType.StoredProcedure);
            return res.ToList();
        }
    }
}
