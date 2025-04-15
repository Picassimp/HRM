using InternalPortal.ApplicationCore.Interfaces;
using InternalPortal.ApplicationCore.Interfaces.Business;
using InternalPortal.ApplicationCore.Models.AccessControl;
using InternalPortal.ApplicationCore.ValueObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InternalPortal.API.Controllers
{
    public class AccessControlController : BaseApiController
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAccessControlLogService _accessControlLogService;
        public AccessControlController(IUnitOfWork unitOfWork,
            IAccessControlLogService accessControlLogService)
        {
            _unitOfWork = unitOfWork;
            _accessControlLogService = accessControlLogService;
        }

        [HttpGet]
        [Route("{location}")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> SendAPIAccessControl([FromRoute] string location)
        {
            var user = GetCurrentUser();
            //validate IP
            string? remoteIpAddress;
            //get remote IP through the Azure Front Door header
            bool isClientIp = HttpContext.Request.Headers.TryGetValue("x-envoy-external-address", out var headerValues1);
            if (isClientIp)
            {
                remoteIpAddress = headerValues1.FirstOrDefault();
            }
            else
            {
                remoteIpAddress = HttpContext.Connection.RemoteIpAddress?.MapToIPv4().ToString();
            }
            var whiteListIps = await _unitOfWork.GlobalConfigurationRepository.GetByNameAsync("AllowUnlockTheDoorIP");
            //if we have setup white list IPs
            if (whiteListIps != null && !string.IsNullOrEmpty(whiteListIps.Value))
            {
                if (!string.IsNullOrEmpty(remoteIpAddress) && !whiteListIps.Value.Contains(remoteIpAddress))
                {
                    return ErrorResult("Vui lòng kết nối mạng internet của công ty để sử dụng tính năng này");
                }
            }

            //disable open the door
            //return ErrorResult("Không thể sự dụng chức năng này trong những ngày Tết");
            var requestModel = new AccessControlApiRequest
            {
                Email = user.Email ?? "",
                Location = location
            };

            var res = await _accessControlLogService.CallApiToAccessControl(requestModel);

            if (res.ErrorMessage != null && !string.IsNullOrEmpty(res.ErrorMessage))
            {
                return ErrorResult(res.ErrorMessage ?? "Lỗi không thể gửi API qua hệ thống");
            }

            return SuccessResult("Mở khóa thành công");
        }
    }
}
