using InternalPortal.ApplicationCore.Enums;
using InternalPortal.ApplicationCore.Helpers;
using InternalPortal.ApplicationCore.Interfaces;
using InternalPortal.ApplicationCore.Interfaces.Business;
using InternalPortal.ApplicationCore.Models.PaymentPlan;
using InternalPortal.ApplicationCore.ValueObjects;
using InternalPortal.Infrastructure.Services.Business;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security;
using static InternalPortal.ApplicationCore.ValueObjects.Global;

namespace InternalPortal.API.Controllers
{
    public class PaymentPlanController : BaseApiController
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IPaymentPlanService _paymentPlanService;
        public PaymentPlanController(IUnitOfWork unitOfWork,
           IPaymentPlanService paymentPlanService)
        {
            _unitOfWork = unitOfWork;
            _paymentPlanService = paymentPlanService;
        }
        [HttpGet]
        [Route("{id}")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> GetAsync([FromRoute] int id)
        {
            var paymentPlan = await _unitOfWork.PaymentPlanRepository.GetByIdAsync(id);
            if (paymentPlan == null)
            {
                return ErrorResult("Không tồn tại kế hoạch thanh toán");
            }
            var res = new PaymentPlanResponse()
            {
                Id = paymentPlan.Id,    
                Note = paymentPlan.Note ?? "",
                PayDate = paymentPlan.PayDate,
                PaymentType = paymentPlan.PayType,
                PaymentTypeName = CommonHelper.GetDescription((EPaymentType)paymentPlan.PayType),
                Price = paymentPlan.PaymentAmount,
                Status = paymentPlan.PaymentStatus
            };
            return SuccessResult(res);  
        }
        [HttpPost]
        [Route("purchaseorder/{id}/add")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> CreateAsync([FromRoute] int id, [FromBody] PaymentPlanRequest request)
        {
            var res = await _paymentPlanService.CreateAsync(id, request);
            if(!res.Status || !string.IsNullOrEmpty(res.ErrorMessage) || res.Data == null)
            {
                return ErrorResult(res.ErrorMessage ?? "Lỗi khi tạo kế hoạch thanh toán");
            }
            await _unitOfWork.PaymentPlanRepository.CreateAsync(res.Data);
            await _unitOfWork.SaveChangesAsync();
            return SuccessResult("Tạo kế hoạch thanh toán thành công");
        }
        [HttpDelete]
        [Route("{id}")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> DeleteAsync([FromRoute] int id)
        {
            var paymentPlan = await _unitOfWork.PaymentPlanRepository.GetByIdAsync(id);
            if (paymentPlan == null)
            {
                return ErrorResult("Không tồn tại kế hoạch thanh toán");
            }
            var res = await _paymentPlanService.DeleteAsync(id);
            if(!res.Status || !string.IsNullOrEmpty(res.ErrorMessage) || res.Data == null)
            {
                return ErrorResult(res.ErrorMessage ?? "Lỗi khi xóa kế hoạch thanh toán");
            }
            await _unitOfWork.PaymentPlanRepository.DeleteAsync(res.Data);
            await _unitOfWork.SaveChangesAsync();
            return SuccessResult("Xóa kế hoạch thanh toán thành công");
        }
        [HttpPut]
        [Route("{id}")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> UpdateAsync([FromRoute] int id, [FromBody] PaymentPlanRequest request)
        {
            var paymentPlan = await _unitOfWork.PaymentPlanRepository.GetByIdAsync(id);
            if (paymentPlan == null)
            {
                return ErrorResult("Không tồn tại kế hoạch thanh toán");
            }
            var res = await _paymentPlanService.UpdateAsync(id,request);
            if (!res.Status || !string.IsNullOrEmpty(res.ErrorMessage) || res.Data == null)
            {
                return ErrorResult(res.ErrorMessage ?? "Lỗi khi cập nhật kế hoạch thanh toán");
            }
            await _unitOfWork.PaymentPlanRepository.UpdateAsync(res.Data);
            await _unitOfWork.SaveChangesAsync();
            return SuccessResult("Cập nhật kế hoạch thanh toán thành công");
        }
        [HttpGet]
        [Route("paging")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> GetAllWithPagingAsync([FromQuery] PaymentPlanPagingModel model)
        {
            var user = GetCurrentUser();
            var names = new List<string>()
            {
                Constant.FirstHrEmail,
                Constant.SecondHrEmail,
                Constant.DirectorEmail
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
            var res = await _paymentPlanService.GetAllWithPagingAsync(model);
            return SuccessResult(res);
        }
        [HttpPut]
        [Route("changestatus/{id}")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> ChangeStatusAsync([FromRoute] int id)
        {
            var user = GetCurrentUser();
            var accountantEmails = await _unitOfWork.GlobalConfigurationRepository.GetByNameAsync(Constant.FirstHrEmail);
            if (accountantEmails == null || string.IsNullOrEmpty(accountantEmails.Value))
            {
                return ErrorResult("Người dùng chưa được config");
            }
            var accountantEmail = accountantEmails.Value.Split(",").Select(t => t.Trim()).FirstOrDefault(y => y.Equals(user.Email, StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrEmpty(accountantEmail))
            {
                return ErrorResult("Người dùng chưa được config");
            }
            else
            {
                if (!accountantEmail.Equals(user.Email, StringComparison.OrdinalIgnoreCase))
                {
                    return ErrorResult("Bạn không có quyền cập nhật");
                }
            }
            var res = await _paymentPlanService.ChangeStatusAsync(id);
            if(!res.Status || !string.IsNullOrEmpty(res.ErrorMessage) || res.Data == null)
            {
                return ErrorResult(res.ErrorMessage ?? "Lỗi khi cập nhật thông tin thanh toán");
            }
            await _unitOfWork.PaymentPlanRepository.UpdateAsync(res.Data);
            await _unitOfWork.SaveChangesAsync();
            return SuccessResult("Cập nhật đợt thanh toán thành công");
        }
        [HttpPut]
        [Route("refund/{id}")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> RefundAsync([FromRoute] int id, [FromBody] PaymentRefundRequest request)
        {
            var user = GetCurrentUser();
            var accountantEmails = await _unitOfWork.GlobalConfigurationRepository.GetByNameAsync(Constant.FirstHrEmail);
            if (accountantEmails == null || string.IsNullOrEmpty(accountantEmails.Value))
            {
                return ErrorResult("Người dùng chưa được config");
            }
            var accountantEmail = accountantEmails.Value.Split(",").Select(t => t.Trim()).FirstOrDefault(y => y.Equals(user.Email, StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrEmpty(accountantEmail))
            {
                return ErrorResult("Người dùng chưa được config");
            }
            else
            {
                if (!accountantEmail.Equals(user.Email, StringComparison.OrdinalIgnoreCase))
                {
                    return ErrorResult("Bạn không có quyền cập nhật");
                }
            }
            var res = await _paymentPlanService.RefundAsync(id,request);
            if (!res.Status || !string.IsNullOrEmpty(res.ErrorMessage) || res.Data == null)
            {
                return ErrorResult(res.ErrorMessage ?? "Lỗi khi cập nhật thông tin thanh toán");
            }
            await _unitOfWork.PaymentPlanRepository.UpdateAsync(res.Data);
            await _unitOfWork.SaveChangesAsync();
            return SuccessResult("Cập nhật đợt thanh toán thành công");
        }
    }
}
