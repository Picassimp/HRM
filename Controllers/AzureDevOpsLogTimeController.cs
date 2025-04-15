using InternalPortal.API.Filters;
using InternalPortal.ApplicationCore.Interfaces;
using InternalPortal.ApplicationCore.Interfaces.Business;
using InternalPortal.ApplicationCore.Models;
using InternalPortal.ApplicationCore.Models.ProjectManagement;
using Microsoft.AspNetCore.Mvc;

namespace InternalPortal.API.Controllers
{
    [ApiController]
    [Route("[controller]")]
    [TypeFilter(typeof(AzureDevOpsFilter))]
    public class AzureDevOpsLogTimeController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAzureDevOpsLogTimeService _azureDevOpsLogTimeService;
        public AzureDevOpsLogTimeController(IUnitOfWork unitOfWork,
            IAzureDevOpsLogTimeService azureDevOpsLogTimeService)
        {
            _unitOfWork = unitOfWork;
            _azureDevOpsLogTimeService = azureDevOpsLogTimeService;
        }

        [HttpPost]
        [Route("log-time")]
        public async Task<IActionResult> Create([FromBody] AzureDevOpsLogTimeRequest request)
        {
            HttpContext.Request.Headers.TryGetValue("ADEmail", out var email);
            HttpContext.Request.Headers.TryGetValue("ProjectKey", out var projectKey);
            request.AzureDevOpsProjectId = projectKey.ToString();
            var res = await _azureDevOpsLogTimeService.CreateAsync(request, email.ToString());
            if (!res.Status || res.Data == null || !string.IsNullOrEmpty(res.ErrorMessage))
            {
                return ErrorResult(res.ErrorMessage ?? "Lỗi khi tạo Log time");
            }
            return SuccessResult("Tạo Log time thành công");
        }
        [HttpPut]
        [Route("log-time/{logWorkId}")]
        public async Task<IActionResult> Update([FromRoute] string logWorkId, [FromBody] AzureDevOpsLogTimeUpdateRequest request)
        {
            HttpContext.Request.Headers.TryGetValue("ADEmail", out var email);
            var res = await _azureDevOpsLogTimeService.UpdateAsync(logWorkId, request, email.ToString());
            if (!res.Status || res.Data == null || !string.IsNullOrEmpty(res.ErrorMessage))
            {
                return ErrorResult(res.ErrorMessage ?? "Lỗi khi cập nhật Log time");
            }

            return SuccessResult("Cập nhật log time thành công");
        }

        [HttpDelete]
        [Route("log-time/{logWorkId}")]
        public async Task<IActionResult> Delete([FromRoute] string logWorkId, [FromQuery] string projectId)
        {
            HttpContext.Request.Headers.TryGetValue("ADEmail", out var email);
            var res = await _azureDevOpsLogTimeService.DeleteAsync(logWorkId, email.ToString());
            if (!res.Status || res.Data == null || !string.IsNullOrEmpty(res.ErrorMessage))
            {
                return ErrorResult(res.ErrorMessage ?? "Lỗi khi xóa Log time");
            }
            return SuccessResult("Xóa log time thành công");
        }
        [HttpGet]
        [Route("{taskId}")]
        public async Task<IActionResult> GetLogTimeAzure([FromRoute] string taskId, [FromQuery] string projectId)
        {
            HttpContext.Request.Headers.TryGetValue("ProjectKey", out var projectKey);
            var project = await _unitOfWork.ProjectRepository.GetByAzureDevOpsProjectIdAsync(projectKey.ToString(), projectId);
            var res = await _unitOfWork.ProjectTimesheetLogTimeRepository.GetLogTimeAzureAsync(taskId, project.Id);
            return SuccessResult(res);
        }

        /// <summary>
        /// prepare error result
        /// </summary>
        /// <param name="errorMessages"></param>
        /// <returns></returns>
        [NonAction]
        private IActionResult ErrorResult(string errorMessages)
        {
            var dataResult = new ErrorResponseModel
            {
                Status = false,
                ErrorMessage = errorMessages,
            };
            return Ok(dataResult);
        }

        /// <summary>
        /// prepare success result
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        [NonAction]
        private IActionResult SuccessResult(string message)
        {
            var dataResult = new SuccessResponseModel<object>
            {
                Status = true,
                Message = message,
            };
            return Ok(dataResult);
        }

        /// <summary>
        /// prepare success result
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        [NonAction]
        private IActionResult SuccessResult(object obj, string message = "")
        {
            var dataResult = new SuccessResponseModel<object>
            {
                Status = true,
                Message = message,
                Data = obj,
            };
            return Ok(dataResult);
        }
    }
}
