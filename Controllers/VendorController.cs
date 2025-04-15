using InternalPortal.ApplicationCore.Interfaces;
using InternalPortal.ApplicationCore.Models.Vendor;
using InternalPortal.ApplicationCore.ValueObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InternalPortal.API.Controllers
{
    public class VendorController : BaseApiController
    {
        private readonly IUnitOfWork _unitOfWork;
        public VendorController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }
        [HttpGet]
        [Route("drop-down")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> GetVendorDropdown()
        {
            var res = await _unitOfWork.VendorRepository.GetAllAsync();
            var dropdown = res.Select(t => new VendorDropdownResponse()
            {
                Id = t.Id,
                Name = t.VendorName,
                BankName = t.BankName ?? string.Empty,
                BankNumber = t.BankNumber ?? string.Empty
            }).ToList();
            return SuccessResult(dropdown);
        }
        [HttpGet]
        [Route("user/drop-down")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> GetUserVendorDropdown()
        {
            var user = GetCurrentUser();
            var userVendors = await _unitOfWork.UserVendorRepository.GetByUserIdAsync(user.Id);
            var res = userVendors.Count > 0 ? userVendors.Select(t => new VendorDropdownResponse()
            {
                Id = t.VendorId,
                Name = t.Vendor.VendorName,
                BankName = t.Vendor.BankName ?? string.Empty,   
                BankNumber = t.Vendor.BankNumber ?? string.Empty,   
            }).ToList() : new List<VendorDropdownResponse>();
            return SuccessResult(res);
        }
    }
}
