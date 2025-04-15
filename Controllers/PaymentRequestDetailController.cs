using InternalPortal.ApplicationCore.Interfaces;
using InternalPortal.ApplicationCore.Interfaces.Business;
using InternalPortal.ApplicationCore.Models.PaymentRequest;
using InternalPortal.ApplicationCore.ValueObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InternalPortal.API.Controllers
{
    public class PaymentRequestDetailController : BaseApiController
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IPaymentRequestDetailService _paymentRequestDetailService;
        public PaymentRequestDetailController(IUnitOfWork unitOfWork,
            IPaymentRequestDetailService paymentRequestDetailService)
        {
            _unitOfWork = unitOfWork;
            _paymentRequestDetailService = paymentRequestDetailService; 
        }
        [HttpPost]
        [Route("paymentrequest/{id}/add")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> CreateAsync([FromBody] PaymentRequestDetailModel model, [FromRoute] int id)
        {
            var user = GetCurrentUser();
            var res = await _paymentRequestDetailService.CreateAsync(model,id, user.Id);
            if (!res.Status || !string.IsNullOrEmpty(res.ErrorMessage) || res.Data == null)
            {
                return ErrorResult(res.ErrorMessage ?? "Lỗi khi thêm chi tiết yêu cầu");
            }
            await _unitOfWork.PaymentRequestDetailRepository.CreateAsync(res.Data);
            await _unitOfWork.SaveChangesAsync();
            return SuccessResult("Thêm chi tiết yêu cầu thành công");
        }
        [HttpPut]
        [Route("{id}")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> UpdateAsync([FromRoute] int id, [FromBody] PaymentRequestDetailModel model)
        {
            var user = GetCurrentUser();
            var res = await _paymentRequestDetailService.UpdateAsync(model, user.Id, id);
            if (!res.Status || !string.IsNullOrEmpty(res.ErrorMessage) || res.Data == null)
            {
                return ErrorResult(res.ErrorMessage ?? "Lỗi khi cập nhật chi tiết yêu cầu");
            }
            await _unitOfWork.PaymentRequestDetailRepository.UpdateAsync(res.Data);
            await _unitOfWork.SaveChangesAsync();
            return SuccessResult("Cập nhật chi tiết yêu cầu thành công");
        }
        [HttpDelete]
        [Route("{id}")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> DeleteAsync([FromRoute] int id)
        {
            var user = GetCurrentUser();
            var res = await _paymentRequestDetailService.DeleteAsync(id, user.Id);
            if (!res.Status || !string.IsNullOrEmpty(res.ErrorMessage) || res.Data == null)
            {
                return ErrorResult(res.ErrorMessage ?? "Lỗi khi xóa chi tiết yêu cầu");
            }
            await _unitOfWork.PaymentRequestDetailRepository.DeleteAsync(res.Data);
            await _unitOfWork.SaveChangesAsync();
            return SuccessResult("Xóa chi tiết yêu cầu thành công");
        }
        #region Accountant
        [HttpPut]
        [Route("accountant/{id}")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> AccountantUpdateAsync([FromRoute] int id, [FromBody] PaymentRequestDetailModel model)
        {
            var user = GetCurrentUser();
            var res = await _paymentRequestDetailService.AccountantUpdateAsync(model, user.Email, id);
            if (!res.Status || !string.IsNullOrEmpty(res.ErrorMessage) || res.Data == null)
            {
                return ErrorResult(res.ErrorMessage ?? "Lỗi khi cập nhật chi tiết yêu cầu");
            }
            await _unitOfWork.PaymentRequestDetailRepository.UpdateAsync(res.Data);
            await _unitOfWork.SaveChangesAsync();
            return SuccessResult("Cập nhật chi tiết yêu cầu thành công");
        }
        #endregion
    }
}
