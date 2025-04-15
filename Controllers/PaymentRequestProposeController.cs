using InternalPortal.ApplicationCore.Interfaces;
using InternalPortal.ApplicationCore.Models.PaymentRequestPropose;
using InternalPortal.ApplicationCore.ValueObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InternalPortal.API.Controllers
{
    public class PaymentRequestProposeController : BaseApiController
    {
        private readonly IUnitOfWork _unitOfWork;
        public PaymentRequestProposeController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }
        [HttpGet]
        [Route("drop-down")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> GetVendorDropdown()
        {
            var res = await _unitOfWork.PaymentRequestProposeRepository.GetAllAsync();
            var dropdown = res.Count > 0 ? res.Select(t => new PaymentRequestProposeDropdownResponse()
            {
                Id = t.Id,
                Name = t.Name,
            }).ToList() : new List<PaymentRequestProposeDropdownResponse>();
            var otherPropose = new PaymentRequestProposeDropdownResponse()
            {
                Id = 0,
                Name = "Khác"
            };
            dropdown.Add(otherPropose);
            return SuccessResult(dropdown);
        }
    }
}
