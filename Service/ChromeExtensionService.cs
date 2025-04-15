using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Enums;
using InternalPortal.ApplicationCore.Helpers;
using InternalPortal.ApplicationCore.Interfaces;
using InternalPortal.ApplicationCore.Interfaces.Business;
using InternalPortal.ApplicationCore.Interfaces.Utilities.AzureDevOps;
using InternalPortal.ApplicationCore.Interfaces.Utilities.Jira;
using InternalPortal.ApplicationCore.Models;
using InternalPortal.ApplicationCore.Models.AzureDevOps;
using InternalPortal.ApplicationCore.Models.ChromeExtension;
using InternalPortal.ApplicationCore.Models.Jira;
using InternalPortal.ApplicationCore.Models.User;

namespace InternalPortal.Infrastructure.Services.Business
{
    public class ChromeExtensionService : IChromeExtensionService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAzureDevOpsService _azureDevOpsService;
        private readonly IJiraService _jiraService;

        public ChromeExtensionService(
            IUnitOfWork unitOfWork,
            IAzureDevOpsService azureDevOpsService,
            IJiraService jiraService
            )
        {
            _unitOfWork = unitOfWork;
            _azureDevOpsService = azureDevOpsService;
            _jiraService = jiraService;
        }

        private async Task<string> GetTaskDescriptionAsync(string taskId, ChromeExtensionProjectDtoModel model)
        {
            var summary = string.Empty;
            if (model.Integration == (int)EIntegrationService.Jira)
            {
                var jiraRequest = new JiraRequest
                {
                    TaskId = taskId,
                    JiraDomain = model.JiraDomain!,
                    JiraUser = model.JiraUser!,
                    JiraKey = model.JiraKey!
                };
                var response = await _jiraService.GetTaskSummaryByTaskIdAsync(jiraRequest);
                summary = response.Summary ?? string.Empty;
            }
            else if (model.Integration == (int)EIntegrationService.AzureDevOps)
            {
                var azureDevOpsRequest = new AzureDevOpsTitleRequest
                {
                    TaskId = taskId,
                    AzureDevOpsKey = model.AzureDevOpsKey!,
                    AzureDevOpsProject = model.AzureDevOpsProject!,
                    AzureDevOpsOrganization = model.AzureDevOpsOrganization!
                };
                var response = await _azureDevOpsService.GetAzureSystemTitleByTaskIdAsync(azureDevOpsRequest);
                summary = response?.Fields.SystemTitle ?? string.Empty;
            }
            return summary;
        }

        public async Task<ChromeExtensionResponse> GetTimesheetByUserIdAndDateAsync(UserDtoModel user)
        {
            var now = DateTime.UtcNow.UTCToIct();
            var rawData = await _unitOfWork.ProjectTimesheetRepository.GetTimesheetForExtensionByUserIdAndDateAsync(user.Id, now.Date);
            var response = new ChromeExtensionResponse
            {
                UserName = user.FullName!,
                ChromeExtensionProjectResponses = rawData.GroupBy(o => new { o.ProjectId, o.ProjectName, o.Integration }).Select(y => new ChromeExtensionProjectResponse
                {
                    ProjectId = y.Key.ProjectId,
                    ProjectName = y.Key.ProjectName,
                    Integration = y.Key.Integration,
                    ChromeExtensionTaskResponses = y.Where(y => y.ProjectTimesheetId.HasValue).Select(z => new ChromeExtensionTaskResponse
                    {
                        TaskId = z.TaskId,
                        ProjectTimesheetId = z.ProjectTimesheetId,
                        WorkingTimeInMinute = z.WorkingTimeInMinute,
                        ProcessStatus = z.ProcessStatus
                    }).ToList()
                }).ToList()
            };

            return response;
        }

