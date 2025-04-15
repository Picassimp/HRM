using InternalPortal.ApplicationCore.Interfaces;
using InternalPortal.ApplicationCore.Interfaces.Business;
using InternalPortal.ApplicationCore.Models.ExportBill;
using InternalPortal.ApplicationCore.Models.PurchaseOrder.PurchaseRequest;
using InternalPortal.ApplicationCore.ValueObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using static InternalPortal.ApplicationCore.ValueObjects.Global;

namespace InternalPortal.API.Controllers
{
    public class ExportBillController : BaseApiController
    {
        private readonly IExportBillService _exportBillService;
        private readonly IUnitOfWork _unitOfWork;
        public ExportBillController(IExportBillService exportBillService,
            IUnitOfWork unitOfWork)
        {
            _exportBillService = exportBillService;
            _unitOfWork = unitOfWork;
        }
        [HttpGet]
        [Route("paging")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> GetAllWithPagingAsync([FromQuery] ExportBillPagingModel model)
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
                    return ErrorResult("Bạn không có quyền xem thông tin này");
                }
            }
            var res = await _exportBillService.GetAllWithPagingAsync(model);
            return SuccessResult(res);
        }
        [HttpGet]
        [Route("{id}")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> GetExportBillDetailAsync([FromRoute] int id)
        {
            var user = GetCurrentUser();
            var res = await _exportBillService.GetExportBillDetailAsync(user.Email,id);
            if(!res.Status || !string.IsNullOrEmpty(res.ErrorMessage) || res.Data == null)
            {
                return ErrorResult(res.ErrorMessage ?? "Lỗi khi lấy thông tin của phiếu xuất");
            }
            return SuccessResult(res.Data);
        }
        [HttpGet]
        [Route("detail/{id}")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> GetExportBillLineItemAsync([FromRoute] int id)
        {
            var user = GetCurrentUser();
            var res = await _exportBillService.GetExportBillLineItemAsync(user.Email,id);
            if (!res.Status || !string.IsNullOrEmpty(res.ErrorMessage) || res.Data == null)
            {
                return ErrorResult(res.ErrorMessage ?? "Lỗi khi lấy thông tin chi tiết của phiếu xuất");
            }
            return SuccessResult(res.Data);
        }
        [HttpPost]
        [Route("createfrompr/{id}")]
        public async Task<IActionResult> CreateExportBillFromRequestAsync([FromRoute] int id)
        {
            var user = GetCurrentUser();
            var res = await _exportBillService.CreateExportBillFromRequestAsync(id, user.Id, user.Email);
            if (!res.Status || !string.IsNullOrEmpty(res.ErrorMessage) || res.Data == null)
            {
                return ErrorResult(res.ErrorMessage ?? "Lỗi khi tạo phiếu xuất");
            }
            await _unitOfWork.ExportBillRepository.CreateAsync(res.Data);
            await _unitOfWork.SaveChangesAsync();
            return SuccessResult(res.Data.Id,"Tạo phiếu xuất thành công");
        }
        [HttpPost]
        [Route("additem/{id}")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> AddItemFromRequest([FromRoute] int id, [FromBody] ItemCreateRequest request)
        {
            var res = await _exportBillService.AddItemFromRequestAsync(id, request);
            if (!res.Status || !string.IsNullOrEmpty(res.ErrorMessage))
            {
                return ErrorResult("Lỗi khi thêm sản phẩm");
            }
            return SuccessResult("Thêm sản phẩm thành công");
        }
        [HttpPost]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> CreateAsync([FromBody] ExportBillRequest request)
        {
            var user = GetCurrentUser();
            var res = await _exportBillService.CreateAsync(user.Id,user.Email,request);
            if(!res.Status || !string.IsNullOrEmpty(res.ErrorMessage) || res.Data == null)
            {
                return ErrorResult(res.ErrorMessage ?? "Lỗi khi tạo phiếu xuất");
            }
            await _unitOfWork.ExportBillRepository.CreateAsync(res.Data);
            await _unitOfWork.SaveChangesAsync();
            return SuccessResult(res.Data.Id);
        }
        [HttpDelete]
        [Route("{id}")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> DeleteAsync([FromRoute] int id)
        {
            var user = GetCurrentUser();
            var res = await _exportBillService.DeleteAsync(id,user.Email);
            if (!res.Status || !string.IsNullOrEmpty(res.ErrorMessage) || res.Data == null)
            {
                return ErrorResult(res.ErrorMessage ?? "Lỗi khi xóa phiếu xuất");
            }
            await _unitOfWork.ExportBillRepository.DeleteAsync(res.Data);
            await _unitOfWork.SaveChangesAsync();
            return SuccessResult("Xóa phiếu xuất thành công");
        }
        [HttpPut]
        [Route("{id}")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> UpdateAsync([FromRoute] int id, [FromBody] ExportBillRequest request)
        {
            var user = GetCurrentUser();
            var res = await _exportBillService.UpdateAsync(id,user.Email, request);
            if (!res.Status || !string.IsNullOrEmpty(res.ErrorMessage) || res.Data == null)
            {
                return ErrorResult(res.ErrorMessage ?? "Lỗi khi cập nhật phiếu xuất");
            }
            await _unitOfWork.ExportBillRepository.UpdateAsync(res.Data);
            await _unitOfWork.SaveChangesAsync();
            return SuccessResult("Cập nhật phiếu xuất thành công");
        }
        [HttpGet]
        [Route("accountant/paging")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> AccountantGetAllWithPagingAsync([FromQuery] ExportBillPagingModel model)
        {
            var user = GetCurrentUser();
            var accountantEmails = await _unitOfWork.GlobalConfigurationRepository.GetByNameAsync(Constant.SecondHrEmail);
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
                    return ErrorResult("Bạn không có quyền xem thông tin này");
                }
            }
            var res = await _exportBillService.AccountantGetAllWithPagingAsync(model);
            return SuccessResult(res);
        }
        [HttpGet]
        [Route("director/paging")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> DirectorGetAllWithPagingAsync([FromQuery] ExportBillPagingModel model)
        {
            var user = GetCurrentUser();
            var directorEmail = await _unitOfWork.GlobalConfigurationRepository.GetByNameAsync(Constant.DirectorEmail);
            if (directorEmail == null || string.IsNullOrEmpty(directorEmail.Value))
            {
                return ErrorResult("Người dùng chưa được config");
            }
            var accountantEmail = directorEmail.Value.Split(",").Select(t => t.Trim()).FirstOrDefault(y => y.Equals(user.Email, StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrEmpty(accountantEmail))
            {
                return ErrorResult("Người dùng chưa được config");
            }
            else
            {
                if (!accountantEmail.Equals(user.Email, StringComparison.OrdinalIgnoreCase))
                {
                    return ErrorResult("Bạn không có quyền xem thông tin này");
                }
            }
            var res = await _exportBillService.DirectorGetAllWithPagingAsync(model);
            return SuccessResult(res);
        }
        [HttpPut]
        [Route("export/{id}")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> ExportAsync([FromRoute] int id)
        {
            var user = GetCurrentUser();
            var res = await _exportBillService.ExportAsync(id, user.Email);
            if (!res.Status || !string.IsNullOrEmpty(res.ErrorMessage) || res.Data == null)
            {
                return ErrorResult(res.ErrorMessage ?? "Lỗi khi cập nhật phiếu xuất");
            }
            await _unitOfWork.ExportBillRepository.UpdateAsync(res.Data);
            await _unitOfWork.SaveChangesAsync();
            return SuccessResult("Cập nhật phiếu xuất thành công");
        }
    }
}
