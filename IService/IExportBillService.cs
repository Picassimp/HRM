using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Models;
using InternalPortal.ApplicationCore.Models.ExportBill;
using InternalPortal.ApplicationCore.Models.PurchaseOrder.PurchaseRequest;

namespace InternalPortal.ApplicationCore.Interfaces.Business
{
    public interface IExportBillService
    {
        Task<PagingResponseModel<ExportBillPagingResponse>> GetAllWithPagingAsync(ExportBillPagingModel model);
        Task<CombineResponseModel<ExportBillRespone>> GetExportBillDetailAsync(string email,int exportBillId);
        Task<CombineResponseModel<List<ExportBillLineItemResponse>>> GetExportBillLineItemAsync(string email,int exportBillDetailId);
        Task<CombineResponseModel<ExportBill>> CreateExportBillFromRequestAsync(int id, int userId, string email);
        Task<CombineResponseModel<ExportBill>> AddItemFromRequestAsync(int exportBillId, ItemCreateRequest request);
        Task<CombineResponseModel<ExportBill>> CreateAsync(int userId, string email,ExportBillRequest request);
        Task<CombineResponseModel<ExportBill>> DeleteAsync(int id,string email);
        Task<CombineResponseModel<ExportBill>> UpdateAsync(int id,string email, ExportBillRequest request);
        Task<PagingResponseModel<ExportBillPagingResponse>> AccountantGetAllWithPagingAsync(ExportBillPagingModel model);
        Task<PagingResponseModel<ExportBillPagingResponse>> DirectorGetAllWithPagingAsync(ExportBillPagingModel model);
        Task<CombineResponseModel<ExportBill>> ExportAsync(int id,string email);
    }
}