        public async Task<CombineResponseModel<ChromeExtensionStartTaskRequest>> PrepareStartTaskAsync(int userId, ChromeExtensionStartTaskRequest request)
        {
            var res = new CombineResponseModel<ChromeExtensionStartTaskRequest>();
            var project = await _unitOfWork.ProjectRepository.GetByIdAsync(request.ProjectId);
            if (project == null)
            {
                res.ErrorMessage = "Dự án không tồn tại";
                return res;
            }

            var projectStageInProcess = project.ProjectStages.FirstOrDefault(o => o.Status == (int)EProjectStageStatus.InProcess);

            ProjectTimeSheet? task = null;
            var now = DateTime.UtcNow.UTCToIct();
            if (request.ProjectTimesheetId.HasValue)
            {
                task = await _unitOfWork.ProjectTimesheetRepository.GetByIdAsync(request.ProjectTimesheetId.Value);
                if (task == null)
                {
                    res.ErrorMessage = "Task không tồn tại";
                    return res;
                }

                if (task.ProjectId != project.Id)
                {
                    res.ErrorMessage = $"Task không thuộc dự án {project.Name}";
                    return res;
                }

                if (task.ProjectMember.UserInternalId != userId)
                {
                    res.ErrorMessage = "Task này không thuộc về bạn";
                    return res;
                }

                var listTaskLogTime = task.ProjectTimesheetLogTimes.ToList();
                if (listTaskLogTime.Any())
                {
                    var lastLogTime = listTaskLogTime.Last();

                    // Kiểm tra task đó đã bắt đầu hay chưa
                    if (lastLogTime.StopTime == null)
                    {
                        res.ErrorMessage = "Task đã được bắt đầu";
                        return res;
                    }
                }

                // Tìm các task đang chạy khác của người đó
                var runningTasks = await _unitOfWork.ProjectTimesheetRepository.GetRunningByUserIdAsync(userId);
                foreach (var runningTask in runningTasks)
                {
                    var projectTimesheetLogTimes = runningTask.ProjectTimesheetLogTimes.ToList();
                    var lastItemLogtime = projectTimesheetLogTimes.Last();
                    if (lastItemLogtime != null)
                    {
                        if (lastItemLogtime.StopTime == null)
                        {
                            lastItemLogtime.StopTime = now;
                            lastItemLogtime.TimeSpentSeconds = (int)(now - lastItemLogtime.StartTime).TotalSeconds;

                            await _unitOfWork.ProjectTimesheetLogTimeRepository.UpdateAsync(lastItemLogtime);
                        }
                    }
                    runningTask.ProcessStatus = (int)EProcessStatus.Stop;
                    await _unitOfWork.ProjectTimesheetRepository.UpdateAsync(runningTask);
                }

                // Nếu đã có Id và có thay đổi TaskId thì cập nhật TaskId & Task Description
                if (!string.Equals(task.TaskId, request.TaskId, StringComparison.OrdinalIgnoreCase))
                {
                    var projectDtoModel = new ChromeExtensionProjectDtoModel
                    {
                        Integration = project.Integration,
                        AzureDevOpsKey = project.AzureDevOpsKey,
                        AzureDevOpsProject = project.AzureDevOpsProject,
                        AzureDevOpsOrganization = project.AzureDevOpsOrganization,
                        JiraDomain = project.JiraDomain,
                        JiraKey = project.JiraKey,
                        JiraUser = project.JiraUser
                    };
                    var taskDescription = await GetTaskDescriptionAsync(request.TaskId, projectDtoModel);
                    task.TaskId = request.TaskId;
                    task.Description = taskDescription;
                }

                var newLogTime = new ProjectTimesheetLogTime
                {
                    ProjectTimesheetId = task.Id,
                    StartTime = now,
                    ProjectStageId = projectStageInProcess?.Id
                };
                await _unitOfWork.ProjectTimesheetLogTimeRepository.CreateAsync(newLogTime);

                task.ProcessStatus = (int)EProcessStatus.Running;
                await _unitOfWork.ProjectTimesheetRepository.UpdateAsync(task);
            }
            else
            {
                var projectMember = project.ProjectMembers.FirstOrDefault(o => o.UserInternalId == userId && o.IsActive && !o.IsDeleted);
                if (projectMember != null)
                {
                    // Tìm các task đang chạy khác của người đó
                    var runningTasks = await _unitOfWork.ProjectTimesheetRepository.GetRunningByUserIdAsync(userId);
                    foreach (var runningTask in runningTasks)
                    {
                        var projectTimesheetLogTimes = runningTask.ProjectTimesheetLogTimes.ToList();
                        var lastItemLogtime = projectTimesheetLogTimes.Last();
                        if (lastItemLogtime != null)
                        {
                            if (lastItemLogtime.StopTime == null)
                            {
                                lastItemLogtime.StopTime = now;
                                lastItemLogtime.TimeSpentSeconds = (int)(now - lastItemLogtime.StartTime).TotalSeconds;
                                await _unitOfWork.ProjectTimesheetLogTimeRepository.UpdateAsync(lastItemLogtime);
                            }
                        }
                        runningTask.ProcessStatus = (int)EProcessStatus.Stop;
                        await _unitOfWork.ProjectTimesheetRepository.UpdateAsync(runningTask);
                    }

                    var projectDtoModel = new ChromeExtensionProjectDtoModel
                    {
                        Integration = project.Integration,
                        AzureDevOpsKey = project.AzureDevOpsKey,
                        AzureDevOpsProject = project.AzureDevOpsProject,
                        AzureDevOpsOrganization = project.AzureDevOpsOrganization,
                        JiraDomain = project.JiraDomain,
                        JiraKey = project.JiraKey,
                        JiraUser = project.JiraUser
                    };
                    var taskDescription = await GetTaskDescriptionAsync(request.TaskId, projectDtoModel);
                    
                    task = new ProjectTimeSheet
                    {
                        ProjectId = request.ProjectId,
                        ProjectMemberId = projectMember.Id,
                        TaskId = request.TaskId,
                        Description = taskDescription,
                        CreatedDate = now.Date,
                        Rate = null,
                        IsBillable = false,
                        ProcessStatus = (int)EProcessStatus.Running,
                        IsImport = false,
                        IssueType = null,
                        Priority = null,
                        ProjectTimesheetLogTimes = new List<ProjectTimesheetLogTime>
                        {
                            new()
                            {
                                StartTime = now,
                                StopTime = null,
                                IsImport = false,
                                LogWorkId = null,
                                Comment = null,
                                TimeSpentSeconds = 0,
                                IssueType = null,
                                IsBillable = true,
                                ProjectStageId = projectStageInProcess?.Id
                            }
                        }
                    };
                    await _unitOfWork.ProjectTimesheetRepository.CreateAsync(task);
                }
            }
            await _unitOfWork.SaveChangesAsync();

            res.Status = true;
            res.Data = request;
            return res;
        }

