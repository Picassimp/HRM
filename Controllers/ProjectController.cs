using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Enums;
using InternalPortal.ApplicationCore.Helpers;
using InternalPortal.ApplicationCore.Interfaces;
using InternalPortal.ApplicationCore.Models.Holiday;
using InternalPortal.ApplicationCore.Models.ProjectManagement;
using InternalPortal.ApplicationCore.Models.ProjectModels;
using InternalPortal.ApplicationCore.ValueObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InternalPortal.API.Controllers
{
    public class ProjectController : BaseApiController
    {
        private readonly IUnitOfWork _unitOfWork;
        public ProjectController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        #region Private Method
        private bool CheckUserHasPermission(Project project, int userId)
        {
            // Kiểm tra user đó có phải là PM hoặc Owner của dự án không
            var isPmOrOwnerOfProject = project.ProjectMembers.Any(o => o.IsActive && !o.IsDeleted && o.UserInternalId == userId
                    && (o.Role == (int)EProjectRole.ProjectManager || o.Role == (int)EProjectRole.Owner));

            // Kiểm tra user đó có phải là người tạo dự án không
            var isProjectCreater = project.CreatedByUserId == userId;

            return isProjectCreater || isPmOrOwnerOfProject;
        }
        #endregion

        [HttpGet("dropdown")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> GetProjectDropdownAsync()
        {
            var res = await _unitOfWork.ProjectRepository.GetListProjectAsync();
            var response = res.Count > 0 ? res.Select(t=>new ProjectDropdownResponse()
            {
                Id = t.Id,
                Name = t.Name
            }).ToList() : new List<ProjectDropdownResponse>();
            return SuccessResult(response);
        }

        [HttpGet("{id}/info")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> GetProjectInfoAsync([FromRoute] int id)
        {
            var user = GetCurrentUser();

            var project = await _unitOfWork.ProjectRepository.GetByIdAsync(id);
            if (project == null)
            {
                return ErrorResult("Dự án không tồn tại");
            }

            if (!CheckUserHasPermission(project, user.Id))
            {
                return ErrorResult("Bạn không phải là quản lý của dự án này");
            }

            var response = new ProjectInfoResponse
            {
                Name = project.Name ?? "",
                ClientId = project.ClientId,
                Note = project.Note ?? "",
                StartDate = project.StartDate,
                ExpectedWorkingHours = project.ExpectedWorkingHours,
                ExpectedFinishDate = project.ExpectedFinishDate
            };

            return SuccessResult(response);
        }

        [HttpPut("{id}/info")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> UpdateProjectInfoAsync([FromRoute] int id, [FromBody] ProjectInfoRequest request)
        {
            var user = GetCurrentUser();

            if (request.StartDate.HasValue && request.ExpectedFinishDate.HasValue && request.ExpectedFinishDate.Value < request.StartDate.Value)
            {
                return ErrorResult("Ngày kết thúc phải lớn hơn ngày bắt đầu");
            }

            if (request.ExpectedWorkingHours.HasValue && request.ExpectedWorkingHours.Value <= 0)
            {
                return ErrorResult("Số giờ dự kiến làm việc phải lớn hớn 0");
            }

            var project = await _unitOfWork.ProjectRepository.GetByIdAsync(id);
            if (project == null)
            {
                return ErrorResult("Dự án không tồn tại");
            }

            if (!CheckUserHasPermission(project, user.Id))
            {
                return ErrorResult("Bạn không phải là quản lý của dự án này");
            }

            var projectName = request.Name.Trim();
            if (!project.Name.Equals(projectName, StringComparison.OrdinalIgnoreCase))
            {
                if (project.ProjectTimeSheets.Any(o => o.ProjectTimesheetLogTimes.Any()))
                {
                    return ErrorResult("Dự án đã có log timesheet! Không cho phép cập nhật");
                }

                var existProject = await _unitOfWork.ProjectRepository.GetByProjectNameAndClientIdAsync(projectName, request.ClientId);
                if (existProject != null)
                {
                    return ErrorResult($"Dự án '{projectName}' đã tồn tại");
                }
                project.Name = projectName;
            }

            if (project.ClientId != request.ClientId)
            {
                if (project.ProjectTimeSheets.Any(o => o.ProjectTimesheetLogTimes.Any()))
                {
                    return ErrorResult("Dự án đã có log timesheet! Không cho phép cập nhật");
                }

                var client = await _unitOfWork.ClientRepository.GetByIdAsync(request.ClientId);
                if (client == null)
                {
                    return ErrorResult("Công ty không tồn tại");
                }

                project.ClientId = client.Id;
            }

            project.StartDate = request.StartDate;
            project.ExpectedWorkingHours = request.ExpectedWorkingHours;
            project.ExpectedFinishDate = request.ExpectedFinishDate;
            project.Note = request.Note;
            await _unitOfWork.ProjectRepository.UpdateAsync(project);
            await _unitOfWork.SaveChangesAsync();

            return SuccessResult("Cập nhật thông tin dự án thành công");
        }

        [HttpGet("{id}/integration")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> ManagerGetProjectSettingAsync([FromRoute] int id)
        {
            var user = GetCurrentUser();
            var project = await _unitOfWork.ProjectRepository.GetByIdAsync(id);
            if (project == null)
            {
                return ErrorResult("Dự án không tồn tại");
            }

            if (!CheckUserHasPermission(project, user.Id))
            {
                return ErrorResult("Bạn không phải là quản lý của dự án này");
            }

            var response = new ProjectIntegrationResponse
            {
                Integration = (EIntegrationService)project.Integration,
                AzureDevOpsKey = project.AzureDevOpsKey ?? "",
                AzureDevOpsOrganization = project.AzureDevOpsOrganization ?? "",
                AzureDevOpsProject = project.AzureDevOpsProject ?? "",
                AzureDevOpsProjectId = project.AzureDevOpsProjectId ?? "",
                JiraKey = project.JiraKey ?? "",
                JiraDomain = project.JiraDomain ?? "",
                JiraUser = project.JiraUser ?? "",
                JiraProjectId = project.JiraProjectId ?? "",
                IsAutoLogWork = project.IsAutoLogWork
            };

            return SuccessResult(response);
        }

        [HttpPut("{id}/integration")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> UpdateProjectIntegrationAsync([FromRoute] int id, [FromBody] ProjectIntegrationRequest request)
        {
            var user = GetCurrentUser();
            var isAllowAutoLogWork = !string.IsNullOrEmpty(request.JiraDomain)
                && !string.IsNullOrEmpty(request.JiraProjectId)
                && !string.IsNullOrEmpty(request.JiraKey)
                && !string.IsNullOrEmpty(request.JiraUser)
                && request.Integration == EIntegrationService.Jira
                ? true : false;
            if (request.IsAutoLogWork && !isAllowAutoLogWork)
            {
                return ErrorResult("Vui lòng nhập đủ thông tin để cho phép thực hiện cập nhật");
            }
            var project = await _unitOfWork.ProjectRepository.GetByIdAsync(id);
            if (project == null)
            {
                return ErrorResult("Dự án không tồn tại");
            }

            if (!CheckUserHasPermission(project, user.Id))
            {
                return ErrorResult("Bạn không phải là quản lý của dự án này");
            }
  
            project.Integration = (int)request.Integration;

            if (request.Integration == EIntegrationService.Jira)
            {
                if (string.IsNullOrEmpty(request.JiraKey))
                {
                    return ErrorResult("JiraKey không được trống");
                }
                if (string.IsNullOrEmpty(request.JiraDomain))
                {
                    return ErrorResult("JiraDomain không được trống");
                }
                if (string.IsNullOrEmpty(request.JiraUser))
                {
                    return ErrorResult("JiraUser không được trống");
                }
                if (string.IsNullOrEmpty(request.JiraProjectId))
                {
                    return ErrorResult("JiraProjectId không được trống");
                }
                project.JiraKey = request.JiraKey;
                project.JiraDomain = request.JiraDomain;
                project.JiraUser = request.JiraUser;
                project.JiraProjectId = request.JiraProjectId;
                project.AzureDevOpsKey = null;
                project.AzureDevOpsProject = null;
                project.AzureDevOpsOrganization = null;
                project.AzureDevOpsProjectId = null;
                project.IsAutoLogWork = request.IsAutoLogWork;
            }
            else if (request.Integration == EIntegrationService.AzureDevOps)
            {
                if (string.IsNullOrEmpty(request.AzureDevOpsKey))
                {
                    return ErrorResult("AzureDevOpsKey không được trống");
                }
                if (string.IsNullOrEmpty(request.AzureDevOpsProject))
                {
                    return ErrorResult("AzureDevOpsProject không được trống");
                }
                if (string.IsNullOrEmpty(request.AzureDevOpsOrganization))
                {
                    return ErrorResult("AzureDevOpsOrganization không được trống");
                }

                // project has the same organization will use the same AzureDevOpsProjectId
                var projectHasSameAzureDevopsOrganization = await _unitOfWork.ProjectRepository.GetByAzureDevOpsOrganizationAsync(request.AzureDevOpsOrganization);
                var azureDevOpsOrganizationId = string.Empty;
                if (projectHasSameAzureDevopsOrganization == null || string.IsNullOrEmpty(projectHasSameAzureDevopsOrganization.AzureDevOpsProjectId))
                {
                    azureDevOpsOrganizationId = Guid.NewGuid().ToString();
                }
                else
                {
                    azureDevOpsOrganizationId = projectHasSameAzureDevopsOrganization.AzureDevOpsProjectId;
                }
                project.JiraKey = null;
                project.JiraDomain = null;
                project.JiraUser = null;
                project.JiraProjectId = null;
                project.AzureDevOpsKey = request.AzureDevOpsKey;
                project.AzureDevOpsProject = request.AzureDevOpsProject;
                project.AzureDevOpsOrganization = request.AzureDevOpsOrganization;
                project.AzureDevOpsProjectId = azureDevOpsOrganizationId;
                project.IsAutoLogWork = request.IsAutoLogWork;
            }
            else
            {
                project.Integration = (int)EIntegrationService.None;
                project.JiraKey = null;
                project.JiraDomain = null;
                project.JiraUser = null;
                project.JiraProjectId = null;
                project.AzureDevOpsKey = null;
                project.AzureDevOpsProject = null;
                project.AzureDevOpsOrganization = null;
                project.AzureDevOpsProjectId = null;
                project.IsAutoLogWork = false;
            }

            await _unitOfWork.ProjectRepository.UpdateAsync(project);
            await _unitOfWork.SaveChangesAsync();
            return SuccessResult("Cập nhật tích hợp dự án thành công");
        }

        [HttpPost("{id}/project-stage")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> CreateProjectStageAsync([FromRoute] int id, [FromBody] ProjectStageCreateRequest request)
        {
            #region Validation
            var user = GetCurrentUser();

            if (request.StartDate.HasValue && request.EndDate.HasValue && request.EndDate.Value < request.StartDate.Value)
            {
                return ErrorResult("Ngày kết thúc không hợp lệ");
            }

            if (request.WorkingHour.HasValue && request.WorkingHour.Value <= 0)
            {
                return ErrorResult("Số giờ làm việc không hợp lệ");
            }

            var project = await _unitOfWork.ProjectRepository.GetByIdAsync(id);
            if (project == null)
            {
                return ErrorResult("Dự án không tồn tại");
            }

            if (!CheckUserHasPermission(project, user.Id))
            {
                return ErrorResult("Bạn không phải là quản lý của dự án này");
            }

            var projectStage = await _unitOfWork.ProjectStageRepository.GetByProjectIdAndStageNameAsync(project.Id, request.Name);
            if (projectStage != null)
            {
                return ErrorResult("Giai đoạn này đã tồn tại");
            }

            //if (request.StartDate.HasValue && request.EndDate.HasValue)
            //{
            //    var isExist = await _unitOfWork.ProjectStageRepository.CheckExistByStartAndEndDateAsync(project.Id, request.StartDate.Value, request.EndDate.Value);
            //    if (isExist)
            //    {
            //        return ErrorResult("Thời gian bắt đầu & kết thúc bị trùng trong dự án này");
            //    }
            //}

            var projectMembers = await _unitOfWork.ProjectMemberRepository.GetListProjectMemberAsync(id);
            var userIds = request.ProjectStageMemberIds.Distinct().ToList();
            var users = projectMembers.Where(o => userIds.Contains(o.UserInternalId)).ToList();
            if (users.Count != userIds.Count)
            {
                return ErrorResult("Tồn tại thành viên không hợp lệ");
            }
            #endregion

            var newProjectStage = new ProjectStage
            {
                ProjectId = project.Id,
                Name = request.Name.Trim(),
                WorkingHour = request.WorkingHour,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                Status = (int)EProjectStageStatus.NotStarted,
                Note = request.Note,
                Critical = request.Critical,
                Highlight = request.Highlight,
                CreatedDate = DateTime.UtcNow.UTCToIct(),
                ProjectStageMembers = userIds.Select(o => new ProjectStageMember
                {
                    UserId = o,
                    CreatedDate = DateTime.UtcNow.UTCToIct()
                }).ToList()
            };

            await _unitOfWork.ProjectStageRepository.CreateAsync(newProjectStage);
            await _unitOfWork.SaveChangesAsync();

            return SuccessResult("Thêm giai đoạn thành công");
        }

        [HttpPut("{id}/project-stage/{stageId}")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> UpdateProjectStageAsync([FromRoute] int id, [FromRoute] int stageId, [FromBody] ProjectStageUpdateRequest request)
        {
            #region Validation
            var user = GetCurrentUser();

            if (request.StartDate.HasValue && request.EndDate.HasValue && request.EndDate.Value < request.StartDate.Value)
            {
                return ErrorResult("Ngày kết thúc không hợp lệ");
            }

            if (request.WorkingHour.HasValue && request.WorkingHour.Value <= 0)
            {
                return ErrorResult("Số giờ làm việc không hợp lệ");
            }

            if (!Enum.IsDefined(typeof(EProjectStageStatus), request.Status))
            {
                return ErrorResult("Trạng thái giai đoạn không hợp lệ");
            }

            var project = await _unitOfWork.ProjectRepository.GetByIdAsync(id);
            if (project == null)
            {
                return ErrorResult("Dự án không tồn tại");
            }

            if (!CheckUserHasPermission(project, user.Id))
            {
                return ErrorResult("Bạn không phải là quản lý của dự án này");
            }

            var projectStage = await _unitOfWork.ProjectStageRepository.GetByIdAsync(stageId);
            if (projectStage == null)
            {
                return ErrorResult("Giai đoạn không tồn tại");
            }

            if (projectStage.Status == (int)EProjectStageStatus.Completed)
            {
                return ErrorResult("Không thể cập nhật giai đoạn đã hoàn thành");
            }

            else if (projectStage.Status == (int)EProjectStageStatus.NotStarted && (int)request.Status != projectStage.Status && request.Status != EProjectStageStatus.InProcess)
            {
                return ErrorResult("Chuyển trạng thái không hợp lệ (Chưa Bắt Đầu)");
            }

            else if (projectStage.Status == (int)EProjectStageStatus.InProcess && (int)request.Status != projectStage.Status && request.Status != EProjectStageStatus.Completed)
            {
                return ErrorResult("Chuyển trạng thái không hợp lệ (Đang Tiến Hành)");
            }

            var projectStageNameTrim = request.Name.Trim();
            if (!projectStage.Name.Equals(projectStageNameTrim, StringComparison.OrdinalIgnoreCase))
            {
                var projectStageByName = await _unitOfWork.ProjectStageRepository.GetByProjectIdAndStageNameAsync(project.Id, projectStageNameTrim);
                if (projectStageByName != null)
                {
                    return ErrorResult($"Giai đoạn '{projectStageNameTrim}' đã tồn tại");
                }
            }

            //if (request.StartDate.HasValue && request.EndDate.HasValue)
            //{
            //    var isExist = await _unitOfWork.ProjectStageRepository.CheckExistByStartAndEndDateAsync(project.Id, request.StartDate.Value, request.EndDate.Value, projectStage.Id);
            //    if (isExist)
            //    {
            //        return ErrorResult("Thời gian bắt đầu & kết thúc bị trùng trong dự án này");
            //    }
            //}

            //if (request.Status == EProjectStageStatus.InProcess)
            //{
            //    var isExistInProcess = await _unitOfWork.ProjectStageRepository.CheckExistInProcessStageAsync(project.Id, projectStage.Id);
            //    if (isExistInProcess)
            //    {
            //        return ErrorResult("Chỉ được phép có 1 giai đoạn trong trạng thái 'Đang tiến hành'");
            //    }
            //}
            var projectMembers = await _unitOfWork.ProjectMemberRepository.GetListProjectMemberAsync(id);
            var userIds = request.ProjectStageMemberIds.Distinct().ToList();
            var users = projectMembers.Where(o => userIds.Contains(o.UserInternalId)).ToList();
            if (users.Count != userIds.Count)
            {
                return ErrorResult("Tồn tại thành viên không hợp lệ");
            }
            #endregion

            projectStage.Name = projectStageNameTrim;
            projectStage.WorkingHour = request.WorkingHour;
            projectStage.StartDate = request.StartDate;
            projectStage.EndDate = request.EndDate;
            projectStage.Status = (int)request.Status;
            projectStage.Note = request.Note;
            projectStage.Critical = request.Critical;
            projectStage.Highlight = request.Highlight;
            projectStage.UpdatedDate = DateTime.UtcNow.UTCToIct();

            #region Handle ProjectStageMember
            // Kiểm tra xem thành viên trong giai đoạn đó đã tồn tại hay chưa
            // Nếu tồn tại thì ko làm gì, chưa tồn tại thì thêm
            // Tồn tại trong db mà ko tồn tại trong request thì remove
            var projectStageMembers = projectStage.ProjectStageMembers.ToList();
            var newProjectStageMembers = new List<ProjectStageMember>();
            var removeProjectStageMembers = new List<ProjectStageMember>();
            if (request.ProjectStageMemberIds.Any())
            {
                foreach (var projectStageMember in projectStageMembers)
                {
                    if (userIds.Contains(projectStageMember.UserId))
                    {
                        userIds.Remove(projectStageMember.UserId);
                    }
                    else
                    {
                        removeProjectStageMembers.Add(projectStageMember);
                    }
                }

                newProjectStageMembers.AddRange(userIds.Select(o => new ProjectStageMember
                {
                    ProjectStageId =  projectStage.Id,
                    UserId = o,
                    CreatedDate = DateTime.UtcNow.UTCToIct()
                }));
            }
            else
            {
                removeProjectStageMembers.AddRange(projectStageMembers);
            }
            #endregion

            await _unitOfWork.ProjectStageRepository.UpdateAsync(projectStage);
            await _unitOfWork.ProjectStageMemberRepository.DeleteRangeAsync(removeProjectStageMembers);
            await _unitOfWork.ProjectStageMemberRepository.CreateRangeAsync(newProjectStageMembers);
            await _unitOfWork.SaveChangesAsync();

            return SuccessResult("Cập nhật giai đoạn thành công");
        }

        [HttpGet("{id}/project-stage")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> GetAllProjectStageAsync([FromRoute] int id)
        {
            var user = GetCurrentUser();
            var project = await _unitOfWork.ProjectRepository.GetByIdAsync(id);
            if (project == null)
            {
                return ErrorResult("Dự án không tồn tại");
            }

            if (!CheckUserHasPermission(project, user.Id))
            {
                return ErrorResult("Bạn không phải là quản lý của dự án này");
            }

            var totalProjectWorkingHour = await _unitOfWork.ProjectRepository.CalculateTotalWorkingHourByProjectIdAsync(project.Id);
            var rawModels = await _unitOfWork.ProjectStageRepository.GetAllByProjectIdAsync(project.Id);
            var projectStages = rawModels.Select(o => new ProjectStageResponseModel
            {
                Id = o.Id,
                Name = o.Name,
                WorkingHour = o.WorkingHour,
                StartDate = o.StartDate,
                EndDate = o.EndDate,
                Status = o.Status,
                Critical = o.Critical,
                Highlight = o.Highlight,
                Note = o.Note,
                NumberOfMembers = o.NumberOfMembers,
                ProjectStageMemberIds = !string.IsNullOrEmpty(o.ProjectStageMemberIds) ? o.ProjectStageMemberIds.Split(',').Select(int.Parse).ToList() : new List<int>(),
                ProjectStageMemberNames = o.ProjectStageMemberNames,
                StageWorkingHour = o.StageWorkingHour,
                StageWorkingHourRemaining = o.StageWorkingHourRemaining
            }).ToList();

            var result = new ProjectStageGridResponse
            {
                TotalWorkingHour = rawModels.Sum(o => o.WorkingHour ?? 0),
                TotalStageWorkingHour = rawModels.Sum(o => o.StageWorkingHour),
                TotalProjectWorkingHour = totalProjectWorkingHour ?? 0,
                ProjectStages = projectStages
            };
            return SuccessResult(result);
        }

        [HttpDelete("{id}/project-stage/{stageId}")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> DeleteProjectStageAsync([FromRoute] int id, [FromRoute] int stageId)
        {
            #region Validation
            var user = GetCurrentUser();
            var project = await _unitOfWork.ProjectRepository.GetByIdAsync(id);
            if (project == null)
            {
                return ErrorResult("Dự án không tồn tại");
            }

            if (!CheckUserHasPermission(project, user.Id))
            {
                return ErrorResult("Bạn không phải là quản lý của dự án này");
            }

            var projectStage = await _unitOfWork.ProjectStageRepository.GetByIdAsync(stageId);
            if (projectStage == null)
            {
                return ErrorResult("Giai đoạn không tồn tại");
            }

            if (projectStage.ProjectId != project.Id)
            {
                return ErrorResult("Giai đoạn dự án không hợp lệ");
            }

            if (projectStage.Status != (int)EProjectStageStatus.NotStarted)
            {
                return ErrorResult("Trạng thái giai đoạn không cho phép xóa");
            }

            if (projectStage.ProjectTimesheetLogTimes.Any())
            {
                return ErrorResult("Giai đoạn đã có logtime! Không thể xóa");
            }
            #endregion

            await _unitOfWork.ProjectStageMemberRepository.DeleteRangeAsync(projectStage.ProjectStageMembers.ToList());
            await _unitOfWork.ProjectStageRepository.DeleteAsync(projectStage);
            await _unitOfWork.SaveChangesAsync();
            return SuccessResult("Xóa giai đoạn thành công");
        }

        [HttpGet("{id}/project-stage/dropdown")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> GetProjectStageDropdownAsync([FromRoute] int id)
        {
            var user = GetCurrentUser();

            var project = await _unitOfWork.ProjectRepository.GetByIdAsync(id);
            if (project == null)
            {
                return ErrorResult("Dự án không tồn tại");
            }

            var projectMember = project.ProjectMembers.FirstOrDefault(o => o.UserInternalId == user.Id);
            if (projectMember == null || !projectMember.IsActive || projectMember.IsDeleted)
            {
                return ErrorResult("Bạn không phải là thành viên của dự án này");
            }

            // Sort dropdown
            // 1. Giai đoạn đang tiến hành mà user có trong danh sách thành viên của giai đoạn
            // 2. Tiếp theo là giai đoạn đang tiến hành
            // 3. Cuối cùng là theo ngày bắt đầu dự kiến
            var response = project.ProjectStages
                .OrderByDescending(z => z.Status == (int)EProjectStageStatus.InProcess && z.ProjectStageMembers.Any(q => q.UserId == user.Id))
                .ThenByDescending(z => z.Status == (int)EProjectStageStatus.InProcess)
                .ThenBy(z => z.StartDate)
                .Select(o => new ProjectStageDropdownResponse
                {
                    Id = o.Id,
                    Name = o.Name,
                    StartDate = o.StartDate,
                    EndDate = o.EndDate,
                    Status = o.Status,
                    StageMemberIds = o.ProjectStageMembers.Select(z => z.UserId).ToList()
                }).ToList();

            return SuccessResult(response);
        }
    }
}
