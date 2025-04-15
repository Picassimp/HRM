using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Models;
using InternalPortal.ApplicationCore.Models.PurchaseOrder.PODetail;

namespace InternalPortal.ApplicationCore.Interfaces.Business
{
    public interface IPODetailService
    {
        Task<CombineResponseModel<Podetail>> UpdateAsync(int id,PODetailRequest request);
        Task<CombineResponseModel<Podetail>> DeleteAsync(int id);
        Task<CombineResponseModel<Podetail>> ReceiveAsync(int id, int quantity);
    }
}
