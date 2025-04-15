using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Models;
using InternalPortal.ApplicationCore.Models.PaymentRequest;
namespace InternalPortal.ApplicationCore.Interfaces.Business
{
    public interface IPaymentRequestDetailService
    {
        Task<CombineResponseModel<PaymentRequestDetail>> CreateAsync(PaymentRequestDetailModel model,int paymentRequestId,int userId);
        Task<CombineResponseModel<PaymentRequestDetail>> DeleteAsync(int id,int userId);
        Task<CombineResponseModel<PaymentRequestDetail>> UpdateAsync(PaymentRequestDetailModel model,int userId,int id);
        Task<CombineResponseModel<PaymentRequestDetail>> AccountantUpdateAsync(PaymentRequestDetailModel model, string email, int id);
    }
}
