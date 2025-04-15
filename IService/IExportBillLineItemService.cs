using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Models;

namespace InternalPortal.ApplicationCore.Interfaces.Business
{
    public interface IExportBillLineItemService
    {
        Task<CombineResponseModel<ExportBillLineItem>> DeleteAsync(int id);
        Task<CombineResponseModel<ExportBillLineItem>> UpdateAsync(int id, int quantity);
    }
}
