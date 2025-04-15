using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Models;

namespace InternalPortal.ApplicationCore.Interfaces.Business
{
    public interface IPOPRLineItemService
    {
        Task<CombineResponseModel<PoprlineItem>> DeleteAsync(int id);
        Task<CombineResponseModel<PoprlineItem>> UpdateAsync(int id, int quantity);
        Task<CombineResponseModel<PoprlineItem>> ReceiveAsync(int id,int quantity);
        Task<CombineResponseModel<PoprlineItem>> LackReceiveAsync(int id);
    }
}
