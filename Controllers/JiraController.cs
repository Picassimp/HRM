using InternalPortal.ApplicationCore.Interfaces;
using InternalPortal.ApplicationCore.Interfaces.Utilities.Jira;
using InternalPortal.ApplicationCore.Models.Jira;
using InternalPortal.ApplicationCore.Models.ProjectManagement;
using InternalPortal.ApplicationCore.ValueObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InternalPortal.API.Controllers
{
    public class JiraController : BaseApiController
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IJiraService _jiraService;
        public JiraController(IUnitOfWork unitOfWork,
            IJiraService jiraService)
        {
            _unitOfWork = unitOfWork;
            _jiraService = jiraService;
        }
        /// <summary>
        /// Lấy title theo JiraKey
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("title")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> GetTitleByTaskId([FromQuery] TaskRequest request)
        {
            var project = await _unitOfWork.ProjectRepository.GetByIdAsync(request.ProjectId);
            if (project == null)
            {
                return ErrorResult("Dự án không tồn tại");
            }
            var jiraRequest = new JiraRequest
            {
                TaskId = request.TaskId,    
                JiraDomain = project.JiraDomain!,
                JiraKey = project.JiraKey!,
                JiraUser = project.JiraUser!
            };
            var jiraTask = await _jiraService.GetTaskSummaryByTaskIdAsync(jiraRequest);
            return SuccessResult(jiraTask);
        }
    }
}
