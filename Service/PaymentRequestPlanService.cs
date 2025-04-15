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
    public class PaymentRequestPlanService : IPaymentRequestPlanService
    {
        private readonly IUnitOfWork _unitOfWork;
        public PaymentRequestPlanService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<CombineResponseModel<PaymentRequestPlan>> ChangeStatusAsync(int id, PaymentRequestPlanChangeStatusModel model, string email)
        {
            var res = new CombineResponseModel<PaymentRequestPlan>();
            if (!model.PaymentDate.HasValue)
            {
                res.ErrorMessage = "Chưa nhập ngày thanh toán";
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
                    res.ErrorMessage = "Bạn không có quyền duyệt";
                    return res;
                }
            }
            var paymentRequestPlan = await _unitOfWork.PaymentRequestPlanRepository.GetByIdAsync(id);
            if (paymentRequestPlan == null)
            {
                res.ErrorMessage = "Không tồn tại đợt thanh toán";
                return res;
            }
            var isClose = paymentRequestPlan.PaymentRequest.Isclose ?? false;
            if(isClose)
            {
                res.ErrorMessage = "Đơn đã đóng";
                return res;
            }
            if(model.PaymentDate.Value.Date < paymentRequestPlan.CreateDate.Date)
            {
                res.ErrorMessage = "Ngày thanh toán không nhỏ hơn ngày gửi đơn";
                return res;
            }
            if(paymentRequestPlan.PaymentRequest.ReviewStatus != (int)EPaymentRequestStatus.DirectorApproved)
            {
                res.ErrorMessage = "Đơn yêu cầu chưa được giám đốc duyệt";
                return res;
            }
            if ((bool)paymentRequestPlan.PaymentStatus)
            {
                res.ErrorMessage = "Đợt nảy đã thanh toán";
                return res;
            }
            paymentRequestPlan.PaymentStatus = true;
            paymentRequestPlan.PaymentDate = model.PaymentDate;
            paymentRequestPlan.HrNote = model.Note;
            paymentRequestPlan.UpdateDate = DateTime.UtcNow.UTCToIct();
            res.Status = true;
            res.Data = paymentRequestPlan;
            return res;
        }

        public async Task<CombineResponseModel<PaymentRequestPlan>> CreateAsync(int userId, int paymentRequestId, PaymentRequestPlanRequest request)
        {
            var res = new CombineResponseModel<PaymentRequestPlan>();   
            if(request.IsUrgent && !request.ProposePaymentDate.HasValue)
            {
                res.ErrorMessage = "Vui lòng nhập ngày đề nghị thanh toán";
                return res;
            }
            var paymentRequest = await _unitOfWork.PaymentRequestRepository.GetByIdAsync(paymentRequestId);
            if(paymentRequest == null)
            {
                res.ErrorMessage = "Không tồn tại đơn yêu cầu";
                return res;
            }
            if (request.ProposePaymentDate.HasValue)
            {
                if(request.ProposePaymentDate.Value.Date < paymentRequest.CreateDate.Date)
                {
                    res.ErrorMessage = "Ngày đề nghị không nhỏ hơn ngày gửi đơn";
                    return res;
                }
            }
            var isClose = paymentRequest.Isclose ?? false;
            if (isClose)
            {
                res.ErrorMessage = "Đơn đã đóng";
                return res;
            }
            if (paymentRequest.ReviewStatus != (int)EPaymentRequestStatus.Pending && paymentRequest.ReviewStatus
                != (int)EPaymentRequestStatus.AccountantUpdateRequest && paymentRequest.ReviewStatus
                != (int)EPaymentRequestStatus.DirectorUpdateRequest &&
                (paymentRequest.ReviewStatus != (int)EPaymentRequestStatus.ManagerApproved && paymentRequest.CreateUserId == paymentRequest.ReviewUserId))
            {
                res.ErrorMessage = "Đơn yêu cầu đã được duyệt";
                return res;
            }
            if (paymentRequest.CreateUserId !=  userId)
            {
                res.ErrorMessage = "Đơn này không phải của bạn";
                return res;
            }
            var totalPriceRequest = Math.Round((decimal)paymentRequest.PaymentRequestDetails.Sum(x => (x.Quantity * x.Price) + (x.VatPrice == 0 ? (x.Quantity * x.Price) * x.Vat / 100 : x.VatPrice)));
            var totalPricePaymentPlan = Math.Round(paymentRequest.PaymentRequestPlans.Sum(t => t.PaymentAmount));
            if (totalPriceRequest == totalPricePaymentPlan)
            {
                res.ErrorMessage = "Các đợt thanh toán đã đủ";
                return res;
            }
            if(totalPricePaymentPlan + request.PaymentAmount > totalPriceRequest)// nếu tổng tiền sau khi thêm lớn hơn tổng tiền của yêu cầu
            {
                res.ErrorMessage = "Tổng tiền các đợt thanh toán không lớn hơn tổng tiển của yêu cầu";
                return res;
            }
            var paymentRequestPlan = new PaymentRequestPlan()
            {
                PaymentRequestId = paymentRequest.Id,
                PaymentType = request.PaymentType,
                PaymentStatus = false,
                IsUrgent = request.IsUrgent,
                ProposePaymentDate = request.ProposePaymentDate ,
                PaymentDate = null,
                PaymentAmount = Math.Round(request.PaymentAmount),
                Note = request.Note ?? string.Empty,
                CreateDate = DateTime.UtcNow.UTCToIct()
            };
            res.Status = true;
            res.Data = paymentRequestPlan;
            return res;
        }

        public async Task<CombineResponseModel<PaymentRequestPlan>> DeleteAsync(int userId, int id)
        {
            var res = new CombineResponseModel<PaymentRequestPlan>();   
            var paymentRequestPlan = await _unitOfWork.PaymentRequestPlanRepository.GetByIdAsync(id);
            if(paymentRequestPlan == null)
            {
                res.ErrorMessage = "Không tồn tại đợt thanh toán";
                return res;
            }
            var isClose = paymentRequestPlan.PaymentRequest.Isclose ?? false;
            if (isClose)
            {
                res.ErrorMessage = "Đơn đã đóng";
                return res;
            }
            if (paymentRequestPlan.PaymentRequest.ReviewStatus != (int)EPaymentRequestStatus.Pending && paymentRequestPlan.PaymentRequest.ReviewStatus
                != (int)EPaymentRequestStatus.AccountantUpdateRequest && paymentRequestPlan.PaymentRequest.ReviewStatus
                != (int)EPaymentRequestStatus.DirectorUpdateRequest
                && (paymentRequestPlan.PaymentRequest.ReviewStatus != (int)EPaymentRequestStatus.ManagerApproved && paymentRequestPlan.PaymentRequest.CreateUserId == paymentRequestPlan.PaymentRequest.ReviewUserId))
            {
                res.ErrorMessage = "Đơn yêu cầu đã duyệt";
                return res;
            }
            if (paymentRequestPlan.PaymentRequest.CreateUserId != userId)
            {
                res.ErrorMessage = "Đơn này không phải của bạn";
                return res;
            }
            res.Status = true;
            res.Data = paymentRequestPlan;
            return res;
        }

        public async Task<CombineResponseModel<PaymentRequestPlan>> UpdateAsync(int userId, int id, PaymentRequestPlanRequest request)
        {
            var res = new CombineResponseModel<PaymentRequestPlan>();
            if (request.IsUrgent && !request.ProposePaymentDate.HasValue)
            {
                res.ErrorMessage = "Vui lòng nhập ngày đề nghị thanh toán";
                return res;
            }
            var paymentRequestPlan = await _unitOfWork.PaymentRequestPlanRepository.GetByIdAsync(id);
            if (paymentRequestPlan == null)
            {
                res.ErrorMessage = "Không tồn tại đợt thanh toán";
                return res;
            }
            if (request.ProposePaymentDate.HasValue)
            {
                if (request.ProposePaymentDate.Value.Date < paymentRequestPlan.PaymentRequest.CreateDate.Date)
                {
                    res.ErrorMessage = "Ngày đề nghị không nhỏ hơn ngày gửi đơn";
                    return res;
                }
            }
            var isClose = paymentRequestPlan.PaymentRequest.Isclose ?? false;
            if (isClose)
            {
                res.ErrorMessage = "Đơn đã đóng";
                return res;
            }
            if (paymentRequestPlan.PaymentRequest.ReviewStatus != (int)EPaymentRequestStatus.Pending && paymentRequestPlan.PaymentRequest.ReviewStatus
                != (int)EPaymentRequestStatus.AccountantUpdateRequest && paymentRequestPlan.PaymentRequest.ReviewStatus
                != (int)EPaymentRequestStatus.DirectorUpdateRequest
                && (paymentRequestPlan.PaymentRequest.ReviewStatus != (int)EPaymentRequestStatus.ManagerApproved && paymentRequestPlan.PaymentRequest.CreateUserId == paymentRequestPlan.PaymentRequest.ReviewUserId))
            {
                res.ErrorMessage = "Đơn đã duyệt";
                return res;
            }
            if (paymentRequestPlan.PaymentRequest.CreateUserId != userId)
            {
                res.ErrorMessage = "Đơn này không phải của bạn";
                return res;
            }
            var totalPriceRequest =Math.Round((decimal)paymentRequestPlan.PaymentRequest.PaymentRequestDetails.Sum(t => (t.Quantity * t.Price) + (t.VatPrice == 0 ? (t.Quantity * t.Price) * t.Vat / 100 : t.VatPrice)));
            var totalPricePaymentPlan = paymentRequestPlan.PaymentRequest.PaymentRequestPlans.Where(t => t.Id != id).Sum(t => t.PaymentAmount);//tính tổng tiền các đợt không bao gồm đợt update
            if(totalPricePaymentPlan + request.PaymentAmount > totalPriceRequest)  // nếu tổng tiền sau khi update lớn hơn tổng tiền yêu cầu
            {
                res.ErrorMessage = "Tổng tiển các đợt thanh toán không lớn hơn tổng tiền của yêu cầu";
                return res;
            }
            paymentRequestPlan.PaymentType = request.PaymentType;
            paymentRequestPlan.IsUrgent = request.IsUrgent;
            paymentRequestPlan.ProposePaymentDate = request.ProposePaymentDate;
            paymentRequestPlan.PaymentAmount = request.PaymentAmount;
            paymentRequestPlan.Note = request.Note;
            paymentRequestPlan.UpdateDate = DateTime.UtcNow.UTCToIct();
            res.Status = true;
            res.Data = paymentRequestPlan;
            return res;
        }
        public async Task<PagingResponseModel<PaymentRequestPlanPagingResponse>> GetAllWithPagingAsync(PaymentRequestPlanPagingModel model)
        {
            var responseRaw = await _unitOfWork.PaymentRequestPlanRepository.GetPagingAsync(model);
            var response = responseRaw.Count > 0 ? responseRaw.Select(t =>
            {
                var item = new PaymentRequestPlanPagingResponse()
                {
                    PaymentId = t.PaymentId,
                    RequestName = t.RequestName,
                    PaymentRequestId  = t.PaymentRequestId,
                    PaymentDate = t.PaymentDate,
                    ProposePaymentDate = t.ProposePaymentDate,
                    PaymentAmount = t.PaymentAmount,
                    IsUrgent = t.IsUrgent,
                    PaymentStatus = t.PaymentStatus,
                    ReviewStatus = t.ReviewStatus,
                    ReviewStatusName = CommonHelper.GetDescription((EPaymentRequestStatus)t.ReviewStatus),
                    Batch = t.Batch,
                    CreateDate = t.CreateDate,
                    TotalRecord = t.TotalRecord
                };
                return item;
            }).ToList() : new List<PaymentRequestPlanPagingResponse>();
            var totalRecord = response.Count > 0 ? response.FirstOrDefault().TotalRecord : 0;
            var res = new PagingResponseModel<PaymentRequestPlanPagingResponse>()
            {
                Items = response,
                TotalRecord = totalRecord
            };
            return res;
        }

        public async Task<CombineResponseModel<PaymentRequestPlan>> AccountantUpdateAsync(string email, int id, PaymentRequestPlanRequest request)
        {
            var res = new CombineResponseModel<PaymentRequestPlan>();
            var paymentRequestPlan = await _unitOfWork.PaymentRequestPlanRepository.GetByIdAsync(id);
            if (paymentRequestPlan == null)
            {
                res.ErrorMessage = "Không tồn tại đợt thanh toán";
                return res;
            }
            if (paymentRequestPlan.PaymentRequest.ReviewStatus != (int)EPaymentRequestStatus.ManagerApproved)
            {
                res.ErrorMessage = "Trạng thái đơn không cho phép thực hiện yêu cầu này";
                return res;
            }
            var isClose = paymentRequestPlan.PaymentRequest.Isclose ?? false;
            if (isClose)
            {
                res.ErrorMessage = "Đơn đã đóng";
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
            var totalPriceRequest = Math.Round((decimal)paymentRequestPlan.PaymentRequest.PaymentRequestDetails.Sum(t => (t.Quantity * t.Price) + (t.VatPrice == 0 ? (t.Quantity * t.Price) * t.Vat / 100 : t.VatPrice)));
            var totalPricePaymentPlan = paymentRequestPlan.PaymentRequest.PaymentRequestPlans.Where(t => t.Id != id).Sum(t => t.PaymentAmount);//tính tổng tiền các đợt không bao gồm đợt update
            if (totalPricePaymentPlan + request.PaymentAmount > totalPriceRequest)  // nếu tổng tiền sau khi update lớn hơn tổng tiền yêu cầu
            {
                res.ErrorMessage = "Tổng tiển các đợt thanh toán không lớn hơn tổng tiền của yêu cầu";
                return res;
            }
            paymentRequestPlan.PaymentAmount = request.PaymentAmount;
            paymentRequestPlan.UpdateDate = DateTime.UtcNow.UTCToIct();
            res.Status = true;
            res.Data = paymentRequestPlan;
            return res;
        }

        public async Task<CombineResponseModel<PaymentRequestPlan>> AccountantCreateAsync(string email, int paymentRequestId, PaymentRequestPlanRequest request)
        {
            var res = new CombineResponseModel<PaymentRequestPlan>();
            if (request.IsUrgent && !request.ProposePaymentDate.HasValue)
            {
                res.ErrorMessage = "Vui lòng nhập ngày đề nghị thanh toán";
                return res;
            }
            var paymentRequest = await _unitOfWork.PaymentRequestRepository.GetByIdAsync(paymentRequestId);
            if (paymentRequest == null)
            {
                res.ErrorMessage = "Không tồn tại đơn yêu cầu";
                return res;
            }
            if (request.ProposePaymentDate.HasValue)
            {
                if (request.ProposePaymentDate.Value.Date < paymentRequest.CreateDate.Date)
                {
                    res.ErrorMessage = "Ngày đề nghị không nhỏ hơn ngày gửi đơn";
                    return res;
                }
            }
            if (paymentRequest.ReviewStatus != (int)EPaymentRequestStatus.ManagerApproved)
            {
                res.ErrorMessage = "Đơn yêu cầu đã được duyệt";
                return res;
            }
            var isClose = paymentRequest.Isclose ?? false;
            if (isClose)
            {
                res.ErrorMessage = "Đơn đã đóng";
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
            var totalPriceRequest = Math.Round((decimal)paymentRequest.PaymentRequestDetails.Sum(x => (x.Quantity * x.Price) + (x.VatPrice == 0 ? (x.Quantity * x.Price) * x.Vat / 100 : x.VatPrice)));
            var totalPricePaymentPlan = Math.Round(paymentRequest.PaymentRequestPlans.Sum(t => t.PaymentAmount));
            if (totalPriceRequest == totalPricePaymentPlan)
            {
                res.ErrorMessage = "Các đợt thanh toán đã đủ";
                return res;
            }
            if (totalPricePaymentPlan + request.PaymentAmount > totalPriceRequest)// nếu tổng tiền sau khi thêm lớn hơn tổng tiền của yêu cầu
            {
                res.ErrorMessage = "Tổng tiền các đợt thanh toán không lớn hơn tổng tiển của yêu cầu";
                return res;
            }
            var paymentRequestPlan = new PaymentRequestPlan()
            {
                PaymentRequestId = paymentRequest.Id,
                PaymentType = request.PaymentType,
                PaymentStatus = false,
                IsUrgent = request.IsUrgent,
                ProposePaymentDate = request.ProposePaymentDate,
                PaymentDate = null,
                PaymentAmount = Math.Round(request.PaymentAmount),
                Note = request.Note ?? string.Empty,
                CreateDate = DateTime.UtcNow.UTCToIct()
            };
            res.Status = true;
            res.Data = paymentRequestPlan;
            return res;
        }

        public async Task<CombineResponseModel<PaymentRequestPlan>> AccountantDeleteAsync(string email, int id)
        {
            var res = new CombineResponseModel<PaymentRequestPlan>();
            var paymentRequestPlan = await _unitOfWork.PaymentRequestPlanRepository.GetByIdAsync(id);
            if (paymentRequestPlan == null)
            {
                res.ErrorMessage = "Không tồn tại đợt thanh toán";
                return res;
            }
            if (paymentRequestPlan.PaymentRequest.ReviewStatus != (int)EPaymentRequestStatus.ManagerApproved)
            {
                res.ErrorMessage = "Đơn yêu cầu đã duyệt";
                return res;
            }
            var isClose = paymentRequestPlan.PaymentRequest.Isclose ?? false;
            if (isClose)
            {
                res.ErrorMessage = "Đơn đã đóng";
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
            res.Status = true;
            res.Data = paymentRequestPlan;
            return res;
        }

        public async Task<CombineResponseModel<PaymentRequestPlan>> RefundAsync(string email, int id)
        {
            var res = new CombineResponseModel<PaymentRequestPlan>();
            var paymentRequestPlan = await _unitOfWork.PaymentRequestPlanRepository.GetByIdAsync(id);
            if (paymentRequestPlan == null)
            {
                res.ErrorMessage = "Không tồn tại đợt thanh toán";
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
            var isClose = paymentRequestPlan.PaymentRequest.Isclose ?? false;
            if (isClose)
            {
                res.ErrorMessage = "Đơn đã đóng";
                return res;
            }
            var paymentStatus = paymentRequestPlan.PaymentStatus ?? false;
            if (!paymentStatus)
            {
                res.ErrorMessage = "Đợt này chưa thanh toán";
                return res;
            }
            if(paymentRequestPlan.PaymentRequest.ReviewStatus != (int)EPaymentRequestStatus.Cancel)
            {
                res.ErrorMessage = "Trạng thái đơn không cho phép hoàn tiền";
                return res;
            }
            var isRefund = paymentRequestPlan.IsRefund ?? false;
            if (isRefund)
            {
                res.ErrorMessage = "Đợt này đã hoàn tiền";
                return res;
            }
            paymentRequestPlan.IsRefund = true;
            paymentRequestPlan.UpdateDate = DateTime.UtcNow.UTCToIct();
            res.Status = true;
            res.Data = paymentRequestPlan;
            return res;
        }
    }
}
