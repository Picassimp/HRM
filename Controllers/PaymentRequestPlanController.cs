using InternalPortal.ApplicationCore.Enums;
using InternalPortal.ApplicationCore.Helpers;
using InternalPortal.ApplicationCore.Interfaces;
using InternalPortal.ApplicationCore.Interfaces.Business;
using InternalPortal.ApplicationCore.Models.PaymentRequest;
using InternalPortal.ApplicationCore.ValueObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using static InternalPortal.ApplicationCore.ValueObjects.Global;

namespace InternalPortal.API.Controllers
{
    public class PaymentRequestPlanController : BaseApiController
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IPaymentRequestPlanService _paymentRequestPlanService;
        public PaymentRequestPlanController(IUnitOfWork unitOfWork,
            IPaymentRequestPlanService paymentRequestPlanService)
        {
            _unitOfWork = unitOfWork;
            _paymentRequestPlanService = paymentRequestPlanService; 
        }
        [HttpGet]
        [Route("paymentRequest/{id}")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> UserGetAsync([FromRoute] int id)
        {
            var user = GetCurrentUser();
            var paymentRequest = await _unitOfWork.PaymentRequestRepository.GetByIdAsync(id);
            if (paymentRequest == null)
            {
                return ErrorResult("Không tồn tại đơn yêu cầu");
            }
            if (paymentRequest.CreateUserId != user.Id)
            {
                return ErrorResult("Đơn này không phải của bạn");
            }
            var totalPriceRequest = paymentRequest.PaymentRequestDetails.Count > 0 ?
                paymentRequest.PaymentRequestDetails.Sum(t => Math.Round((decimal)((t.Quantity * t.Price) + (t.VatPrice == 0 ? t.Quantity * t.Price * t.Vat / 100 : t.VatPrice)))) : 0;
            var totalPricePaymentPlan = paymentRequest.PaymentRequestPlans.Count > 0 ? paymentRequest.PaymentRequestPlans.Sum(t => Math.Round(t.PaymentAmount)) : 0;
            var response = new PaymentRequestPlanResponse()
            {
                ReviewStatus = paymentRequest.ReviewStatus,
                TotalPrice = totalPriceRequest - totalPricePaymentPlan,
                PaymentRequestPlanDetailResponses = paymentRequest.PaymentRequestPlans.Count > 0 ? paymentRequest.PaymentRequestPlans.Select(t => new PaymentRequestPlanDetailResponse()
                {
                    Id = t.Id,
                    PaymentType = t.PaymentType,
                    PaymentTypeName = CommonHelper.GetDescription((EPaymentType)t.PaymentType),
                    PaymentStatus = (bool)t.PaymentStatus,
                    IsUrgent = (bool)t.IsUrgent,
                    ProposePaymentDate = t.ProposePaymentDate.HasValue ? t.ProposePaymentDate : null,
                    PaymentDate = t.PaymentDate.HasValue ? t.PaymentDate : null,
                    PaymentAmount = t.PaymentAmount,
                    Note = t.Note,
                    HrNote = t.HrNote
                }).ToList() : new List<PaymentRequestPlanDetailResponse>(),
                IsDirectorApprove = paymentRequest.ReviewStatus == (int)EPaymentRequestStatus.DirectorApproved ? true : false,
            };
            return SuccessResult(response);
        }
        [HttpPost]
        [Route("paymentrequest/{paymentRequestId}/add")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> CreateAsync([FromRoute] int paymentRequestId, [FromBody] PaymentRequestPlanRequest request)
        {
            var user = GetCurrentUser();
            var res = await _paymentRequestPlanService.CreateAsync(user.Id, paymentRequestId, request);
            if(!res.Status || !string.IsNullOrEmpty(res.ErrorMessage) || res.Data == null)
            {
                return ErrorResult(res.ErrorMessage ?? "Lỗi khi tạo đợt thanh toán");
            }
            await _unitOfWork.PaymentRequestPlanRepository.CreateAsync(res.Data);
            await _unitOfWork.SaveChangesAsync();
            return SuccessResult("Tạo đợt thanh toán thành công");
        }
        [HttpDelete]
        [Route("{id}")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> DeleteAsync([FromRoute] int id)
        {
            var user = GetCurrentUser();
            var res = await _paymentRequestPlanService.DeleteAsync(user.Id,id);
            if (!res.Status || !string.IsNullOrEmpty(res.ErrorMessage) || res.Data == null)
            {
                return ErrorResult(res.ErrorMessage ?? "Lỗi khi xóa đợt thanh toán");
            }
            await _unitOfWork.PaymentRequestPlanRepository.DeleteAsync(res.Data);
            await _unitOfWork.SaveChangesAsync();
            return SuccessResult("Xóa đợt thanh toán thành công");
        }
        [HttpPut]
        [Route("{id}")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> UpdateAsync([FromRoute] int id, [FromBody] PaymentRequestPlanRequest request)
        {
            var user = GetCurrentUser();
            var res = await _paymentRequestPlanService.UpdateAsync(user.Id, id, request);
            if (!res.Status || !string.IsNullOrEmpty(res.ErrorMessage) || res.Data == null)
            {
                return ErrorResult(res.ErrorMessage ?? "Lỗi khi cập nhật đợt thanh toán");
            }
            await _unitOfWork.PaymentRequestPlanRepository.UpdateAsync(res.Data);
            await _unitOfWork.SaveChangesAsync();
            return SuccessResult("Cập nhật đợt thanh toán thành công");
        }
        #region Manager
        [HttpGet]
        [Route("manager/paymentRequest/{id}")]
        [Authorize(Policy = AuthorizationPolicies.ManagerRoleRequired)]
        public async Task<IActionResult> ManagerGetAsync([FromRoute] int id)
        {
            var user = GetCurrentUser();
            var paymentRequest = await _unitOfWork.PaymentRequestRepository.GetByIdAsync(id);
            if (paymentRequest == null)
            {
                return ErrorResult("Không tồn tại đơn yêu cầu");
            }
            if (paymentRequest.ReviewUserId != user.Id)
            {
                return ErrorResult("Bạn không phải manager của yêu cầu này");
            }
            var totalPriceRequest = paymentRequest.PaymentRequestDetails.Count > 0 ?
                paymentRequest.PaymentRequestDetails.Sum(t => Math.Round((decimal)((t.Quantity * t.Price) + (t.VatPrice == 0 ? t.Quantity * t.Price * t.Vat / 100 : t.VatPrice)))) : 0;
            var totalPricePaymentPlan = paymentRequest.PaymentRequestPlans.Count > 0 ? paymentRequest.PaymentRequestPlans.Sum(t => Math.Round(t.PaymentAmount)) : 0;
            var response = new PaymentRequestPlanResponse()
            {
                ReviewStatus = paymentRequest.ReviewStatus,
                TotalPrice = totalPriceRequest - totalPricePaymentPlan,
                PaymentRequestPlanDetailResponses = paymentRequest.PaymentRequestPlans.Count > 0 ? paymentRequest.PaymentRequestPlans.Select(t => new PaymentRequestPlanDetailResponse()
                {
                    Id = t.Id,
                    PaymentType = t.PaymentType,
                    PaymentTypeName = CommonHelper.GetDescription((EPaymentType)t.PaymentType),
                    PaymentStatus = (bool)t.PaymentStatus,
                    IsUrgent = (bool)t.IsUrgent,
                    ProposePaymentDate = t.ProposePaymentDate.HasValue ? t.ProposePaymentDate : null,
                    PaymentDate = t.PaymentDate.HasValue ? t.PaymentDate : null,
                    PaymentAmount = t.PaymentAmount,
                    Note = t.Note,
                    HrNote = t.HrNote,  
                }).ToList() : new List<PaymentRequestPlanDetailResponse>(),
                IsDirectorApprove = paymentRequest.ReviewStatus == (int)EPaymentRequestStatus.DirectorApproved ? true : false
            };
            return SuccessResult(response);
        }
        #endregion
        #region API chung cho Accountant và Director
        [HttpGet]
        [Route("view/paymentRequest/{id}")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> GetAsync([FromRoute] int id)
        {
            var user = GetCurrentUser();
            var names = new List<string>()
            {
                Constant.PaymentRequestAccountEmails,
                Constant.PaymentRequestDirectorEmails
            };
            var emails = await _unitOfWork.GlobalConfigurationRepository.GetByMultiNameAsync(names);
            if (emails.Count == 0)
            {
                return ErrorResult("Người dùng chưa được config");
            }
            var isHasAllowEmail = string.Join(",", emails.Select(t => t.Value)).Split(",").Any(y => y.Equals(user.Email, StringComparison.OrdinalIgnoreCase));
            if (!isHasAllowEmail)
            {
                return ErrorResult("Bạn không có quyền xem thông tin này");
            }
            var paymentRequest = await _unitOfWork.PaymentRequestRepository.GetByIdAsync(id);
            if (paymentRequest == null)
            {
                return ErrorResult("Không tồn tại đơn yêu cầu");
            }
            var totalPriceRequest = paymentRequest.PaymentRequestDetails.Count > 0 ?
                paymentRequest.PaymentRequestDetails.Sum(t => Math.Round((decimal)((t.Quantity * t.Price) + (t.VatPrice == 0 ? t.Quantity * t.Price * t.Vat / 100 : t.VatPrice)))) : 0;
            var totalPricePaymentPlan = paymentRequest.PaymentRequestPlans.Count > 0 ? paymentRequest.PaymentRequestPlans.Sum(t => Math.Round(t.PaymentAmount)) : 0;
            var response = new PaymentRequestPlanResponse()
            {
                ReviewStatus = paymentRequest.ReviewStatus,
                TotalPrice = totalPriceRequest - totalPricePaymentPlan,
                PaymentRequestPlanDetailResponses = paymentRequest.PaymentRequestPlans.Count > 0 ? paymentRequest.PaymentRequestPlans.Select(t => new PaymentRequestPlanDetailResponse()
                {
                    Id = t.Id,
                    PaymentType = t.PaymentType,
                    PaymentTypeName = CommonHelper.GetDescription((EPaymentType)t.PaymentType),
                    PaymentStatus = (bool)t.PaymentStatus,
                    IsUrgent = (bool)t.IsUrgent,
                    ProposePaymentDate = t.ProposePaymentDate.HasValue ? t.ProposePaymentDate : null,
                    PaymentDate = t.PaymentDate.HasValue ? t.PaymentDate : null,
                    PaymentAmount = t.PaymentAmount,
                    Note = t.Note,
                    HrNote = t.HrNote,
                    IsRefund = t.IsRefund ?? false
                }).ToList() : new List<PaymentRequestPlanDetailResponse>(),
                IsDirectorApprove = paymentRequest.ReviewStatus == (int)EPaymentRequestStatus.DirectorApproved ? true : false
            };
            return SuccessResult(response);
        }
        [HttpGet]
        [Route("view/paging")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> GetPagingAsync([FromQuery] PaymentRequestPlanPagingModel model)
        {
            var user = GetCurrentUser();
            var names = new List<string>()
            {
                Constant.PaymentRequestAccountEmails,
                Constant.PaymentRequestDirectorEmails
            };
            var emails = await _unitOfWork.GlobalConfigurationRepository.GetByMultiNameAsync(names);
            if (emails.Count == 0)
            {
                return ErrorResult("Người dùng chưa được config");
            }
            var isHasAllowEmail = string.Join(",", emails.Select(t => t.Value)).Split(",").Any(y => y.Equals(user.Email, StringComparison.OrdinalIgnoreCase));
            if (!isHasAllowEmail)
            {
                return ErrorResult("Bạn không có quyền xem thông tin này");
            }
            var res = await _paymentRequestPlanService.GetAllWithPagingAsync(model);
            return SuccessResult(res);
        }
        #endregion
        #region Accountant
        [HttpPut]
        [Route("changestatus/{id}")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> ChangeStatusAsync([FromRoute] int id, [FromBody] PaymentRequestPlanChangeStatusModel model)
        {
            var user = GetCurrentUser();
            var res = await _paymentRequestPlanService.ChangeStatusAsync(id, model, user.Email);
            if(!res.Status || !string.IsNullOrEmpty(res.ErrorMessage) || res.Data == null)
            {
                return ErrorResult(res.ErrorMessage ?? "Lỗi khi cập nhật trạng thái thanh toán");
            }
            await _unitOfWork.PaymentRequestPlanRepository.UpdateAsync(res.Data);
            await _unitOfWork.SaveChangesAsync();
            return SuccessResult("Cập nhật trạng thái thanh toán thành công");
        }
        [HttpPut]
        [Route("accountant/{id}")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> AccountantUpdateAsync([FromRoute] int id, [FromBody] PaymentRequestPlanRequest request)
        {
            var user = GetCurrentUser();
            var res = await _paymentRequestPlanService.AccountantUpdateAsync(user.Email, id, request);
            if (!res.Status || !string.IsNullOrEmpty(res.ErrorMessage) || res.Data == null)
            {
                return ErrorResult(res.ErrorMessage ?? "Lỗi khi cập nhật đợt thanh toán");
            }
            await _unitOfWork.PaymentRequestPlanRepository.UpdateAsync(res.Data);
            await _unitOfWork.SaveChangesAsync();
            return SuccessResult("Cập nhật đợt thanh toán thành công");
        }
        [HttpPost]
        [Route("accountant/paymentrequest/{paymentRequestId}/add")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> AccountantCreateAsync([FromRoute] int paymentRequestId, [FromBody] PaymentRequestPlanRequest request)
        {
            var user = GetCurrentUser();
            var res = await _paymentRequestPlanService.AccountantCreateAsync(user.Email, paymentRequestId, request);
            if (!res.Status || !string.IsNullOrEmpty(res.ErrorMessage) || res.Data == null)
            {
                return ErrorResult(res.ErrorMessage ?? "Lỗi khi tạo đợt thanh toán");
            }
            await _unitOfWork.PaymentRequestPlanRepository.CreateAsync(res.Data);
            await _unitOfWork.SaveChangesAsync();
            return SuccessResult("Tạo đợt thanh toán thành công");
        }
        [HttpDelete]
        [Route("accountant/{id}")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> AccountantDeleteAsync([FromRoute] int id)
        {
            var user = GetCurrentUser();
            var res = await _paymentRequestPlanService.AccountantDeleteAsync(user.Email, id);
            if (!res.Status || !string.IsNullOrEmpty(res.ErrorMessage) || res.Data == null)
            {
                return ErrorResult(res.ErrorMessage ?? "Lỗi khi xóa đợt thanh toán");
            }
            await _unitOfWork.PaymentRequestPlanRepository.DeleteAsync(res.Data);
            await _unitOfWork.SaveChangesAsync();
            return SuccessResult("Xóa đợt thanh toán thành công");
        }
        [HttpPut]
        [Route("refund/{id}")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> RefundAsync([FromRoute] int id)
        {
            var user = GetCurrentUser();
            var res = await _paymentRequestPlanService.RefundAsync(user.Email, id);
            if (!res.Status || !string.IsNullOrEmpty(res.ErrorMessage) || res.Data == null)
            {
                return ErrorResult(res.ErrorMessage ?? "Lỗi khi hoàn tiền");
            }
            await _unitOfWork.PaymentRequestPlanRepository.UpdateAsync(res.Data);
            await _unitOfWork.SaveChangesAsync();
            return SuccessResult("Hoàn tiền thành công");
        }
        #endregion
    }
}
