using Hangfire;
using InternalPortal.ApplicationCore.Interfaces;
using InternalPortal.ApplicationCore.Interfaces.Business;
using InternalPortal.ApplicationCore.Models;
using InternalPortal.ApplicationCore.Models.Log;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InternalPortal.API.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class AccessControlLogController : BaseApiController
    {
        private readonly IConfiguration _configuration;
        private readonly IAccessControlLogService _accessControlLogService;

        public AccessControlLogController(
            IConfiguration configuration,
            IAccessControlLogService accessControlLogService
        )
        {
            _configuration = configuration;
            _accessControlLogService = accessControlLogService;
        }
        #region Create Log Cửa của NOIS
        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> CreateLogAsync([FromBody] AccessControlLogRequest request)
        {
            // Get api-key header to validate
            var apiKey = HttpContext.Request.Headers["api-key"];

            if (apiKey != _configuration["AccessControlLogApiKey"])
            {
                var dataResult = new ErrorResponseModel
                {
                    Status = false,
                    ErrorMessage = "Không có quyền truy cập !",
                };

                return Unauthorized(dataResult);
            }

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

            var url = HttpContext.Request.Path + HttpContext.Request.QueryString;

            var apiLogModel = new ApiLogModel<AccessControlLogRequest>
            {
                IpAddress = remoteIpAddress,
                Url = url,
                Method = Request.Method,
                Data = request
            };
            try
            {
                BackgroundJob.Enqueue<IAccessControlLogService>(x => x.CreateLogAsync(apiLogModel));
            }
            catch (Exception)
            {
                await _accessControlLogService.CreateLogAsync(apiLogModel);
            }
            return SuccessResult("Tạo dữ liệu log thành công");
        }
        #endregion
    }


}
