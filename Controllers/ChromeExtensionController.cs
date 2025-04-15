using InternalPortal.ApplicationCore.Enums;
using InternalPortal.ApplicationCore.Interfaces;
using InternalPortal.ApplicationCore.Interfaces.Business;
using InternalPortal.ApplicationCore.Interfaces.Utilities.AzureDevOps;
using InternalPortal.ApplicationCore.Interfaces.Utilities.Jira;
using InternalPortal.ApplicationCore.Models.AzureDevOps;
using InternalPortal.ApplicationCore.Models.ChromeExtension;
using InternalPortal.ApplicationCore.Models.Jira;
using InternalPortal.ApplicationCore.Models.ProjectManagement;
using InternalPortal.ApplicationCore.ValueObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InternalPortal.API.Controllers
{
    public class ChromeExtensionController : BaseApiController
    {
        private readonly IChromeExtensionService _chromeExtensionService;
        public ChromeExtensionController(
            IChromeExtensionService chromeExtensionService
            )
        {
            _chromeExtensionService = chromeExtensionService;
        }

        [HttpGet]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> GetTimesheetAsync()
        {
            var user = GetCurrentUser();
            var response = await _chromeExtensionService.GetTimesheetByUserIdAndDateAsync(user);
            return SuccessResult(response);
        }

        [HttpPut("task/start")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> StartLogTimeAsync([FromBody] ChromeExtensionStartTaskRequest request)
        {
            var user = GetCurrentUser();
            var res = await _chromeExtensionService.PrepareStartTaskAsync(user.Id, request);
            if (!res.Status || !string.IsNullOrEmpty(res.ErrorMessage) || res.Data == null)
            {
                return ErrorResult(res.ErrorMessage ?? "Lỗi khi bắt đầu task");
            }
            return SuccessResult("Bắt đầu task thành công");
        }

        [HttpPut("task/stop")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> StopLogTimeAsync([FromBody] ChromeExtensionStopTaskRequest request)
        {
            var user = GetCurrentUser();
            var res = await _chromeExtensionService.PrepareStopTaskAsync(user.Id, request);
            if (!res.Status || !string.IsNullOrEmpty(res.ErrorMessage) || res.Data == null)
            {
                return ErrorResult(res.ErrorMessage ?? "Lỗi khi dừng task");
            }
            return SuccessResult("Dừng task thành công");
        }
    }
}
