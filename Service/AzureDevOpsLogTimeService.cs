using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Enums;
using InternalPortal.ApplicationCore.Helpers;
using InternalPortal.ApplicationCore.Interfaces;
using InternalPortal.ApplicationCore.Interfaces.Business;
using InternalPortal.ApplicationCore.Interfaces.Utilities.AzureDevOps;
using InternalPortal.ApplicationCore.Models;
using InternalPortal.ApplicationCore.Models.AzureDevOps;
using InternalPortal.ApplicationCore.Models.ProjectManagement;

namespace InternalPortal.Infrastructure.Services.Business
{
    public class AzureDevOpsLogTimeService : IAzureDevOpsLogTimeService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAzureDevOpsService _azureDevOpsService;
        public AzureDevOpsLogTimeService(IUnitOfWork unitOfWork,
            IAzureDevOpsService azureDevOpsService)
        {
            _unitOfWork = unitOfWork;
            _azureDevOpsService = azureDevOpsService;
        }
        #region Private Methods
        private bool IsAllowToLogTime(List<ProjectTimesheetLogTime> logTimes, DateTime start, DateTime end)
        {
            if (logTimes.Count == 0)
            {
                return true;
            }
            foreach (var log in logTimes)
            {
                if (start < log.StopTime && end > log.StartTime)//nếu mốc thời gian input chồng lên logtime đang xét
                {
                    return false;
                }
            }
            return true;
        }
        #endregion
        public async Task<CombineResponseModel<ProjectTimeSheet>> CreateAsync(AzureDevOpsLogTimeRequest request, string email)
        {
            var res = new CombineResponseModel<ProjectTimeSheet>();
            if (request.StartDate > request.EndDate)
            {
                res.ErrorMessage = "Thời gian bắt đầu không lớn hơn hiện tại";
                return res;
            }

            var now = DateTime.UtcNow.UTCToIct();
            if (request.EndDate > now)
            {
                res.ErrorMessage = "Thời gian kết thúc không lớn hơn hiện tại";
                return res;
            }

            var project = await _unitOfWork.ProjectRepository.GetByAzureDevOpsProjectIdAsync(request.AzureDevOpsProjectId, request.ProjectId);
            if (project == null)
            {
                res.ErrorMessage = "Dự án không tồn tại";
                return res;
            }

            if (string.IsNullOrEmpty(project.AzureDevOpsKey)
                || string.IsNullOrEmpty(project.AzureDevOpsOrganization)
                || string.IsNullOrEmpty(project.AzureDevOpsProject))
            {
                res.ErrorMessage = "Dự án chưa có cấu hình của AzureDevops";
                return res;
            }

            var existMember = project.ProjectMembers.FirstOrDefault(t => t.DevOpsAccountEmail == email);
            if (existMember == null)
            {
                res.ErrorMessage = "Bạn không phải là thành viên của dự án này";
                return res;
            }

            var timeSheetExist = project.ProjectTimeSheets.FirstOrDefault(t => t.TaskId?.ToLower() == request.TaskId.ToLower() && t.CreatedDate.Date == request.StartDate.Date);
            var azureRequest = new AzureDevOpsTitleRequest
            {
                AzureDevOpsKey = project.AzureDevOpsKey,
                AzureDevOpsOrganization = project.AzureDevOpsOrganization,
                AzureDevOpsProject = project.AzureDevOpsProject,
                TaskId = request.TaskId,
            };
            var azureResponse = await _azureDevOpsService.GetAzureSystemTitleByTaskIdAsync(azureRequest);

            // Tìm giai đoạn đang InProcess của dự án
            var projectStageInProcess = project.ProjectStages.FirstOrDefault(o => o.Status == (int)EProjectStageStatus.InProcess);

            //nếu user chưa tạo timesheet thì tạo timesheet kèm logtime
            if (timeSheetExist == null)
            {
                var timeSheet = new ProjectTimeSheet()
                {
                    ProjectId = project.Id,
                    ProjectMemberId = existMember.Id,
                    TaskId = request.TaskId,
                    Description = azureResponse?.Fields != null ? azureResponse.Fields.SystemTitle : "",
                    CreatedDate = request.StartDate.Date,
                    Rate = 0,
                    IsBillable = false,
                    ProcessStatus = (int)EProcessStatus.Stop,
                    IsImport = true,
                    IssueType = azureResponse?.Fields != null ? azureResponse.Fields.SystemWorkItemType : "",
                    Priority = null,
                };

                var timeSheetLogTime = new ProjectTimesheetLogTime()
                {
                    StartTime = request.StartDate,
                    StopTime = request.EndDate,
                    IsImport = true,
                    LogWorkId = Guid.NewGuid().ToString(),
                    Comment = request.Comment,
                    TimeSpentSeconds = (int)(request.EndDate - request.StartDate).TotalSeconds,
                    IssueType = azureResponse?.Fields != null ? azureResponse.Fields.SystemWorkItemType : "",
                    ProjectStageId = projectStageInProcess?.Id
                };
                timeSheet.ProjectTimesheetLogTimes.Add(timeSheetLogTime);

                var estimateByTaskId = await _unitOfWork.ProjectTimesheetEstimateRepository.GetByTaskIdAndProjectIdAsync(request.TaskId, project.Id);
                var estimateTimeInSecond = azureResponse?.Fields != null && azureResponse.Fields.OriginalEstimate.HasValue ? (int)azureResponse.Fields.OriginalEstimate * 60 * 60 : 0;
                if (estimateByTaskId == null)
                {
                    await _unitOfWork.ProjectTimesheetEstimateRepository.CreateAsync(new ProjectTimesheetEstimate
                    {
                        ProjectId = project.Id,
                        TaskId = request.TaskId,
                        EstimateTimeInSecond = estimateTimeInSecond,
                        CreatedDate = DateTime.UtcNow.UTCToIct(),
                    });
                }
                else
                {
                    if (estimateByTaskId.EstimateTimeInSecond != estimateTimeInSecond)
                    {
                        estimateByTaskId.EstimateTimeInSecond = estimateTimeInSecond;
                        await _unitOfWork.ProjectTimesheetEstimateRepository.UpdateAsync(estimateByTaskId);
                    }
                }

                await _unitOfWork.ProjectTimesheetRepository.CreateAsync(timeSheet);
                await _unitOfWork.SaveChangesAsync();
                res.Status = true;
                res.Data = timeSheet;
                return res;
            }
            //kiểm tra logtime có trùng thời gian với các logtime khác hay không
            var sortedLogTimes = timeSheetExist.ProjectTimesheetLogTimes.OrderBy(log => log.StartTime).ToList();
            bool isAllow = IsAllowToLogTime(sortedLogTimes, request.StartDate, request.EndDate);

            if (!isAllow)
            {
                res.ErrorMessage = "Thời gian bị trùng lặp";
                return res;
            }

            var projectEstimate = await _unitOfWork.ProjectTimesheetEstimateRepository.GetByTaskIdAndProjectIdAsync(request.TaskId, project.Id);
            var etaInSecond = azureResponse?.Fields != null && azureResponse.Fields.OriginalEstimate.HasValue ? (int)azureResponse.Fields.OriginalEstimate * 60 * 60 : 0;
            if (projectEstimate == null)
            {
                await _unitOfWork.ProjectTimesheetEstimateRepository.CreateAsync(new ProjectTimesheetEstimate
                {
                    ProjectId = project.Id,
                    TaskId = request.TaskId,
                    EstimateTimeInSecond = etaInSecond,
                    CreatedDate = DateTime.UtcNow.UTCToIct(),
                });
            }
            else
            {
                if (projectEstimate.EstimateTimeInSecond != etaInSecond)
                {
                    projectEstimate.EstimateTimeInSecond = etaInSecond;
                    await _unitOfWork.ProjectTimesheetEstimateRepository.UpdateAsync(projectEstimate);
                }
            }

            // đã có timesheet thì tạo logtime
            var logTime = new ProjectTimesheetLogTime()
            {
                ProjectTimesheetId = timeSheetExist.Id,
                StartTime = request.StartDate,
                StopTime = request.EndDate,
                IsImport = true,
                LogWorkId = Guid.NewGuid().ToString(),
                Comment = request.Comment,
                TimeSpentSeconds = (int)(request.EndDate - request.StartDate).TotalSeconds,
                IssueType = azureResponse?.Fields != null ? azureResponse.Fields.SystemWorkItemType : "",
                ProjectStageId = projectStageInProcess?.Id
            };
            timeSheetExist.ProjectTimesheetLogTimes.Add(logTime);
            await _unitOfWork.ProjectTimesheetRepository.UpdateAsync(timeSheetExist);
            await _unitOfWork.SaveChangesAsync();
            res.Status = true;
            res.Data = timeSheetExist;
            return res;
        }

