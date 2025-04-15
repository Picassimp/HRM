using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Models;

namespace InternalPortal.ApplicationCore.Interfaces.Business
{
    public interface IExportBillDetailService
    {
        Task<CombineResponseModel<ExportBillDetail>> DeleteAsync(int id);
    }
}
