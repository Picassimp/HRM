using InternalPortal.ApplicationCore.Interfaces;
using InternalPortal.ApplicationCore.Interfaces.Business;
using InternalPortal.ApplicationCore.ValueObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InternalPortal.API.Controllers
{
    public class POPRLineItemController : BaseApiController
    {
        private readonly IPOPRLineItemService _poprlineItemService;
        private readonly IUnitOfWork _unitOfWork;
        public POPRLineItemController(IPOPRLineItemService pOPRLineItemService,
            IUnitOfWork unitOfWork)
        {
            _poprlineItemService = pOPRLineItemService; 
            _unitOfWork = unitOfWork;
        }
        [HttpDelete]
        [Route("{id}")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> DeleleAsync([FromRoute] int id)
        {
            var res = await _poprlineItemService.DeleteAsync(id);
            if (!res.Status || res.Data == null || !string.IsNullOrEmpty(res.ErrorMessage))
            {
                return ErrorResult(res.ErrorMessage ?? "Lỗi khi xóa sản phẩm");
            }
            return SuccessResult("Xóa sản phẩm thành công");
        }
        [HttpPut]
        [Route("{id}")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> UpdateAsync([FromRoute] int id, [FromBody]int quantity)
        {
            var res = await _poprlineItemService.UpdateAsync(id,quantity);
            if (!res.Status || res.Data == null || !string.IsNullOrEmpty(res.ErrorMessage))
            {
                return ErrorResult(res.ErrorMessage ?? "Lỗi khi cập nhật sản phẩm");
            }
            return SuccessResult("Cập nhật sản phẩm thành công");
        }
        [HttpPut]
        [Route("receive/{id}")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> ReceiveAsync([FromRoute] int id, [FromBody] int quantity)
        {
            var res = await _poprlineItemService.ReceiveAsync(id,quantity);
            if (!res.Status || res.Data == null || !string.IsNullOrEmpty(res.ErrorMessage))
            {
                return ErrorResult(res.ErrorMessage ?? "Lỗi khi nhận sản phẩm");
            }
            await _unitOfWork.POPRLineItemRepository.UpdateAsync(res.Data);
            await _unitOfWork.SaveChangesAsync();
            return SuccessResult("Nhận hàng thành công");
        }
        [HttpPut]
        [Route("lackreceive/{id}")]
        public async Task<IActionResult> LackReceivedAsync([FromRoute] int id)
        {
            var res = await _poprlineItemService.LackReceiveAsync(id);
            if (!res.Status || res.Data == null || !string.IsNullOrEmpty(res.ErrorMessage))
            {
                return ErrorResult(res.ErrorMessage ?? "Lỗi khi nhận sản phẩm");
            }
            await _unitOfWork.POPRLineItemRepository.UpdateAsync(res.Data);
            await _unitOfWork.SaveChangesAsync();
            return SuccessResult("Nhận hàng thiếu thành công");
        }
    }
}