        public async Task<CombineResponseModel<ProjectTimesheetLogTime>> DeleteAsync(string logworkId, string email)
        {
            var res = new CombineResponseModel<ProjectTimesheetLogTime>();
            var logtime = await _unitOfWork.ProjectTimesheetLogTimeRepository.GetByLogworkId(logworkId);
            if (logtime == null)
            {
                res.ErrorMessage = "Log time không tồn tại";
                return res;
            }
            var isMyLogTime = logtime.ProjectTimesheet.ProjectMember.DevOpsAccountEmail.Equals(email, StringComparison.OrdinalIgnoreCase);
            if (!isMyLogTime)
            {
                res.ErrorMessage = "Không thể xóa task của người khác";
                return res;
            }
            await _unitOfWork.ProjectTimesheetLogTimeRepository.DeleteAsync(logtime);
            await _unitOfWork.SaveChangesAsync();

            res.Status = true;
            res.Data = logtime;
            return res;
        }

        public async Task<CombineResponseModel<ProjectTimesheetLogTime>> UpdateAsync(string logworkId, AzureDevOpsLogTimeUpdateRequest request, string email)
        {
            var res = new CombineResponseModel<ProjectTimesheetLogTime>();
            if (request.StartDate > request.EndDate)
            {
                res.ErrorMessage = "Thời gian kết thúc không được nhỏ hơn bắt đầu";
                return res;
            }
            var now = DateTime.UtcNow.UTCToIct();
            if (request.EndDate > now)
            {
                res.ErrorMessage = "Thời gian kết thúc không lớn hơn hiện tại";
                return res;
            }

            var logtime = await _unitOfWork.ProjectTimesheetLogTimeRepository.GetByLogworkId(logworkId);
            if (logtime == null)
            {
                res.ErrorMessage = "Log time không tồn tại";
                return res;
            }

            var project = logtime.ProjectTimesheet.Project;

            if (string.IsNullOrEmpty(project.AzureDevOpsKey)
                || string.IsNullOrEmpty(project.AzureDevOpsOrganization)
                || string.IsNullOrEmpty(project.AzureDevOpsProject))
            {
                res.ErrorMessage = "Dự án chưa có cấu hình của AzureDevops";
                return res;
            }

            if (string.IsNullOrEmpty(logtime.ProjectTimesheet.ProjectMember.DevOpsAccountEmail))
            {
                res.ErrorMessage = "Logt time không hợp lệ";
                return res;
            }

            var isMyLogTime = logtime.ProjectTimesheet.ProjectMember.DevOpsAccountEmail.Equals(email, StringComparison.OrdinalIgnoreCase);
            if (!isMyLogTime)
            {
                res.ErrorMessage = "Không thể sửa task của người khác";
                return res;
            }

            var azureRequest = new AzureDevOpsTitleRequest
            {
                AzureDevOpsKey = project.AzureDevOpsKey,
                AzureDevOpsOrganization = project.AzureDevOpsOrganization,
                AzureDevOpsProject = project.AzureDevOpsProject,
                TaskId = logtime.ProjectTimesheet.TaskId!,
            };
            var azureResponse = await _azureDevOpsService.GetAzureSystemTitleByTaskIdAsync(azureRequest);

            var projectStageInProcess = project.ProjectStages.FirstOrDefault(o => o.Status == (int)EProjectStageStatus.InProcess);
            //nếu ngày update khác ngày logtime thì tạo mới timesheet
            if (logtime.StartTime.Date != request.StartDate.Date)
            {
                var timeSheet = new ProjectTimeSheet()
                {
                    ProjectId = logtime.ProjectTimesheet.ProjectId,
                    ProjectMemberId = logtime.ProjectTimesheet.ProjectMember.Id,
                    TaskId = logtime.ProjectTimesheet.TaskId,
                    Description = azureResponse?.Fields != null ? azureResponse.Fields.SystemTitle : "",
                    CreatedDate = request.StartDate,
                    Rate = 0,
                    IsBillable = false,
                    ProcessStatus = (int)EProcessStatus.Stop,
                    IsImport = true,
                    IssueType = azureResponse?.Fields != null ? azureResponse.Fields.SystemWorkItemType : "",
                    Priority = null,
                };
                var timeSheetLogTime = new ProjectTimesheetLogTime()
                {
                    StartTime = request.StartDate,
                    StopTime = request.EndDate,
                    IsImport = true,
                    LogWorkId = Guid.NewGuid().ToString(),
                    Comment = request.Comment,
                    TimeSpentSeconds = (int)(request.EndDate - request.StartDate).TotalSeconds,
                    IssueType = azureResponse?.Fields != null ? azureResponse.Fields.SystemWorkItemType : "",
                    ProjectStageId = projectStageInProcess?.Id
                };
                timeSheet.ProjectTimesheetLogTimes.Add(timeSheetLogTime);
                await _unitOfWork.ProjectTimesheetRepository.CreateAsync(timeSheet);

                var estimateByTaskId = await _unitOfWork.ProjectTimesheetEstimateRepository.GetByTaskIdAndProjectIdAsync(logtime.ProjectTimesheet.TaskId!, project.Id);
                var estimateTimeInSecond = azureResponse?.Fields != null && azureResponse.Fields.OriginalEstimate.HasValue ? (int)azureResponse.Fields.OriginalEstimate * 60 * 60 : 0;
                if (estimateByTaskId == null)
                {
                    await _unitOfWork.ProjectTimesheetEstimateRepository.CreateAsync(new ProjectTimesheetEstimate
                    {
                        ProjectId = project.Id,
                        TaskId = logtime.ProjectTimesheet.TaskId!,
                        EstimateTimeInSecond = estimateTimeInSecond,
                        CreatedDate = DateTime.UtcNow.UTCToIct(),
                    });
                }
                else
                {
                    if (estimateByTaskId.EstimateTimeInSecond != estimateTimeInSecond)
                    {
                        estimateByTaskId.EstimateTimeInSecond = estimateTimeInSecond;
                        await _unitOfWork.ProjectTimesheetEstimateRepository.UpdateAsync(estimateByTaskId);
                    }
                }

                await _unitOfWork.SaveChangesAsync();
                res.Status = true;
                res.Data = timeSheetLogTime;
                return res;
            }
            //kiểm tra logtime có trùng thời gian với các logtime khác hay không
            var sortedLogTimes = logtime.ProjectTimesheet.ProjectTimesheetLogTimes.OrderBy(log => log.StartTime).ToList();
            sortedLogTimes.Remove(logtime);
            bool isAllow = IsAllowToLogTime(sortedLogTimes, request.StartDate, request.EndDate);

            if (!isAllow)
            {
                res.ErrorMessage = "Thời gian bị trùng lặp";
                return res;
            }
            logtime.StartTime = request.StartDate;
            logtime.StopTime = request.EndDate;
            logtime.Comment = request.Comment;
            logtime.IssueType = azureResponse?.Fields != null ? azureResponse.Fields.SystemWorkItemType : "";
            logtime.TimeSpentSeconds = (int)(request.EndDate - request.StartDate).TotalSeconds;
            logtime.ProjectStageId = projectStageInProcess?.Id;
            sortedLogTimes.Add(logtime);

            var projectEstimate = await _unitOfWork.ProjectTimesheetEstimateRepository.GetByTaskIdAndProjectIdAsync(logtime.ProjectTimesheet.TaskId!, project.Id);
            var etaInSecond = azureResponse?.Fields != null && azureResponse.Fields.OriginalEstimate.HasValue ? (int)azureResponse.Fields.OriginalEstimate * 60 * 60 : 0;
            if (projectEstimate == null)
            {
                await _unitOfWork.ProjectTimesheetEstimateRepository.CreateAsync(new ProjectTimesheetEstimate
                {
                    ProjectId = project.Id,
                    TaskId = logtime.ProjectTimesheet.TaskId!,
                    EstimateTimeInSecond = etaInSecond,
                    CreatedDate = DateTime.UtcNow.UTCToIct(),
                });
            }
            else
            {
                if (projectEstimate.EstimateTimeInSecond != etaInSecond)
                {
                    projectEstimate.EstimateTimeInSecond = etaInSecond;
                    await _unitOfWork.ProjectTimesheetEstimateRepository.UpdateAsync(projectEstimate);
                }
            }

            await _unitOfWork.ProjectTimesheetLogTimeRepository.UpdateAsync(logtime);
            await _unitOfWork.SaveChangesAsync();

            res.Status = true;
            res.Data = logtime;
            return res;
        }
    }
}
