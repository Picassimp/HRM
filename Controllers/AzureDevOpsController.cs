using InternalPortal.ApplicationCore.Interfaces;
using InternalPortal.ApplicationCore.Interfaces.Utilities.AzureDevOps;
using InternalPortal.ApplicationCore.Models.AzureDevOps;
using InternalPortal.ApplicationCore.Models.ProjectManagement;
using InternalPortal.ApplicationCore.ValueObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InternalPortal.API.Controllers
{
    public class AzureDevOpsController : BaseApiController
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAzureDevOpsService _azureDevOpsService;
        public AzureDevOpsController(IAzureDevOpsService azureDevOpsService,
            IUnitOfWork unitOfWork)
        {
            _azureDevOpsService = azureDevOpsService;
            _unitOfWork = unitOfWork;
        }
        [HttpGet("title")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> GetAzureTitleByIdAsync([FromQuery] TaskRequest request)
        {
            var project = await _unitOfWork.ProjectRepository.GetByIdAsync(request.ProjectId);
            if(project == null)
            {
                return ErrorResult("Dự án không tồn tại");
            }
            var azureRequest = new AzureDevOpsTitleRequest
            {
                TaskId = request.TaskId,
                AzureDevOpsKey = project.AzureDevOpsKey!,
                AzureDevOpsOrganization = project.AzureDevOpsOrganization!,
                AzureDevOpsProject = project.AzureDevOpsProject! 
            };
            var azure = await _azureDevOpsService.GetAzureSystemTitleByTaskIdAsync(azureRequest);
            var response = new AzureSummaryModel
            {
                Summary = azure?.Fields != null ? azure.Fields.SystemTitle : "",
                IssueType = azure?.Fields != null ? azure.Fields.SystemWorkItemType : "",
                EstimateTimeInSecond = azure?.Fields != null  && azure.Fields.OriginalEstimate.HasValue ? (int)(azure.Fields.OriginalEstimate * 60 * 60) : 0 // convert từ decimal hour sang int second
            };
            return SuccessResult(response);
        }
    }
}