        public async Task<CombineResponseModel<ChromeExtensionStopTaskRequest>> PrepareStopTaskAsync(int userId, ChromeExtensionStopTaskRequest request)
        {
            var res = new CombineResponseModel<ChromeExtensionStopTaskRequest>();

            var project = await _unitOfWork.ProjectRepository.GetByIdAsync(request.ProjectId);
            if (project == null)
            {
                res.ErrorMessage = "Dự án không tồn tại";
                return res;
            }

            var task = await _unitOfWork.ProjectTimesheetRepository.GetByIdAsync(request.ProjectTimesheetId);
            if (task == null)
            {
                res.ErrorMessage = "Task không tồn tại";
                return res;
            }

            if (task.ProjectId != project.Id)
            {
                res.ErrorMessage = $"Task không thuộc dự án {project.Name}";
                return res;
            }

            if (task.ProjectMember.UserInternalId != userId)
            {
                res.ErrorMessage = "Task này không thuộc về bạn";
                return res;
            }

            // Lấy ra list LogTime của task
            var listTaskLogTime = task.ProjectTimesheetLogTimes.ToList();
            var runningLog = listTaskLogTime.Any() ? listTaskLogTime.Last() : null;

            // Nếu có logtime
            if (runningLog == null)
            {
                res.ErrorMessage = "LogTime không tồn tại";
                return res;
            }

            if (runningLog.StopTime.HasValue)
            {
                res.ErrorMessage = "Task đã dừng";
                return res;
            }

            var stopTime = DateTime.UtcNow.UTCToIct();
            runningLog.StopTime = stopTime;
            runningLog.TimeSpentSeconds = (int)(stopTime - runningLog.StartTime).TotalSeconds;
            await _unitOfWork.ProjectTimesheetLogTimeRepository.UpdateAsync(runningLog);

            task.ProcessStatus = (int)EProcessStatus.Stop;
            await _unitOfWork.ProjectTimesheetRepository.UpdateAsync(task);

            await _unitOfWork.SaveChangesAsync();
            res.Status = true;
            res.Data = request;
            return res;
        }
    }
}
