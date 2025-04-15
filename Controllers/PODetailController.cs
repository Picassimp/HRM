using InternalPortal.ApplicationCore.Interfaces;
using InternalPortal.ApplicationCore.Interfaces.Business;
using InternalPortal.ApplicationCore.Models.PurchaseOrder.PODetail;
using InternalPortal.ApplicationCore.ValueObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InternalPortal.API.Controllers
{
    public class PODetailController : BaseApiController
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IPODetailService _poDetailService;
        public PODetailController(IUnitOfWork unitOfWork,
            IPODetailService poDetailService)
        {
            _unitOfWork = unitOfWork;
            _poDetailService = poDetailService;
        }
        [HttpPut]
        [Route("{id}")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> UpdateAsync([FromRoute] int id, [FromBody] PODetailRequest request)
        {
            var res = await _poDetailService.UpdateAsync(id, request);
            if(!res.Status || !string.IsNullOrEmpty(res.ErrorMessage) || res.Data == null)
            {
                return ErrorResult(res.ErrorMessage ?? "Lỗi khi cập nhật chi tiết đơn hàng");
            }
            await _unitOfWork.PODetailRepository.UpdateAsync(res.Data);
            await _unitOfWork.SaveChangesAsync();
            return SuccessResult("Cập nhật chi tiết đơn hàng thành công");
        }
        [HttpDelete]
        [Route("{id}")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> DeleteAsync([FromRoute] int id)
        {
            var res = await _poDetailService.DeleteAsync(id);
            if (!res.Status || !string.IsNullOrEmpty(res.ErrorMessage) || res.Data == null)
            {
                return ErrorResult(res.ErrorMessage ?? "Lỗi khi xóa chi tiết đơn hàng");
            }
            await _unitOfWork.PODetailRepository.DeleteAsync(res.Data);
            await _unitOfWork.SaveChangesAsync();
            return SuccessResult("Xóa chi tiết đơn hàng thành công");
        }
        [HttpPut]
        [Route("receive/{id}")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> ReceiveAsync([FromRoute] int id, [FromBody] int quantity)
        {
            var res = await _poDetailService.ReceiveAsync(id, quantity);
            if (!res.Status || !string.IsNullOrEmpty(res.ErrorMessage) || res.Data == null)
            {
                return ErrorResult(res.ErrorMessage ?? "Lỗi khi nhận hàng");
            }
            await _unitOfWork.PODetailRepository.UpdateAsync(res.Data);
            await _unitOfWork.SaveChangesAsync();
            return SuccessResult("Nhận hàng thành công");
        }
    }
}
