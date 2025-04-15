using InternalPortal.ApplicationCore.Interfaces;
using InternalPortal.ApplicationCore.Models.Location;
using InternalPortal.ApplicationCore.ValueObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace InternalPortal.API.Controllers
{
    public class LocationController : BaseApiController
    {
        private readonly IUnitOfWork _unitOfWork;
        public LocationController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }
        [HttpGet]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> SendLocation()
        {
            var globals = await _unitOfWork.GlobalConfigurationRepository.GetByNameAsync("location");
            var model = JsonConvert.DeserializeObject<List<LocationMobileResponse>>(globals.Value);
            if (model == null || model.Count == 0)
            {
                return ErrorResult("Không có toạ độ");
            }
            if (!model.Any())
            {
                return ErrorResult("Toạ độ không hợp lệ");
            }
            return SuccessResult(model);
        }
    }
}
