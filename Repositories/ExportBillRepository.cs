using Dapper;
using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Helpers;
using InternalPortal.ApplicationCore.Interfaces.Repositories;
using InternalPortal.ApplicationCore.Models.ExportBill;
using InternalPortal.ApplicationCore.Models.PurchaseOrder.PurchaseRequest;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace InternalPortal.Infrastructure.Persistence.Repositories
{
    public class ExportBillRepository : EfRepository<ExportBill>, IExportBillRepository
    {
        public ExportBillRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task RemoveExportBillByPrIdAsync(int id)
        {
            string query = "Internal_PurchaseRequest_RemoveEb_ById";

            var parameters = new DynamicParameters(
                new
                {
                    @id = id
                }
            );

            await Context.Database.GetDbConnection().ExecuteAsync(query, parameters, commandType: CommandType.StoredProcedure);
        }
        public async Task<List<ExportBillPagingResponseRaw>> GetAllWithPagingAsync(ExportBillPagingModel model)
        {
            string query = @"Internal_GetExportBillPaging";
            var parameters = new DynamicParameters(
                 new
                 {
                     @countSkip = model.PageIndex,
                     @pageSize = model.PageSize,
                     @keyword = model.Keyword,
                     @userIds = model.UserIds,
                     @departmentIds = model.DepartmentIds,
                     @projectIds = model.ProjectIds,
                     @status = model.Status
                 }
              );
            var res = await Context.Database.GetDbConnection()
             .QueryAsync<ExportBillPagingResponseRaw>(query, parameters,
                 commandType: CommandType.StoredProcedure);
            return res.ToList();
        }
        public async Task<List<ExportBillDetailResponseRaw>> GetExportBillDetailAsync(int exportBillId)
        {
            string query = @"Internal_GetExportBillDetail";
            var parameters = new DynamicParameters(
                 new
                 {
                     @exportBillId = exportBillId
                 }
              );
            var res = await Context.Database.GetDbConnection()
             .QueryAsync<ExportBillDetailResponseRaw>(query, parameters,
                 commandType: CommandType.StoredProcedure);
            return res.ToList();
        }

        public async Task<List<ExportBillLineItemResponseRaw>> GetExportBillLineItemAsync(int exportBillDetailId, int exportBillId)
        {
            string query = @"Internal_GetDetailForEBDetail";
            var parameters = new DynamicParameters(
                 new
                 {
                     @exportBillDetailId = exportBillDetailId,
                     @exportBillId = exportBillId
                 }
              );
            var res = await Context.Database.GetDbConnection()
             .QueryAsync<ExportBillLineItemResponseRaw>(query, parameters,
                 commandType: CommandType.StoredProcedure);
            return res.ToList();
        }

        public async Task<List<ExportBillInfoModel>> GetDtoByLineItemIdsAsync(List<int> lineItemIds)
        {
            string query = "Internal_PurchaseRequest_GetEbInfoByLineItemIds";

            var parameters = new DynamicParameters(
                new
                {
                    @ids = lineItemIds.JoinComma(true)
                }
            );

            var res = await Context.Database.GetDbConnection().QueryAsync<ExportBillInfoModel>(query, parameters, commandType: CommandType.StoredProcedure);
            return res.ToList();
        }
    }
}
