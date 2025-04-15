using InternalPortal.ApplicationCore.Interfaces;
using InternalPortal.ApplicationCore.Interfaces.Business;
using InternalPortal.ApplicationCore.ValueObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InternalPortal.API.Controllers
{
    public class ExportBillDetailController : BaseApiController
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IExportBillDetailService _exportBillDetailService;
        public ExportBillDetailController(IUnitOfWork unitOfWork,
            IExportBillDetailService exportBillDetailService)
        {
            _unitOfWork = unitOfWork;
            _exportBillDetailService = exportBillDetailService;
        }
        [HttpDelete]
        [Route("{id}")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> DeleteAsync([FromRoute] int id)
        {
            var res = await _exportBillDetailService.DeleteAsync(id);
            if (!res.Status || !string.IsNullOrEmpty(res.ErrorMessage) || res.Data == null)
            {
                return ErrorResult(res.ErrorMessage ?? "Lỗi khi xóa chi tiết phiếu xuất");
            }
            await _unitOfWork.ExportBillDetailRepository.DeleteAsync(res.Data);
            await _unitOfWork.SaveChangesAsync();
            return SuccessResult("Xóa chi tiết phiếu xuất thành công");
        }
    }
}
