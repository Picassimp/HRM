using InternalPortal.ApplicationCore.Interfaces;
using InternalPortal.ApplicationCore.Interfaces.Business;
using InternalPortal.ApplicationCore.ValueObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InternalPortal.API.Controllers
{
    public class ExportBillLineItemController : BaseApiController
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IExportBillLineItemService _exportBillLineItemService;
        public ExportBillLineItemController(IUnitOfWork unitOfWork,
            IExportBillLineItemService exportBillLineItemService)
        {
            _unitOfWork = unitOfWork;
            _exportBillLineItemService = exportBillLineItemService;
        }
        [HttpDelete]
        [Route("{id}")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> DeleteAsync([FromRoute] int id)
        {
            var res = await _exportBillLineItemService.DeleteAsync(id);
            if(!res.Status || !string.IsNullOrEmpty(res.ErrorMessage) || res.Data == null)
            {
                return ErrorResult(res.ErrorMessage ?? "Lỗi khi xóa chi tiết phiếu xuất");
            }
            return SuccessResult("Cập nhật chi tiết phiếu xuất thành công");
        }

        [HttpPut]
        [Route("{id}")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> UpdateAsync([FromRoute] int id, [FromBody] int quantity)
        {
            var res = await _exportBillLineItemService.UpdateAsync(id,quantity);
            if (!res.Status || !string.IsNullOrEmpty(res.ErrorMessage) || res.Data == null)
            {
                return ErrorResult(res.ErrorMessage ?? "Lỗi khi cập nhật chi tiết phiếu xuất");
            }
            return SuccessResult("Cập nhật chi tiết phiếu xuất thành công");
        }
    }
}
