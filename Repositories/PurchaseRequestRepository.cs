using Dapper;
using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Helpers;
using InternalPortal.ApplicationCore.Enums;
using InternalPortal.ApplicationCore.Interfaces.Repositories;
using InternalPortal.ApplicationCore.Models.PurchaseOrder;
using InternalPortal.ApplicationCore.Models.PurchaseOrder.PurchaseRequest;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace InternalPortal.Infrastructure.Persistence.Repositories
{
    public class PurchaseRequestRepository : EfRepository<PurchaseRequest>, IPurchaseRequestRepository
    {
        public PurchaseRequestRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<List<PurchaseRequestPagingResponse>> GetAllWithPagingAsync(PurchaseRequestPagingModel request, int userId)
        {
            string query = "Internal_PurchaseRequest_User_GetPaging";

            var parameters = new DynamicParameters(
                new
                {
                    countSkip = request.PageIndex,
                    pageSize = request.PageSize,
                    userId = userId,
                    isUrgent = request.IsUrgent
                }
            );

            var res = await Context.Database.GetDbConnection().QueryAsync<PurchaseRequestPagingResponse>(query, parameters, commandType: CommandType.StoredProcedure);
            return res.ToList();
        }

        public async Task<int?> IsValidMemberAsync(int projectId, int userId)
        {
            string query = "Internal_PurchaseRequest_IsValidMember_ByProjectIdAndUserId";
            var parameters = new DynamicParameters(
               new
               {
                   @projectId = projectId,
                   @userId = userId
               }
            );
            return await Context.Database.GetDbConnection().QueryFirstOrDefaultAsync<int?>(query, parameters, commandType: CommandType.StoredProcedure);
        }

        public async Task<List<PurchaseRequestManagerPagingResponse>> GetAllWithPagingByManagerAsync(int managerId, PurchaseRequestManagerPagingRequest request)
        {
            string query = "Internal_PurchaseRequest_Manager_GetPaging";

            var parameters = new DynamicParameters(
                new
                {
                    managerId = managerId,
                    countSkip = request.PageIndex,
                    pageSize = request.PageSize,
                    keyword = request.Keyword ?? "",
                    userIds = request.UserIds,
                    departmentIds = request.DepartmentIds,
                    projectIds = request.ProjectIds,
                    status = request.Status,
                    isUrgent = request.IsUrgent,
                }
            );

            var response = await Context.Database.GetDbConnection().QueryAsync<PurchaseRequestManagerPagingResponse>(query, parameters, commandType: CommandType.StoredProcedure);
            return response.ToList();
        }

        public async Task<List<AdminPurchaseRequestResponseRawModel>> GetPrDtoByPrIdAsync(int id)
        {
            string query = "Internal_PurchaseRequest_GetDtoById";

            var parameters = new DynamicParameters(
                new
                {
                    @id = id
                }
            );

            var res = await Context.Database.GetDbConnection().QueryAsync<AdminPurchaseRequestResponseRawModel>(query, parameters, commandType: CommandType.StoredProcedure);
            return res.ToList();
        }

        public async Task<List<AccountantPurchaseRequestPagingRawResponse>> AccountantGetPrPagingAsync(AccountantPurchaseRequestPagingModel request, bool isDirector)
        {
            string query = "Internal_PurchaseRequest_Accountant_GetPaging";

            var parameters = new DynamicParameters(
                new
                {
                    countSkip = request.PageIndex,
                    pageSize = request.PageSize,
                    keyword = request.Keyword,
                    userIds = request.UserIds,
                    departmentIds = request.DepartmentIds,
                    projectIds = request.ProjectIds,
                    statuses = request.Statuses,
                    isUrgent = request.IsUrgent,
                    isDirector = isDirector
                }
            );

            var res = await Context.Database.GetDbConnection().QueryAsync<AccountantPurchaseRequestPagingRawResponse>(query, parameters, commandType: CommandType.StoredProcedure);
            return res.ToList();
        }

        public async Task RemovePoByPrIdAsync(int purchaseRequestId)
        {
            string query = "Internal_PurchaseRequest_RemovePo_ById";

            var parameters = new DynamicParameters(
                new
                {
                    @id = purchaseRequestId
                }
            );

            await Context.Database.GetDbConnection().ExecuteAsync(query, parameters, commandType: CommandType.StoredProcedure);
        }
        public async Task<List<HRPurchaseRequestPagingResponseRaw>> HRGetAllWithPagingAsync(HRPurchaseRequestPagingRequest model)
        {
            string query = @"Internal_GetPurchaseRequestPaging";

            var parameters = new DynamicParameters(
                new
                {
                    countSkip = model.PageIndex,
                    pageSize = model.PageSize,
                    keyword = model.Keyword ?? "",
                    userIds = model.UserIds,
                    departmentIds = model.DepartmentIds,
                    projectIds = model.ProjectIds,
                    status = model.Status,
                    isUrgent = model.IsUrgent,
                }
            );

            var response = await Context.Database.GetDbConnection().QueryAsync<HRPurchaseRequestPagingResponseRaw>(query, parameters, commandType: CommandType.StoredProcedure);
            return response.ToList();
        }

        public async Task<List<PurchaseRequestFileResponseRaw>> GetPurchaseRequestFileAsync(int purchaseOrderId)
        {
            string query = @"Internal_GetFileFromPO";

            var parameters = new DynamicParameters(
                new
                {
                    purchaseOrderId
                }
            );

            var response = await Context.Database.GetDbConnection().QueryAsync<PurchaseRequestFileResponseRaw>(query, parameters, commandType: CommandType.StoredProcedure);
            return response.ToList();
        }
        public async Task<List<PuchaseRequestDropdown>> GetPurchaseRequestsAsync(int purchaseOrderId,bool isCompensationPO)
        {
            string query = @"Internal_GetPurchaseRequestDropdown_PO";
            var parameters = new DynamicParameters(
                new
                {
                    purchaseOrderId,
                    isCompensationPO
                }
            );
            var response = await Context.Database.GetDbConnection().QueryAsync<PuchaseRequestDropdown>(query, parameters, commandType: CommandType.StoredProcedure);
            return response.ToList();
        }

        public async Task<List<PuchaseRequestDropdown>> GetPurchaseRequestsForEBAsync(int exportBillId)
        {
            string query = @"Internal_GetPurchaseRequestDropdown_EB";
            var parameters = new DynamicParameters(
                new
                {
                    exportBillId
                }
            );
            var response = await Context.Database.GetDbConnection().QueryAsync<PuchaseRequestDropdown>(query, parameters, commandType: CommandType.StoredProcedure);
            return response.ToList();
        }

        public async Task<List<MultiplePurchaseRequestDtoModel>> GetByIdsAsync(List<int> purchaseRequestIds)
        {
            string query = "Internal_PurchaseRequest_GetDtoByIds";

            var parameters = new DynamicParameters(
                new
                {
                    @ids = purchaseRequestIds.JoinComma(true)
                }
            );

            var res = await Context.Database.GetDbConnection().QueryAsync<MultiplePurchaseRequestDtoModel>(query, parameters, commandType: CommandType.StoredProcedure);
            return res.ToList();
        }

        public async Task AdminReviewPurchaseRequestsAsync(List<int> purchaseRequestIds, EPurchaseRequestStatus reviewStatus)
        {
            string query = "Internal_PurchaseRequest_AdminReviewRequests";

            var parameters = new DynamicParameters(
                new
                {
                    @ids = purchaseRequestIds.JoinComma(true),
                    @reviewStatus = (int)reviewStatus
                }
            );

            await Context.Database.GetDbConnection().ExecuteAsync(query, parameters, commandType: CommandType.StoredProcedure);
        }

        public async Task UpdatePurchaseRequestAsync(int prId, string? reviewNote, EPurchaseRequestStatus reviewStatus)
        {
            string query = "Internal_PurchaseRequest_UpdateById";
            var parameters = new DynamicParameters(
                new
                {
                    @id = prId,
                    @reviewNote = reviewNote,
                    @reviewStatus = (int)reviewStatus
                }
            );

            await Context.Database.GetDbConnection().ExecuteAsync(query, parameters, commandType: CommandType.StoredProcedure);
        }
    }
}