using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Enums;
using InternalPortal.ApplicationCore.Helpers;
using InternalPortal.ApplicationCore.Interfaces;
using InternalPortal.ApplicationCore.Interfaces.Business;
using InternalPortal.ApplicationCore.Models;
using InternalPortal.ApplicationCore.Models.PaymentRequest;
using static InternalPortal.ApplicationCore.ValueObjects.Global;

namespace InternalPortal.Infrastructure.Services.Business
{
    public class PaymentRequestDetailService : IPaymentRequestDetailService
    {
        private readonly IUnitOfWork _unitOfWork;
        public PaymentRequestDetailService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<CombineResponseModel<PaymentRequestDetail>> AccountantUpdateAsync(PaymentRequestDetailModel model, string email, int id)
        {
            var res = new CombineResponseModel<PaymentRequestDetail>();
            var paymentRequestDetail = await _unitOfWork.PaymentRequestDetailRepository.GetByIdAsync(id);
            if (paymentRequestDetail == null)
            {
                res.ErrorMessage = "Không tồn tại chi tiết yêu cầu";
                return res;
            }
            var accountantEmails = await _unitOfWork.GlobalConfigurationRepository.GetByNameAsync(Constant.PaymentRequestAccountEmails);
            if (accountantEmails == null || string.IsNullOrEmpty(accountantEmails.Value))
            {
                res.ErrorMessage = "Người dùng chưa được config";
                return res;
            }
            var accountantEmail = accountantEmails.Value.Split(",").Select(t => t.Trim()).FirstOrDefault(y => y.Equals(email, StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrEmpty(accountantEmail))
            {
                res.ErrorMessage = "Người dùng chưa được config";
                return res;
            }
            else
            {
                if (!accountantEmail.Equals(email, StringComparison.OrdinalIgnoreCase))
                {
                    res.ErrorMessage = "Bạn không có quyền thực hiện yêu cầu này";
                    return res;
                }
            }
            if (paymentRequestDetail.PaymentRequest.ReviewStatus != (int)EPaymentRequestStatus.ManagerApproved)
            {
                res.ErrorMessage = "Trạng thái đơn không cho phép thực hiện yêu cầu này";
                return res;
            }
            paymentRequestDetail.Quantity = model.Quantity;
            paymentRequestDetail.Price = Math.Round(model.Price);
            paymentRequestDetail.Vat = model.Vat;
            paymentRequestDetail.VatPrice = model.VatPrice;
            paymentRequestDetail.UpdateDate = DateTime.UtcNow.UTCToIct();
            res.Status = true;
            res.Data = paymentRequestDetail;
            return res;
        }

        public async Task<CombineResponseModel<PaymentRequestDetail>> CreateAsync(PaymentRequestDetailModel model, int paymentRequestId, int userId)
        {
            var res = new CombineResponseModel<PaymentRequestDetail>(); 
            if(model.Quantity <= 0)
            {
                res.ErrorMessage = "Số lượng không hợp lệ";
                return res;
            }
            var paymentRequest = await _unitOfWork.PaymentRequestRepository.GetByIdAsync(paymentRequestId);
            if(paymentRequest == null)
            {
                res.ErrorMessage = "Không tồn tại đơn yêu cầu";
                return res;
            }
            if(paymentRequest.CreateUserId != userId)
            {
                res.ErrorMessage = "Đơn này không phải của bạn";
                return res;
            }
            if(paymentRequest.ReviewStatus != (int)EPaymentRequestStatus.Pending && paymentRequest.ReviewStatus 
                != (int)EPaymentRequestStatus.AccountantUpdateRequest && paymentRequest.ReviewStatus 
                != (int)EPaymentRequestStatus.DirectorUpdateRequest
                && (paymentRequest.ReviewStatus != (int)EPaymentRequestStatus.ManagerApproved && paymentRequest.CreateUserId == paymentRequest.ReviewUserId))
            {
                res.ErrorMessage = "Đơn đã duyệt";
                return res;
            }
            var paymentRequestDetail = new PaymentRequestDetail()
            {
                PaymentRequestId = paymentRequest.Id,
                Name = model.Name,
                Quantity = model.Quantity,
                Price = Math.Round(model.Price),
                Vat = model.Vat,
                VatPrice = model.VatPrice,
                CreateDate = DateTime.UtcNow.UTCToIct()
            };
            res.Status = true;
            res.Data = paymentRequestDetail;
            return res;
        }

        public async Task<CombineResponseModel<PaymentRequestDetail>> DeleteAsync(int id, int userId)
        {
            var res = new CombineResponseModel<PaymentRequestDetail>();
            var paymentRequestDetail = await _unitOfWork.PaymentRequestDetailRepository.GetByIdAsync(id);
            if (paymentRequestDetail == null)
            {
                res.ErrorMessage = "Không tồn tại chi tiết yêu cầu";
                return res;
            }
            if(paymentRequestDetail.PaymentRequest.CreateUserId != userId)
            {
                res.ErrorMessage = "Đơn này không phải của bạn";
                return res;
            }
            if (paymentRequestDetail.PaymentRequest.ReviewStatus != (int)EPaymentRequestStatus.Pending && paymentRequestDetail.PaymentRequest.ReviewStatus
                != (int)EPaymentRequestStatus.AccountantUpdateRequest && paymentRequestDetail.PaymentRequest.ReviewStatus
                != (int)EPaymentRequestStatus.DirectorUpdateRequest &&
                (paymentRequestDetail.PaymentRequest.ReviewStatus != (int)EPaymentRequestStatus.ManagerApproved && paymentRequestDetail.PaymentRequest.CreateUserId == paymentRequestDetail.PaymentRequest.ReviewUserId))
            {
                res.ErrorMessage = "Đơn đã duyệt";
                return res;
            }
            res.Status = true;
            res.Data = paymentRequestDetail;
            return res;
        }

        public async Task<CombineResponseModel<PaymentRequestDetail>> UpdateAsync(PaymentRequestDetailModel model,int userId,int id)
        {
            var res = new CombineResponseModel<PaymentRequestDetail>();
            var paymentRequestDetail = await _unitOfWork.PaymentRequestDetailRepository.GetByIdAsync(id);
            if (paymentRequestDetail == null)
            {
                res.ErrorMessage = "Không tồn tại chi tiết yêu cầu";
                return res;
            }
            if (paymentRequestDetail.PaymentRequest.CreateUserId != userId)
            {
                res.ErrorMessage = "Đơn này không phải của bạn";
                return res;
            }
            if (paymentRequestDetail.PaymentRequest.ReviewStatus != (int)EPaymentRequestStatus.Pending && paymentRequestDetail.PaymentRequest.ReviewStatus
               != (int)EPaymentRequestStatus.AccountantUpdateRequest && paymentRequestDetail.PaymentRequest.ReviewStatus
               != (int)EPaymentRequestStatus.DirectorUpdateRequest &&
               (paymentRequestDetail.PaymentRequest.ReviewStatus != (int)EPaymentRequestStatus.ManagerApproved && paymentRequestDetail.PaymentRequest.CreateUserId == paymentRequestDetail.PaymentRequest.ReviewUserId)) 
            {
                res.ErrorMessage = "Đơn đã duyệt";
                return res;
            }
            paymentRequestDetail.Name = model.Name;
            paymentRequestDetail.Quantity = model.Quantity;
            paymentRequestDetail.Price = Math.Round(model.Price);
            paymentRequestDetail.Vat = model.Vat;
            paymentRequestDetail.VatPrice = model.VatPrice;
            paymentRequestDetail.UpdateDate = DateTime.UtcNow.UTCToIct();
            res.Status = true;
            res.Data = paymentRequestDetail;
            return res;
        }
    }
}
