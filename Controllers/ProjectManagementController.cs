using AutoMapper;
using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Enums;
using InternalPortal.ApplicationCore.Helpers;
using InternalPortal.ApplicationCore.Interfaces;
using InternalPortal.ApplicationCore.Interfaces.Business;
using InternalPortal.ApplicationCore.Models.ProjectManagement;
using InternalPortal.ApplicationCore.Models.ProjectManagement.ProjectFavorite;
using InternalPortal.ApplicationCore.ValueObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;

namespace InternalPortal.API.Controllers
{
    public class ProjectManagementController : BaseApiController
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IProjectManagementService _projectManagementService;
        private readonly IMapper _mapper;
        private readonly IClientService _clientService;
        public ProjectManagementController(IUnitOfWork unitOfWork,
            IProjectManagementService projectManagementService,
            IMapper mapper,
            IClientService clientService)
        {
            _unitOfWork = unitOfWork;
            _projectManagementService = projectManagementService;
            _mapper = mapper;
            _clientService = clientService;
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

        [HttpGet("manager/report/filter")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> ManagerGetFilterDataAsync()
        {
            var user = GetCurrentUser();

            // Lấy company chưa bị xóa và do user/admin tạo
            var companyDataFilter = await _unitOfWork.ClientRepository.GetCompanyDataFilterAsync(user.Id);

            // Lấy danh sách dự án mà user có role là Ownwer/ProjectManager
            var projectDataFilter = await _unitOfWork.ProjectRepository.GetProjectDataFilterAsync(user.Id);

            // Lấy danh sách thành viên trong những dự án mà user có role Owner/PM
            var userDataFilter = await _unitOfWork.ProjectRepository.GetUserDataFilterAsync(user.Id);

            var projectIds = projectDataFilter.Select(p => p.Id).ToList();
            // Lấy danh sách giai đoạn từ dự án mà user có role là Ownwer/ProjectManager
            var projectStageData = await _unitOfWork.ProjectStageRepository.GetByProjectIdsAsync(projectIds);
            var projectDataStageFilter = projectStageData
                .Select(ps => new ProjectStageDataModel
                    {
                        Id = ps.Id,
                        ProjectId = ps.ProjectId,
                        ProjectStageName = ps.Name + " (" + ps.Project.Name + ")"
                    })
                .ToList();

            // Lấy danh sách IssueType từ dự án mà user có role là Ownwer/ProjectManager
            var issueTypeData = await _unitOfWork.ProjectIssueTypeRepository.GetByProjectIdsAsync(projectIds);
            var issueTypeFilter = issueTypeData
                .Select(it => new IssueTypeDataModel
                    {
                        Id = it.Id,
                        ProjectId = it.ProjectId,
                        IssueType = it.IssueType + " (" + it.Project.Name + ")"
                    })
                .ToList();

            // Lấy danh sách Tag từ dự án mà user có role là Ownwer/ProjectManager
            var tagsData = await _unitOfWork.ProjectTagRepository.GetByProjectIdsAsync(projectIds);
            var tagFilter = tagsData
                .Select(t => new TagDataModel
                    {
                        Id = t.Id,
                        ProjectId = t.ProjectId,
                        Tag = t.Tag + " (" + t.Project.Name + ")"
                    })
                .ToList();

            var response = new ManagerReportResponse
            {
                ProjectDataFilter = projectDataFilter,
                UserDataFilter = userDataFilter,
                CompanyDataFilter = companyDataFilter,
                ProjectStageFilter = projectDataStageFilter,
                ProjectIssueTypeFilter = issueTypeFilter,
                ProjectTagFilter = tagFilter
            };
            return SuccessResult(response);
        }

        [HttpGet("manager/report/filter/linking")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> ManagerGetDataForFilterLinking()
        {
            var user = GetCurrentUser();

            var result = await _unitOfWork.ProjectRepository.ManagerGetDataForFilterLinkingAsync(user.Id);
            return SuccessResult(result);
        }

        [HttpGet("manager/report/paging")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> ManagerGetReportDataAsync([FromQuery] ManagerProjectReportRequest request)
        {
            var user = GetCurrentUser();

            var result = await _projectManagementService.ManagerGetReportDataAsync(user.Id, request);
            return SuccessResult(result);
        }

        [HttpGet("manager/report/chart")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> ManagerGetProjectChartAsync([FromQuery] ManagerProjectChartRequest request)
        {
            var user = GetCurrentUser();

            var result = await _projectManagementService.ManagerGetChartDataAsync(user.Id, request);
            return SuccessResult(result);
        }
        [HttpGet("manager/detail")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> ManagerGetDetailForReportingAsync([FromQuery] ManagerProjectDetailRequest request)
        {
            var user = GetCurrentUser();

            var result = await _projectManagementService.ManagerGetDetailForReportingAsync(user.Id, request);
            return SuccessResult(result);
        }
        [HttpGet("manager/detail/export")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> ManagerExportReportDetailAsync([FromQuery] ManagerProjectDetailRequest request)
        {
            var user = GetCurrentUser();

            var dataForExport = await _projectManagementService.ManagerGetDetailForReportingAsync(user.Id, request);
            const string mediaType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            byte[] file = _projectManagementService.ManagerExportDetailReporting(dataForExport);
            return FileResult(file, "ManagerDetailReporting_" + DateTime.UtcNow.UTCToIct().Date.ToString("MM-dd-yyyy") + ".xlsx", mediaType);
        }

        /// <summary>
        /// Phân trang dự án của manager
        /// </summary>
        /// <returns></returns>
        [HttpGet("manager/project/paging")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> ManagerGetAllProjectAsync()
        {
            var user = GetCurrentUser();

            var projects = await _unitOfWork.ProjectRepository.GetProjectsAsync(user.Id);

            var clientFilterData = await _unitOfWork.ClientRepository.GetClientFilterDataAsync(user.Id);

            var projectMemberFilterData = await _unitOfWork.UserInternalRepository.GetProjectMemberFilterDataAsync();
            var response = new ProjectPagingResponse
            {
                Projects = projects,
                FilterData = new ProjectPagingFilterDataModel
                {
                    Clients = clientFilterData,
                    ProjectMembers = projectMemberFilterData
                }
            };

            return SuccessResult(response);
        }
        /// <summary>
        /// Manager thêm dự án
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        [HttpPost("manager/project")]
        [Authorize(Policy = AuthorizationPolicies.ManagerRoleRequired)]
        public async Task<IActionResult> ManagerCreateProjectAsync([FromBody] ProjectCreateRequest model)
        {
            var user = GetCurrentUser();

            if (string.IsNullOrEmpty(model.Name))
            {
                return ErrorResult("Tên dự án không được trống");
            }

            var client = await _unitOfWork.ClientRepository.GetByIdAsync(model.ClientId);
            if (client == null || !client.IsActive || client.IsDeleted)
            {
                return ErrorResult("Công ty không tồn tại");
            }

            var existingProject = await _unitOfWork.ProjectRepository.GetExistingProjectAsync(model, user.Id);
            if (existingProject.Any())
            {
                return ErrorResult($"Dự án '{model.Name}' đã tồn tại.");
            }

            // Thêm người tạo dự án là Owner
            var member = new ProjectMember
            {
                UserInternalId = user.Id,
                Role = (int)EProjectRole.Owner,
                IsActive = true,
                CreatedDate = DateTime.UtcNow.UTCToIct()
            };

            var project = new Project
            {
                Name = model.Name,
                ClientId = model.ClientId,
                CreatedByUserId = user.Id,
                IsActive = true,
                ProjectMembers = new List<ProjectMember> { member }
            };

            await _unitOfWork.ProjectMemberRepository.CreateAsync(member);
            await _unitOfWork.ProjectRepository.CreateAsync(project);
            await _unitOfWork.SaveChangesAsync();

            return SuccessResult("Tạo mới dự án thành công.");
        }
        /// <summary>
        /// Cập nhật trạng thái (Set IsActive, IsDelete)
        /// </summary>
        /// <param name="id"></param>
        /// <param name="model"></param>
        /// <returns></returns>
        [HttpPut("manager/project/{id}/status")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> ManagerUpdateProjectStatusAsync([FromRoute] int id,
            [FromBody] ProjectStatusUpdateRequest model)
        {
            var user = GetCurrentUser();

            var project = await _unitOfWork.ProjectRepository.GetByIdAsync(id);
            if (project == null)
            {
                return ErrorResult("Dự án không tồn tại.");
            }

            if (!CheckUserHasPermission(project, user.Id))
            {
                return ErrorResult("Bạn không phải là quản lý của dự án này");
            }

            var listProjectTimesheet = project.ProjectTimeSheets.ToList();
            if (model.IsDeleted)
            {
                if (listProjectTimesheet.Any())
                {
                    return ErrorResult("Dự án đã có task! Không thể xóa");
                }
            }

            project.IsActive = model.IsActive;
            project.IsDeleted = model.IsDeleted;

            await _unitOfWork.ProjectRepository.UpdateAsync(project);
            await _unitOfWork.SaveChangesAsync();

            return SuccessResult("Cập nhật dự án thành công");
        }
        /// <summary>
        /// Impor timesheet vào HRM
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        [Route("manager/import")]
        [HttpPost]
        [Authorize(Policy = AuthorizationPolicies.ManagerRoleRequired)]
        public async Task<IActionResult> ImportTask([FromForm] IFormFile file)
        {
            var user = GetCurrentUser();
            if (file.Length > 0 && (file.ContentType
                == "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
                || file.ContentType.ToString() == "application/vnd.ms-excel"))
            {
                using var stream = file.OpenReadStream();
                var result = await _projectManagementService.ImportTask(user.Id, stream);
                if (result == null)
                    return ErrorResult("invalid.file.format");
                return FileResult(result, "ImportTask_" + DateTime.UtcNow.UTCToIct().ToString("MM-dd-yyyy") + "." + "pdf", "application/pdf");
            }
            return ErrorResult("File import không hợp lệ");
        }
        /// <summary>
        /// Lấy danh sách công ty có IsActive = true
        /// </summary>
        /// <returns></returns>
        [HttpGet("manager/common/companies")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> ManagerGetActiveCompanyAsync()
        {
            var user = GetCurrentUser();
            var clientFilterData = await _unitOfWork.ClientRepository.GetClientFilterDataAsync(user.Id);
            return SuccessResult(clientFilterData.Where(t => t.IsActive).ToList());
        }
        /// <summary>
        /// Lấy danh sách user trong hệ thống
        /// </summary>
        /// <returns></returns>
        [HttpGet("common/users")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> CommonDataUsers()
        {
            var projectMemberFilterData = await _unitOfWork.UserInternalRepository.GetProjectMemberFilterDataAsync();
            return SuccessResult(projectMemberFilterData);
        }
        /// <summary>
        /// Xem thông tin cơ bản của dự án
        /// Gồm tên đối tác, tên dự án, tiền công
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet("manager/project/{id}/defaultInfo")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> ManagerGetProjectDefaultInfoAsync([FromRoute] int id)
        {
            var project = await _unitOfWork.ProjectRepository.GetByIdAsync(id);
            if (project == null)
            {
                return ErrorResult("Dự án không tồn tại");
            }

            if (project.ClientId.HasValue)
            {
                var client = await _unitOfWork.ClientRepository.GetByIdAsync(project.ClientId.Value);
                if (client != null)
                {
                    var response = new DefaultInfoResponse
                    {
                        ClientName = client.Company ?? "",
                        ProjectName = project.Name ?? "",
                    };
                    return SuccessResult(response);
                }
                else
                {
                    var response = new DefaultInfoResponse
                    {
                        ClientName = "",
                        ProjectName = project.Name ?? "",
                    };
                    return SuccessResult(response);
                }
            }
            else
            {
                var response = new DefaultInfoResponse
                {
                    ClientName = "",
                    ProjectName = project.Name ?? "",
                };
                return SuccessResult(response);
            }
        }
        /// <summary>
        /// Lấy danh sách thành viên trong dự án
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet("manager/project/{id}/members")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> ManagerGetProjectMembersAsync([FromRoute] int id)
        {
            var user = GetCurrentUser();
            var project = await _unitOfWork.ProjectRepository.GetByIdAsync(id);
            if (project == null)
            {
                return ErrorResult("Dự án không tồn tại.");
            }

            if (!CheckUserHasPermission(project, user.Id))
            {
                return ErrorResult("Bạn không phải là quản lý của dự án này");
            }

            var members = await _unitOfWork.ProjectMemberRepository.GetProjectMemberAsync(project);
            var integration = project.Integration;
            var response = new ProjectResponse
            {
                Integration = (EIntegrationService)integration,
                ProjectMembersGetResponses = members
            };
            return SuccessResult(response);
        }
        /// <summary>
        /// Cập nhật trạng thái thành viên
        /// </summary>
        /// <param name="id"></param>
        /// <param name="memberId"></param>
        /// <param name="model"></param>
        /// <returns></returns>
        [HttpPut("manager/project/{id}/member/{memberId}/status")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> ManagerUpdateProjectMemberStatusAsync([FromRoute] int id, [FromRoute] int memberId,
            [FromBody] ProjectMemberStatusUpdateRequest model)
        {
            var user = GetCurrentUser();
            var project = await _unitOfWork.ProjectRepository.GetByIdAsync(id);
            if (project == null)
            {
                return ErrorResult("Dự án không tồn tại");
            }

            var member = await _unitOfWork.ProjectMemberRepository.GetByIdAsync(memberId);
            if (member == null)
            {
                return ErrorResult("Thành viên không tồn tại");
            }

            if (!CheckUserHasPermission(project, user.Id))
            {
                return ErrorResult("Bạn không phải là quản lý của dự án này");
            }

            if (member.Role == (int)EProjectRole.Owner)
            {
                if (model.IsDeleted)
                {
                    return ErrorResult("Không được xóa quản lý của dự án");
                }
                if (!model.IsActive)
                {
                    return ErrorResult("Không được vô hiệu hóa quản lý của dự án");
                }
            }

            if (model.IsDeleted)
            {
                if (member.ProjectTimeSheets.Any())
                {
                    return ErrorResult("Thành viên đã có task! Không thể xóa");
                }
            }

            member.IsActive = model.IsActive;
            member.IsDeleted = model.IsDeleted;

            await _unitOfWork.ProjectMemberRepository.UpdateAsync(member);
            await _unitOfWork.SaveChangesAsync();

            return SuccessResult("Cập nhật thành viên thành công");
        }
        /// <summary>
        /// Thêm thành viên
        /// </summary>
        /// <param name="id"></param>
        /// <param name="model"></param>
        /// <returns></returns>
        [HttpPost("manager/project/{id}/members")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> ManagerAddProjectMemberAsync([FromRoute] int id,
            [FromBody] ProjectMemberCreateRequest model)
        {
            var user = GetCurrentUser();
            var project = await _unitOfWork.ProjectRepository.GetByIdAsync(id);
            if (project == null)
            {
                return ErrorResult("Dự án không tồn tại.");
            }

            if (!CheckUserHasPermission(project, user.Id))
            {
                return ErrorResult("Bạn không phải là quản lý của dự án này");
            }

            var allMember = await _unitOfWork.ProjectMemberRepository.GetListProjectMemberAsync(project.Id);
            var newMember = new ProjectMember();

            var projectMember = allMember.FirstOrDefault(x => x.UserInternalId == model.UserId);
            if (projectMember == null)
            {
                newMember = new ProjectMember()
                {
                    ProjectId = project.Id,
                    UserInternalId = model.UserId,
                    Role = (int)model.Role,
                    IsActive = true,
                    CreatedDate = DateTime.UtcNow.UTCToIct(),
                };

                // Khi thêm thành viên có role là ProjectManager hoặc Owner
                // Thì thêm userClient để người đó có thể thấy dc tên k/h trong thông tin dự án
                if (model.Role == EProjectRole.ProjectManager || model.Role == EProjectRole.Owner)
                {
                    var userClients = project.Client?.UserClients.ToList();
                    if (userClients != null && userClients.Any())
                    {
                        var userClient = userClients.Find(o => o.UserId == model.UserId);
                        if (userClient == null)
                        {
                            userClient = new UserClient()
                            {
                                UserId = model.UserId,
                                ClientId = project.ClientId.HasValue ? project.ClientId.Value : 0,
                                CreatedDate = DateTime.UtcNow.UTCToIct()
                            };
                            await _unitOfWork.UserClientRepository.CreateAsync(userClient);
                        }
                    }
                }

                if (model.Integration == EIntegrationService.AzureDevOps)
                {
                    newMember.DevOpsAccountEmail = model.DevOpsAccountEmail ?? "";
                    newMember.JiraAccountEmail = null;
                }

                if (model.Integration == EIntegrationService.Jira)
                {
                    newMember.DevOpsAccountEmail = null;
                    newMember.JiraAccountEmail = model.JiraAccountEmail ?? "";
                }

                if (model.Integration == EIntegrationService.None)
                {
                    newMember.DevOpsAccountEmail = null;
                    newMember.JiraAccountEmail = null;
                }
            }

            await _unitOfWork.ProjectMemberRepository.CreateAsync(newMember);
            await _unitOfWork.SaveChangesAsync();
            return SuccessResult("Thêm thành viên thành công");
        }

        /// <summary>
        /// Cập nhật thành viên
        /// </summary>
        /// <param name="id"></param>
        /// <param name="model"></param>
        /// <returns></returns>
        [HttpPut("manager/project/{id}/members/{memberId}")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> UpdateProjectMemberAsync([FromRoute] int id, [FromRoute] int memberId,
            [FromBody] ProjectMemberUpdateRequest model)
        {
            var user = GetCurrentUser();
            var project = await _unitOfWork.ProjectRepository.GetByIdAsync(id);
            if (project == null)
            {
                return ErrorResult("Dự án không tồn tại.");
            }

            if (!CheckUserHasPermission(project, user.Id))
            {
                return ErrorResult("Bạn không phải là quản lý của dự án này");
            }

            var projectMember = await _unitOfWork.ProjectMemberRepository.GetByIdAsync(memberId);
            if (projectMember == null)
            {
                return ErrorResult("Thành viên không tồn tại.");
            }

            projectMember.Role = (int)model.Role;
            // Khi cập nhật thành viên có role là ProjectManager hoặc Owner
            // Thì thêm userClient để người đó có thể thấy dc tên k/h trong thông tin dự án
            if (model.Role == EProjectRole.ProjectManager || model.Role == EProjectRole.Owner)
            {
                var userClients = project.Client?.UserClients.ToList();
                if (userClients != null && userClients.Any())
                {
                    var userClient = userClients.Find(o => o.UserId == projectMember.UserInternalId);
                    if (userClient == null)
                    {
                        userClient = new UserClient()
                        {
                            UserId = projectMember.UserInternalId,
                            ClientId = project.ClientId.HasValue ? project.ClientId.Value : 0,
                            CreatedDate = DateTime.UtcNow.UTCToIct()
                        };
                        await _unitOfWork.UserClientRepository.CreateAsync(userClient);
                    }
                }
            }

            if (model.Integration == EIntegrationService.Jira)
            {
                projectMember.JiraAccountEmail = model.JiraAccountEmail ?? "";
                projectMember.DevOpsAccountEmail = null;
            }
            if (model.Integration == EIntegrationService.AzureDevOps)
            {
                projectMember.JiraAccountEmail = null;
                projectMember.DevOpsAccountEmail = model.DevOpsAccountEmail ?? "";
            }
            if (model.Integration == EIntegrationService.None)
            {
                projectMember.JiraAccountEmail = null;
                projectMember.DevOpsAccountEmail = null;
            }
            await _unitOfWork.ProjectMemberRepository.UpdateAsync(projectMember);
            await _unitOfWork.SaveChangesAsync();
            return SuccessResult("Cập nhật thành viên thành công.");
        }

        /// <summary>
        /// Xem cài đặt trong dự án
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet("manager/project/{id}/settings")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> ManagerGetProjectSettingAsync([FromRoute] int id)
        {
            //var user = GetCurrentUser();
            //var project = await _unitOfWork.ProjectRepository.GetByIdAsync(id);
            //if (project == null)
            //{
            //    return ErrorResult("Dự án không tồn tại");
            //}

            //if (!CheckUserHasPermission(project, user.Id))
            //{
            //    return ErrorResult("Bạn không phải là quản lý của dự án này");
            //}

            //var response = new ProjectSettingsGetResponse
            //{
            //    Name = project.Name ?? "",
            //    ClientId = project.ClientId,
            //    Note = project.Note ?? "",
            //    Integration = (EIntegrationService)project.Integration,
            //    AzureDevOpsKey = project.AzureDevOpsKey ?? "",
            //    AzureDevOpsOrganization = project.AzureDevOpsOrganization ?? "",
            //    AzureDevOpsProject = project.AzureDevOpsProject ?? "",
            //    AzureDevOpsProjectId = project.AzureDevOpsProjectId ?? "",
            //    JiraKey = project.JiraKey ?? "",
            //    JiraDomain = project.JiraDomain ?? "",
            //    JiraUser = project.JiraUser ?? "",
            //    JiraProjectId = project.JiraProjectId ?? "",
            //    IsAutoLogWork = project.IsAutoLogWork,
            //};

            //return SuccessResult(response);
            return ErrorResult("Không còn sử dụng");
        }
        /// <summary>
        /// Sửa cài đặt
        /// </summary>
        /// <param name="id"></param>
        /// <param name="model"></param>
        /// <returns></returns>
        [HttpPut("manager/project/{id}/settings")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> ManagerUpdateProjectSettingAsync([FromRoute] int id, [FromBody] ProjectSettingsUpdateRequest model)
        {
            //var user = GetCurrentUser();
            //var isAllowToEnable = !string.IsNullOrEmpty(model.JiraDomain)
            //    && !string.IsNullOrEmpty(model.JiraProjectId)
            //    && !string.IsNullOrEmpty(model.JiraKey)
            //    && !string.IsNullOrEmpty(model.JiraUser)
            //    && model.Integration == EIntegrationService.Jira
            //    ? true : false;
            //if (model.IsAutoLogWork)
            //{
            //    if (!isAllowToEnable)
            //    {
            //        return ErrorResult("Vui lòng nhập đủ thông tin để cho phép thực hiện tính năng này");
            //    }
            //}
            //var project = await _unitOfWork.ProjectRepository.GetByIdAsync(id);
            //if (project == null)
            //{
            //    return ErrorResult("Dự án không tồn tại");
            //}

            //if (!CheckUserHasPermission(project, user.Id))
            //{
            //    return ErrorResult("Bạn không phải là quản lý của dự án này");
            //}

            //if (project.Name != model.Name)
            //{
            //    var projectRequest = new ProjectCreateRequest
            //    {
            //        Name = model.Name,
            //        ClientId = model.ClientId
            //    };
            //    var existingProjectList = await _unitOfWork.ProjectRepository.GetExistingProjectAsync(projectRequest, user.Id);
            //    var existingProject = existingProjectList.FirstOrDefault();
            //    if (existingProject != null)
            //    {
            //        return ErrorResult($"Dự án {project.Name} đã tồn tại");
            //    }
            //}

            //if (string.IsNullOrEmpty(model.Name) || string.IsNullOrWhiteSpace(model.Name))
            //{
            //    return ErrorResult("Tên dự án không được trống");
            //}

            //project.Name = model.Name;
            //project.Note = model.Note;
            //project.ClientId = model.ClientId;
            //project.Integration = (int)model.Integration;

            //if (model.Integration == EIntegrationService.Jira)
            //{
            //    if (string.IsNullOrEmpty(model.JiraKey))
            //    {
            //        return ErrorResult("JiraKey không được trống");
            //    }
            //    if (string.IsNullOrEmpty(model.JiraDomain))
            //    {
            //        return ErrorResult("JiraDomain không được trống");
            //    }
            //    if (string.IsNullOrEmpty(model.JiraUser))
            //    {
            //        return ErrorResult("JiraUser không được trống");
            //    }
            //    if (string.IsNullOrEmpty(model.JiraProjectId))
            //    {
            //        return ErrorResult("JiraProjectId không được trống");
            //    }
            //    project.JiraKey = model.JiraKey;
            //    project.JiraDomain = model.JiraDomain;
            //    project.JiraUser = model.JiraUser;
            //    project.JiraProjectId = model.JiraProjectId;
            //    project.AzureDevOpsKey = null;
            //    project.AzureDevOpsProject = null;
            //    project.AzureDevOpsOrganization = null;
            //    project.AzureDevOpsProjectId = null;
            //    project.IsAutoLogWork = model.IsAutoLogWork;
            //}
            //else if (model.Integration == EIntegrationService.AzureDevOps)
            //{
            //    if (string.IsNullOrEmpty(model.AzureDevOpsKey))
            //    {
            //        return ErrorResult("AzureDevOpsKey không được trống");
            //    }
            //    if (string.IsNullOrEmpty(model.AzureDevOpsProject))
            //    {
            //        return ErrorResult("AzureDevOpsProject không được trống");
            //    }
            //    if (string.IsNullOrEmpty(model.AzureDevOpsOrganization))
            //    {
            //        return ErrorResult("AzureDevOpsOrganization không được trống");
            //    }
            //    //project has the same organization will use the same AzureDevOpsProjectId
            //    var project1 = await _unitOfWork.ProjectRepository.GetByAzureDevOpsOrganizationAsync(model.AzureDevOpsOrganization);
            //    var azureDevOpsOrganizationId = string.Empty;
            //    if (project1 == null || string.IsNullOrEmpty(project1.AzureDevOpsProjectId))
            //    {
            //        azureDevOpsOrganizationId = Guid.NewGuid().ToString();
            //    }
            //    else
            //    {
            //        azureDevOpsOrganizationId = project1.AzureDevOpsProjectId;
            //    }
            //    project.JiraKey = null;
            //    project.JiraDomain = null;
            //    project.JiraUser = null;
            //    project.JiraProjectId = null;
            //    project.AzureDevOpsKey = model.AzureDevOpsKey;
            //    project.AzureDevOpsProject = model.AzureDevOpsProject;
            //    project.AzureDevOpsOrganization = model.AzureDevOpsOrganization;
            //    project.AzureDevOpsProjectId = azureDevOpsOrganizationId;
            //    project.IsAutoLogWork = model.IsAutoLogWork;
            //}
            //else
            //{
            //    project.Integration = (int)EIntegrationService.None;
            //    project.JiraKey = null;
            //    project.JiraDomain = null;
            //    project.JiraUser = null;
            //    project.JiraProjectId = null;
            //    project.AzureDevOpsKey = null;
            //    project.AzureDevOpsProject = null;
            //    project.AzureDevOpsOrganization = null;
            //    project.AzureDevOpsProjectId = null;
            //    project.IsAutoLogWork = model.IsAutoLogWork;
            //}

            //await _unitOfWork.ProjectRepository.UpdateAsync(project);
            //await _unitOfWork.SaveChangesAsync();
            //return SuccessResult("Cập nhật dự án thành công");
            return ErrorResult("Không còn sử dụng");
        }
        /// <summary>
        /// Manager lấy danh sách company họ tạo
        /// </summary>
        /// <returns></returns>
        [HttpGet("manager/company/paging")]
        [Authorize(Policy = AuthorizationPolicies.ManagerRoleRequired)]
        public async Task<IActionResult> ManagerGetAllCompanyAsync()
        {
            var user = GetCurrentUser();
            var clients = await _unitOfWork.ClientRepository.ManagerGetAllCompanyAsync(user.Id);
            return SuccessResult(_mapper.Map<List<Client>, List<ClientPagingResponse>>(clients));
        }
        /// <summary>
        /// Manager sửa thông tin công ty
        /// </summary>
        /// <param name="id"></param>
        /// <param name="model"></param>
        /// <returns></returns>
        [HttpPut("manager/company/{id}")]
        [Authorize(Policy = AuthorizationPolicies.ManagerRoleRequired)]
        public async Task<IActionResult> ManagerUpdateClientAsync([FromRoute] int id, [FromBody] ClientUpdateRequest model)
        {
            //var user = GetCurrentUser();
            //var client = await _unitOfWork.ClientRepository.GetByIdAsync(id);
            //if (client == null)
            //{
            //    return ErrorResult("Công ty không tồn tại");
            //}

            //if (client.CreatedByUserId.HasValue)
            //{
            //    if (client.CreatedByUserId != user.Id)
            //    {
            //        return ErrorResult("Bạn không phải là người thêm công ty này");
            //    }
            //}

            //if (string.IsNullOrEmpty(model.Name) || string.IsNullOrWhiteSpace(model.Name))
            //{
            //    return ErrorResult("Tên khách hàng không được trống");
            //}

            //if (string.IsNullOrEmpty(model.Company) || string.IsNullOrWhiteSpace(model.Company))
            //{
            //    return ErrorResult("Tên công ty không được trống");
            //}

            //var existClient = await _unitOfWork.ClientRepository.GetExistClientAsync(id, model.Company, user.Id);

            //if (existClient.FirstOrDefault() != null)
            //{
            //    return ErrorResult("Công ty đã tồn tại");
            //}

            //client.Name = model.Name;
            //client.Address = model.Address;
            //client.City = model.City;
            //client.Country = model.Country;
            //client.Company = model.Company;
            //client.Stage = model.Stage;
            //client.PhoneNumber = model.PhoneNumber;
            //client.Email = model.Email;
            //client.IsActive = model.IsActive;
            //client.IsDeleted = model.IsDeleted;

            //if (model.IsDeleted)
            //{
            //    var projects = await _unitOfWork.ProjectRepository.GetListProjectAsync(client.Id);
            //    if (projects.Any())
            //    {
            //        return ErrorResult("Công ty đã có dự án! Không thể xóa");
            //    }
            //}

            //await _unitOfWork.ClientRepository.UpdateAsync(client);
            //await _unitOfWork.SaveChangesAsync();

            //return SuccessResult("Cập nhật công ty thành công");
            return ErrorResult("Không còn sử dụng");
        }
        /// <summary>
        /// Manager tạo công ty mới
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        [HttpPost("manager/company")]
        [Authorize(Policy = AuthorizationPolicies.ManagerRoleRequired)]
        public async Task<IActionResult> ManagerCreateClientAsync([FromBody] ClientCreateRequest model)
        {
            //var user = GetCurrentUser();
            //var existingClient = await _unitOfWork.ClientRepository.GetExistClientAsync(null, model.Company, user.Id);
            //if (existingClient.Any())
            //{
            //    return ErrorResult("Tên công ty đã tồn tại");
            //}

            //if (string.IsNullOrEmpty(model.Name) || string.IsNullOrWhiteSpace(model.Name))
            //{
            //    return ErrorResult("Tên khách hàng không được trống");
            //}

            //if (string.IsNullOrEmpty(model.Company) || string.IsNullOrWhiteSpace(model.Company))
            //{
            //    return ErrorResult("Tên công ty không được trống");
            //}

            //var client = new Client
            //{
            //    Name = model.Name,
            //    Address = model.Address,
            //    Company = model.Company,
            //    Country = model.Country,
            //    City = model.City,
            //    Stage = model.Stage,
            //    PhoneNumber = model.PhoneNumber,
            //    Email = model.Email,
            //    CreatedByUserId = user.Id,
            //    IsActive = true,
            //    IsDeleted = false
            //};

            //await _unitOfWork.ClientRepository.CreateAsync(client);
            //await _unitOfWork.SaveChangesAsync();

            //return SuccessResult("Tạo công ty thành công");
            return ErrorResult("Không còn sử dụng");
        }
        /// <summary>
        /// Lấy dự án mà user đó dc asign
        /// </summary>
        /// <returns></returns>
        [HttpGet("common/user-projects")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> CommonDataUserProjects()
        {
            var user = GetCurrentUser();
            // old code: var response = await _projectManagementService.UserGetProjectFilterAsync(user.Id);
            var data = await _unitOfWork.ProjectFavoriteRepository.GetProjectDropdownByUserIdAsync(user.Id);

            var clients = data.DistinctBy(x => new
            {
                x.ClientId,
                x.Name
            }).Select(o => new ClientDropdownModel
            {
                Id = o.ClientId,
                Name = o.Name
            }).OrderBy(z => z.Name).ToList();

            var projects = data.DistinctBy(o => new
            {
                o.Id,
                o.ProjectName,
                o.Integration,
                o.ClientId
            }).Select(x => new ProjectDropdownModel
            {
                Id = x.Id,
                Name = x.ProjectName,
                Integration = x.Integration,
                ClientId = x.ClientId,
            }).OrderBy(z => z.Name).ToList();

            var response = new ProjectAndClientDropdownResponse
            {
                Clients = clients,
                Projects = projects
            };

            return SuccessResult(response);
        }
        /// <summary>
        /// Lấy dự án mà user đó quản lý
        /// </summary>
        /// <returns></returns>
        [HttpGet("common/manager-projects")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> CommonDataManageProjects()
        {
            var user = GetCurrentUser();
            var response = await _clientService.ManagerGetDataProjectsAsync(user.Id);
            return SuccessResult(response);
        }
        /// <summary>
        /// Phân trang timesheet cá nhân
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        [HttpGet("timesheet")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> TimesheetPaging([FromQuery] ProjectTimesheetUserPagingRequest model)
        {
            var user = GetCurrentUser();
            var project = await _unitOfWork.ProjectRepository.GetByIdAsync(model.ProjectId);
            if (project == null)
            {
                return ErrorResult("Dự án không tồn tại");
            }

            var projectMember = await _unitOfWork.ProjectMemberRepository.GetUserProjectAsync(project.Id, user.Id, false);
            if (projectMember == null)
            {
                return ErrorResult("Bạn không phải là thành viên dự án");
            }

            var response = await _projectManagementService.UserGetTimesheetAsync(user.Id, model);
            return SuccessResult(response);
        }

        /// <summary>
        /// Phân trang timesheet cá nhân quản lý dự án
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        [HttpGet("timesheet-self-projects")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> TimesheetSelfPaging([FromQuery] ProjectTimesheetSelfUserPagingRequest model)
        {
            var user = GetCurrentUser();

            var response = await _projectManagementService.UserGetTimesheetSelfAsync(user.Id, model);

            return SuccessResult(response);
        }

        /// <summary>
        /// Cập nhật task cho timesheet cá nhân quản lý dự án
        /// </summary>
        /// <param name="id"></param>
        /// <param name="model"></param>
        /// <returns></returns>
        [HttpPut("timesheet-self-projects/task/{id}")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> TimesheetSelfTaskUpdate([FromRoute] int id, [FromBody] ProjectTimesheetTaskUpdateRequest model)
        {
            var user = GetCurrentUser();

            var issueType = model.IssueType.Trim();
            if (string.IsNullOrWhiteSpace(issueType))
            {
                return ErrorResult("Loại task không được trống");
            }

            var task = await _unitOfWork.ProjectTimesheetRepository.GetByIdAsync(id);
            if (task == null)
            {
                return ErrorResult("Task không tồn tại");
            }

            if (task.ProjectMember.UserInternalId != user.Id)
            {
                return ErrorResult("Không thể cập nhật task của người khác");
            }

            var project = await _unitOfWork.ProjectRepository.GetByIdAsync(task.ProjectId);
            if (project == null)
            {
                return ErrorResult("Dự án không tồn tại");
            }

            var projectMember = await _unitOfWork.ProjectMemberRepository.GetUserProjectAsync(project.Id, user.Id, true);
            if (projectMember == null)
            {
                return ErrorResult("Bạn không phải là thành viên dự án");
            }

            var checkProjectExist = await _unitOfWork.ProjectRepository.GetByIdAsync(model.ProjectId);
            if (checkProjectExist == null)
            {
                return ErrorResult("Dự án đã chọn không tồn tại");
            }

            var projectMemberUpdate = await _unitOfWork.ProjectMemberRepository.GetUserProjectAsync(model.ProjectId, user.Id, true);
            if (projectMemberUpdate == null)
            {
                return ErrorResult("Bạn không phải là thành viên của dự án mà bạn muốn cập nhật");
            }

            task.TaskId = model.TaskId;
            task.Description = model.Description;
            task.ProjectId = model.ProjectId;
            task.ProjectMemberId = projectMemberUpdate.Id;
            task.IssueType = issueType;

            // 431: Nếu IssueType của task chưa tồn tại trong ProjectIssueType thì thêm mới
            var newProjectIssueTypes = new List<ProjectIssueType>();
            var projectIssueTypes = project.ProjectIssueTypes.ToList();
            if (!projectIssueTypes.Any(o => o.IssueType.Equals(issueType, StringComparison.OrdinalIgnoreCase)))
            {
                newProjectIssueTypes.Add(new ProjectIssueType
                {
                    IssueType = issueType,
                    ProjectId = project.Id,
                    CreatedDate = DateTime.UtcNow.UTCToIct()
                });
            }
            await _unitOfWork.ProjectIssueTypeRepository.CreateRangeAsync(newProjectIssueTypes);

            #region 437: Add Tags (optional) cho task
            var newProjectTags = new List<ProjectTag>();
            var newProjectTimesheetTags = new List<ProjectTimesheetTag>();
            var removeProjectTimesheetTags = new List<ProjectTimesheetTag>();

            var projectTags = project.ProjectTags.ToList();
            var projectTimesheetTags = task.ProjectTimesheetTags.ToList();
            if (model.Tags.Any())
            {
                foreach (var tag in model.Tags)
                {
                    if (!string.IsNullOrWhiteSpace(tag))
                    {
                        var tagTrim = tag.Trim();
                        // Thêm tag theo project
                        var projectTag = projectTags.Find(o => o.Tag.Equals(tagTrim, StringComparison.OrdinalIgnoreCase));
                        if (projectTag == null)
                        {
                            projectTag = newProjectTags.Find(o => o.Tag.Equals(tagTrim, StringComparison.OrdinalIgnoreCase));
                            if (projectTag == null)
                            {
                                newProjectTags.Add(new ProjectTag
                                {
                                    ProjectId = project.Id,
                                    Tag = tagTrim,
                                    CreatedDate = DateTime.UtcNow.UTCToIct()
                                });
                            }
                        }

                        var projectTimesheetTag = projectTimesheetTags.Find(o => o.Tag.Equals(tagTrim, StringComparison.OrdinalIgnoreCase));
                        if (projectTimesheetTag == null)
                        {
                            projectTimesheetTag = newProjectTimesheetTags.Find(o => o.Tag.Equals(tagTrim, StringComparison.OrdinalIgnoreCase));
                            if (projectTimesheetTag == null)
                            {
                                newProjectTimesheetTags.Add(new ProjectTimesheetTag
                                {
                                    ProjectTimesheetId = task.Id,
                                    Tag = tagTrim,
                                    CreatedDate = DateTime.UtcNow.UTCToIct()
                                });
                            }
                        }
                    }
                }

                removeProjectTimesheetTags.AddRange(projectTimesheetTags.Where(o => !model.Tags.Contains(o.Tag)).ToList());
            }
            else
            {
                removeProjectTimesheetTags.AddRange(projectTimesheetTags);
            }
            #endregion

            var estimateByTaskId = await _unitOfWork.ProjectTimesheetEstimateRepository.GetByTaskIdAndProjectIdAsync(model.TaskId, project.Id);
            if (estimateByTaskId == null)
            {
                await _unitOfWork.ProjectTimesheetEstimateRepository.CreateAsync(new ProjectTimesheetEstimate
                {
                    ProjectId = project.Id,
                    TaskId = model.TaskId,
                    EstimateTimeInSecond = model.EstimateTimeInSecond,
                    CreatedDate = DateTime.UtcNow.UTCToIct(),
                });
            }
            else
            {
                if (estimateByTaskId.EstimateTimeInSecond != model.EstimateTimeInSecond)
                {
                    estimateByTaskId.EstimateTimeInSecond = model.EstimateTimeInSecond;
                    await _unitOfWork.ProjectTimesheetEstimateRepository.UpdateAsync(estimateByTaskId);
                }
            }

            await _unitOfWork.ProjectTimesheetTagRepository.DeleteRangeAsync(removeProjectTimesheetTags);
            await _unitOfWork.ProjectTagRepository.CreateRangeAsync(newProjectTags);
            await _unitOfWork.ProjectTimesheetTagRepository.CreateRangeAsync(newProjectTimesheetTags);
            await _unitOfWork.ProjectTimesheetRepository.UpdateAsync(task);
            await _unitOfWork.SaveChangesAsync();

            return SuccessResult("Cập nhật task thành công");
        }

        /// <summary>
        /// Dropdown chọn dự án khi tạo hoặc sửa task ở timesheet cá nhân quản lý dự án
        /// </summary>
        /// <returns></returns>
        [HttpGet("timesheet-projects")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> GetListProject()
        {
            var user = GetCurrentUser();

            var response = await _unitOfWork.ProjectRepository.GetAllProjectsByUserIdAsync(user.Id);

            return SuccessResult(response);
        }

        /// <summary>
        /// Lấy danh sách thành viên trong dự án
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet("common/project/{id}/members")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> GetMemberOfProject([FromRoute] int id)
        {
            var result = await _unitOfWork.ProjectMemberRepository.GetMemberFiterAsync(id);
            return SuccessResult(result);
        }
        /// <summary>
        /// Phân trang timesheet nếu User đó là Owner/ProjectManager
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        [HttpGet("timesheet-manager")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> ManagerTimesheetPaging([FromQuery] ProjectTimesheetPagingRequest model)
        {
            var user = GetCurrentUser();
            var project = await _unitOfWork.ProjectRepository.GetByIdAsync(model.ProjectId);
            if (project == null)
            {
                return ErrorResult("Dự án không tồn tại");
            }

            var projectMember = await _unitOfWork.ProjectMemberRepository.GetUserProjectAsync(project.Id, user.Id, false);
            if (projectMember == null)
            {
                return ErrorResult("Bạn không phải là thành viên dự án");
            }

            if (projectMember.Role != (int)EProjectRole.ProjectManager && projectMember.Role != (int)EProjectRole.Owner)
            {
                return ErrorResult("Bạn không phải là quản lý dự án này");
            }

            var result = await _projectManagementService.ManagerOrOwnerGetGroupAsync(model);
            return SuccessResult(result);
        }
        /// <summary>
        /// Export excel
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        [HttpGet("timesheet/manager-projects/export")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> ManagerExportExcel([FromQuery] ProjectTimesheetPagingRequest model)
        {
            var user = GetCurrentUser();
            if (model.ProjectId.Equals(null) || model.ProjectId == 0)
            {
                return ErrorResult("Hãy chọn ít nhất dự án nào đó");
            }
            var project = await _unitOfWork.ProjectRepository.GetByIdAsync(model.ProjectId);
            if (project == null)
            {
                return ErrorResult("Dự án không tồn tại");
            }
            var result = await _unitOfWork.ProjectTimesheetRepository.ManagerOrOwnerGetProjectPagingAsync(model);
            const string mediaType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

            byte[] file = _projectManagementService.ManagerProjectExportExcel(project.Name, result);
            return FileResult(file, "ProjectManagement_" + DateTime.UtcNow.UTCToIct().Date.ToString("MM-dd-yyyy") + ".xlsx", mediaType);
        }
        /// <summary>
        /// Thêm task
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        [HttpPost("timesheet/task")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> TimesheetTaskCreate([FromBody] ProjectTimesheetTaskCreateRequest request)
        {
            var user = GetCurrentUser();

            var issueType = request.IssueType.Trim();
            if (string.IsNullOrWhiteSpace(issueType))
            {
                return ErrorResult("Loại task không được trống");
            }

            var project = await _unitOfWork.ProjectRepository.GetByIdAsync(request.ProjectId);
            if (project == null)
            {
                return ErrorResult("Dự án không tồn tại");
            }

            var projectMember = await _unitOfWork.ProjectMemberRepository.GetUserProjectAsync(project.Id, user.Id, true);
            if (projectMember == null)
            {
                return ErrorResult("Bạn không phải là thành viên dự án");
            }

            // Chặn logtime khác ngày hiện tại khi IsStartTask = true
            var now = DateTime.UtcNow.UTCToIct();
            if (request.IsStartTask && request.CreatedDate.Date != now.Date)
            {
                return ErrorResult("Không được start task khác ngày hiện tại");
            }

            // Tạo task
            var task = new ProjectTimeSheet
            {
                ProjectId = request.ProjectId,
                ProjectMemberId = projectMember.Id,
                TaskId = request.TaskId,
                Description = request.Description,
                CreatedDate = request.CreatedDate,
                IssueType = issueType,
                ProcessStatus = (int)EProcessStatus.NotStart
            };

            // Nếu IssueType của task chưa tồn tại trong ProjectIssueType thì thêm mới
            var newProjectIssueTypes = new List<ProjectIssueType>();
            var projectIssueTypes = project.ProjectIssueTypes.ToList();
            if (!projectIssueTypes.Any(o => o.IssueType.Equals(issueType, StringComparison.OrdinalIgnoreCase)))
            {
                newProjectIssueTypes.Add(new ProjectIssueType
                {
                    IssueType = issueType,
                    ProjectId = project.Id,
                    CreatedDate = DateTime.UtcNow.UTCToIct()
                });
            }

            // 437: Add Tags (optional) cho task
            var newProjectTags = new List<ProjectTag>();
            var newProjectTimesheetTags = new List<ProjectTimesheetTag>();
            var projectTags = project.ProjectTags.ToList();
            foreach (var tag in request.Tags)
            {
                if (!string.IsNullOrWhiteSpace(tag))
                {
                    var tagTrim = tag.Trim();
                    // Thêm tag theo project
                    var projectTag = projectTags.Find(o => o.Tag.Equals(tagTrim, StringComparison.OrdinalIgnoreCase));
                    if (projectTag == null)
                    {
                        projectTag = newProjectTags.Find(o => o.Tag.Equals(tagTrim, StringComparison.OrdinalIgnoreCase));
                        if (projectTag == null)
                        {
                            newProjectTags.Add(new ProjectTag
                            {
                                ProjectId = project.Id,
                                Tag = tagTrim,
                                CreatedDate = DateTime.UtcNow.UTCToIct()
                            });
                        }
                    }

                    // Thêm tag theo task
                    if (!newProjectTimesheetTags.Any(o => o.Tag.Equals(tagTrim, StringComparison.OrdinalIgnoreCase)))
                    {
                        newProjectTimesheetTags.Add(new ProjectTimesheetTag
                        {
                            Tag = tagTrim,
                            CreatedDate = DateTime.UtcNow.UTCToIct()
                        });
                    }
                }
            }

            var listInsertLogTime = new List<ProjectTimesheetLogTime>();

            if (request.LogTimeCreateRequest != null && request.LogTimeCreateRequest.Any())
            {
                var projectStages = project.ProjectStages.ToList();

                var sortListRequest = request.LogTimeCreateRequest.OrderBy(o => o.StartTime).ToList();
                // Chỉ cho phép logtime trong ngày
                var tomorrow = now.Date.AddDays(1);
                if (sortListRequest.Any(o => o.StopTime >= tomorrow))
                {
                    return ErrorResult("Thời gian kết thúc không được lớn hơn hiện tại");
                }

                for (int i = 0; i < sortListRequest.Count(); i++)
                {
                    var currentItem = sortListRequest[i];
                    if (currentItem != null)
                    {
                        var currentStartTime = currentItem.StartTime;
                        var currentStopTime = currentItem.StopTime;
                        if (currentStopTime < currentStartTime)
                        {
                            return ErrorResult("Thời gian làm task không hợp lệ");
                        }

                        // Chỉ cho phép logtime trong vòng 2d từ ngày tạo task
                        var limitDate = task.CreatedDate.AddDays(2);
                        if (currentStopTime > limitDate)
                        {
                            return ErrorResult("Chỉ được log trong vòng 2 ngày của task");
                        }

                        // 425: Kiểm tra logtime cho giai đoạn
                        if (currentItem.ProjectStageId.HasValue)
                        {
                            var projectStage = projectStages.Find(o => o.Id == currentItem.ProjectStageId.Value);
                            if (projectStage == null)
                            {
                                return ErrorResult($"Giai đoạn (Id: {currentItem.ProjectStageId.Value}) không tồn tại");
                            }
                        }

                        // Nếu i là phần tử cuối thì ko check
                        if (i != sortListRequest.Count() - 1)
                        {
                            var nextItem = sortListRequest[i + 1];
                            if (nextItem != null)
                            {
                                var nextStartTime = nextItem.StartTime;
                                var nextStopTime = nextItem.StopTime;
                                if (nextStopTime < nextStartTime)
                                {
                                    return ErrorResult("Thời gian làm task không hợp lệ");
                                }

                                if (nextStartTime < currentStopTime)
                                {
                                    return ErrorResult("Thời gian bị trùng lặp");
                                }
                            }
                        }

                        // tạo 2 logtime nếu qua ngày
                        var nextDate = currentStartTime.Date.AddDays(1);
                        if (currentStartTime < nextDate && nextDate < currentStopTime)
                        {
                            listInsertLogTime.Add(new ProjectTimesheetLogTime
                            {
                                StartTime = currentStartTime,
                                StopTime = nextDate,
                                IsBillable = currentItem.IsBillable,
                                IssueType = string.IsNullOrWhiteSpace(currentItem.IssueType) ? null : currentItem.IssueType.Trim(),
                                ProjectStageId = currentItem.ProjectStageId,
                                TimeSpentSeconds = (int)(nextDate - currentStartTime).TotalSeconds
                            });

                            listInsertLogTime.Add(new ProjectTimesheetLogTime
                            {
                                StartTime = nextDate,
                                StopTime = currentStopTime,
                                IsBillable = currentItem.IsBillable,
                                IssueType = string.IsNullOrWhiteSpace(currentItem.IssueType) ? null : currentItem.IssueType.Trim(),
                                ProjectStageId = currentItem.ProjectStageId,
                                TimeSpentSeconds = (int)(currentStopTime - nextDate).TotalSeconds
                            });
                        }
                        else
                        {
                            listInsertLogTime.Add(new ProjectTimesheetLogTime
                            {
                                StartTime = currentItem.StartTime,
                                StopTime = currentItem.StopTime,
                                IsBillable = currentItem.IsBillable,
                                IssueType = string.IsNullOrWhiteSpace(currentItem.IssueType) ? null : currentItem.IssueType.Trim(),
                                ProjectStageId = currentItem.ProjectStageId,
                                TimeSpentSeconds = (int)(currentItem.StopTime - currentItem.StartTime).TotalSeconds
                            });
                        }

                        // Nếu IssueType của log time không null
                        if (!string.IsNullOrWhiteSpace(currentItem.IssueType?.Trim()))
                        {
                            // Kiểm tra đã tồn tại trong db & trong collection chưa, chưa thì thêm mới
                            if (!projectIssueTypes.Any(o => o.IssueType.Equals(currentItem.IssueType.Trim(), StringComparison.OrdinalIgnoreCase)) &&
                                !newProjectIssueTypes.Any(o => o.IssueType.Equals(currentItem.IssueType.Trim(), StringComparison.OrdinalIgnoreCase)))
                            {
                                newProjectIssueTypes.Add(new ProjectIssueType
                                {
                                    IssueType = currentItem.IssueType.Trim(),
                                    ProjectId = project.Id,
                                    CreatedDate = DateTime.UtcNow.UTCToIct()
                                });
                            }
                        }
                    }
                }
            }

            if (request.IsStartTask)
            {
                var projectStageInProcess = project.ProjectStages.FirstOrDefault(o => o.Status == (int)EProjectStageStatus.InProcess);
                listInsertLogTime.Add(new ProjectTimesheetLogTime
                {
                    StartTime = DateTime.UtcNow.UTCToIct(),
                    StopTime = null,
                    IsBillable = true, // 412: default = true
                    ProjectStageId = projectStageInProcess?.Id // 429: default chọn giai đoạn đang in process
                });
                task.ProcessStatus = (int)EProcessStatus.Running;
                await _unitOfWork.ProjectTimesheetRepository.UpdateAsync(task);

                var allTasks = await _unitOfWork.ProjectTimesheetRepository.GetByProjectIdAndUserIdAsync(request.ProjectId, user.Id);
                // Tìm các task khác của 1 người trong project đó
                foreach (var item in allTasks)
                {
                    // Nếu task đó đang chạy thì dừng task và cập nhật giờ làm
                    if (item.ProcessStatus == (int)EProcessStatus.Running)
                    {
                        var projectTimesheetLogTimes = item.ProjectTimesheetLogTimes.ToList();
                        var lastItemLogtime = projectTimesheetLogTimes.Last();
                        if (lastItemLogtime != null)
                        {
                            if (lastItemLogtime.StopTime == null)
                            {
                                lastItemLogtime.StopTime = DateTime.UtcNow.UTCToIct();
                                await _unitOfWork.ProjectTimesheetLogTimeRepository.UpdateAsync(lastItemLogtime);
                            }
                        }
                        item.ProcessStatus = (int)EProcessStatus.Stop;
                        await _unitOfWork.ProjectTimesheetRepository.UpdateAsync(item);
                    }
                }
            }

            var estimateByTaskId = await _unitOfWork.ProjectTimesheetEstimateRepository.GetByTaskIdAndProjectIdAsync(request.TaskId, project.Id);
            if (estimateByTaskId == null)
            {
                await _unitOfWork.ProjectTimesheetEstimateRepository.CreateAsync(new ProjectTimesheetEstimate
                {
                    ProjectId = project.Id,
                    TaskId = request.TaskId,
                    EstimateTimeInSecond = request.EstimateTimeInSecond,
                    CreatedDate = DateTime.UtcNow.UTCToIct(),
                });
            }
            else
            {
                if (estimateByTaskId.EstimateTimeInSecond != request.EstimateTimeInSecond)
                {
                    estimateByTaskId.EstimateTimeInSecond = request.EstimateTimeInSecond;
                    await _unitOfWork.ProjectTimesheetEstimateRepository.UpdateAsync(estimateByTaskId);
                }
            }

            if (listInsertLogTime.Any())
            {
                task.ProjectTimesheetLogTimes = listInsertLogTime;
            }

            if (newProjectTimesheetTags.Any())
            {
                task.ProjectTimesheetTags = newProjectTimesheetTags;
            }

            await _unitOfWork.ProjectIssueTypeRepository.CreateRangeAsync(newProjectIssueTypes);
            await _unitOfWork.ProjectTagRepository.CreateRangeAsync(newProjectTags);
            await _unitOfWork.ProjectTimesheetRepository.CreateAsync(task);
            await _unitOfWork.SaveChangesAsync();
            return SuccessResult("Log task thành công");
        }
        /// <summary>
        /// Cập nhật task
        /// </summary>
        /// <param name="id"></param>
        /// <param name="model"></param>
        /// <returns></returns>
        [HttpPut("timesheet/task/{id}")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> TimesheetTaskUpdate([FromRoute] int id, [FromBody] ProjectTimesheetTaskUpdateRequest model)
        {
            var user = GetCurrentUser();

            var issueType = model.IssueType.Trim();
            if (string.IsNullOrWhiteSpace(issueType))
            {
                return ErrorResult("Loại task không được trống");
            }

            var task = await _unitOfWork.ProjectTimesheetRepository.GetByIdAsync(id);
            if (task == null)
            {
                return ErrorResult("Task không tồn tại");
            }

            var project = await _unitOfWork.ProjectRepository.GetByIdAsync(task.ProjectId);
            if (project == null)
            {
                return ErrorResult("Dự án không tồn tại");
            }

            var projectMember = await _unitOfWork.ProjectMemberRepository.GetUserProjectAsync(project.Id, user.Id, true);
            if (projectMember == null)
            {
                return ErrorResult("Bạn không phải là thành viên dự án");
            }

            task.TaskId = model.TaskId;
            task.Description = model.Description;
            //task.ProjectId = model.ProjectId;
            task.IssueType = issueType;

            // 431: Nếu IssueType của task chưa tồn tại trong ProjectIssueType thì thêm mới
            var newProjectIssueTypes = new List<ProjectIssueType>();
            var projectIssueTypes = project.ProjectIssueTypes.ToList();
            if (!projectIssueTypes.Any(o => o.IssueType.Equals(issueType, StringComparison.OrdinalIgnoreCase)))
            {
                newProjectIssueTypes.Add(new ProjectIssueType
                {
                    IssueType = issueType,
                    ProjectId = project.Id,
                    CreatedDate = DateTime.UtcNow.UTCToIct()
                });
            }

            var estimateByTaskId = await _unitOfWork.ProjectTimesheetEstimateRepository.GetByTaskIdAndProjectIdAsync(model.TaskId, project.Id);
            if (estimateByTaskId == null)
            {
                await _unitOfWork.ProjectTimesheetEstimateRepository.CreateAsync(new ProjectTimesheetEstimate
                {
                    ProjectId = project.Id,
                    TaskId = model.TaskId,
                    EstimateTimeInSecond = model.EstimateTimeInSecond,
                    CreatedDate = DateTime.UtcNow.UTCToIct(),
                });
            }
            else
            {
                if (estimateByTaskId.EstimateTimeInSecond != model.EstimateTimeInSecond)
                {
                    estimateByTaskId.EstimateTimeInSecond = model.EstimateTimeInSecond;
                    await _unitOfWork.ProjectTimesheetEstimateRepository.UpdateAsync(estimateByTaskId);
                }
            }

            await _unitOfWork.ProjectIssueTypeRepository.CreateRangeAsync(newProjectIssueTypes);
            await _unitOfWork.ProjectTimesheetRepository.UpdateAsync(task);
            await _unitOfWork.SaveChangesAsync();

            return SuccessResult("Cập nhật task thành công");
        }
        /// <summary>
        /// Xóa task
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpDelete("timesheet/task/{id}")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> TimesheetTaskDelete([FromRoute] int id)
        {
            var user = GetCurrentUser();
            var task = await _unitOfWork.ProjectTimesheetRepository.GetByIdAsync(id);
            if (task == null)
            {
                return ErrorResult("Task không tồn tại");
            }

            var logTimeOfTask = task.ProjectTimesheetLogTimes.ToList();
            if (logTimeOfTask.Any())
            {
                return ErrorResult("Task đã tồn tại log time! Không thể xoá");
            }

            await _unitOfWork.ProjectTimesheetTagRepository.DeleteRangeAsync(task.ProjectTimesheetTags.ToList());
            await _unitOfWork.ProjectTimesheetRepository.DeleteAsync(task);
            await _unitOfWork.SaveChangesAsync();
            return SuccessResult("Cập nhật task thành công");
        }
        /// <summary>
        /// Lấy danh sách logtime của task
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpGet("timesheet/task/{id}/log-time")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> GetLogTimeOfTaskAsync([FromRoute] int id)
        {
            var user = GetCurrentUser();
            var task = await _unitOfWork.ProjectTimesheetRepository.GetByIdAsync(id);
            if (task == null)
            {
                return ErrorResult("Task không tồn tại");
            }

            var role = task.Project.ProjectMembers.FirstOrDefault(o => o.UserInternalId == user.Id)?.Role ?? 0;

            var logtimes = task.ProjectTimesheetLogTimes.Select(o => new ProjectTimesheetLogTimeResponse
            {
                Id = o.Id,
                StartTime = o.StartTime,
                StopTime = o.StopTime,
                IsBillable = o.IsBillable,
                IssueType = o.IssueType,
                ProjectStageId = o.ProjectStageId
            }).OrderBy(o => o.StartTime).ToList();

            var response = new ProjectTimesheetLogTimeAndRoleResponse
            {
                Role = role,
                ProjectTimesheetLogs = logtimes
            };
            return SuccessResult(response);
        }
        [HttpPut("timesheet/task/{id}/log-time")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> UpdateLogTimeOfTaskAsync([FromRoute] int id, [FromBody] List<ProjectTimesheetLogTimeUpdateRequest> request)
        {
            var task = await _unitOfWork.ProjectTimesheetRepository.GetByIdAsync(id);
            if (task == null)
            {
                return ErrorResult("Task không tồn tại");
            }

            if (task.ProcessStatus == (int)EProcessStatus.Running)
            {
                return ErrorResult("Task đang chạy! Không thể sửa log time");
            }

            var project = task.Project;
            if (project == null)
            {
                return ErrorResult("Dự án không tồn tại");
            }

            // Nếu IssueType của task chưa tồn tại trong ProjectIssueType thì thêm mới
            var newProjectIssueTypes = new List<ProjectIssueType>();
            var projectIssueTypes = project.ProjectIssueTypes.ToList();

            var listInsertLogTime = new List<ProjectTimesheetLogTime>();
            var listUpdateLogTime = new List<ProjectTimesheetLogTime>();
            var logTimeOfTask = task.ProjectTimesheetLogTimes.ToList();
            if (request.Any())
            {
                // Parse string to datetime
                var format = "MM/dd/yyyy HH:mm:ss";
                var logTimeModel = request.Select(o => new ProjectTimesheetLogTimeUpdateModel
                {
                    Id = o.Id,
                    StartTime = DateTime.ParseExact(o.StartTime, format, CultureInfo.InvariantCulture),
                    StopTime = DateTime.ParseExact(o.StopTime, format, CultureInfo.InvariantCulture),
                    IsBillable = o.IsBillable,
                    IssueType = o.IssueType,
                    ProjectStageId = o.ProjectStageId
                }).ToList();
                var sortListRequest = logTimeModel.OrderBy(o => o.StartTime).ToList();

                var now = DateTime.UtcNow.UTCToIct();

                // Chỉ cho phép logtime trong ngày
                var tomorrow = now.Date.AddDays(1);
                if (sortListRequest.Any(o => o.StopTime >= tomorrow))
                {
                    return ErrorResult("Thời gian kết thúc không được lớn hơn hiện tại");
                }

                var projectStages = project.ProjectStages.ToList();

                for (int i = 0; i < sortListRequest.Count(); i++)
                {
                    var currentItem = sortListRequest[i];
                    if (currentItem != null)
                    {
                        var currentStartTime = currentItem.StartTime;
                        var currentStopTime = currentItem.StopTime;
                        if (currentStopTime < currentStartTime)
                        {
                            return ErrorResult("Thời gian làm task không hợp lệ");
                        }

                        if (currentItem.ProjectStageId.HasValue)
                        {
                            var projectStage = projectStages.Find(o => o.Id == currentItem.ProjectStageId.Value);
                            if (projectStage == null)
                            {
                                return ErrorResult($"Giai đoạn (Id: {currentItem.ProjectStageId.Value}) không tồn tại");
                            }
                        }

                        // tạo 2 logtime nếu qua ngày
                        var nextDate = currentStartTime.Date.AddDays(1);
                        if (currentStartTime < nextDate && nextDate < currentStopTime)
                        {
                            listInsertLogTime.Add(new ProjectTimesheetLogTime
                            {
                                ProjectTimesheetId = id,
                                StartTime = currentStartTime,
                                StopTime = nextDate,
                                IsBillable = currentItem.IsBillable,
                                IssueType = string.IsNullOrWhiteSpace(currentItem.IssueType) ? null : currentItem.IssueType.Trim(),
                                ProjectStageId = currentItem.ProjectStageId,
                                TimeSpentSeconds = (int)(nextDate - currentStartTime).TotalSeconds,
                            });

                            listInsertLogTime.Add(new ProjectTimesheetLogTime
                            {
                                ProjectTimesheetId = id,
                                StartTime = nextDate,
                                StopTime = currentStopTime,
                                IsBillable = currentItem.IsBillable,
                                IssueType = string.IsNullOrWhiteSpace(currentItem.IssueType) ? null : currentItem.IssueType.Trim(),
                                ProjectStageId = currentItem.ProjectStageId,
                                TimeSpentSeconds = (int)(currentStopTime - nextDate).TotalSeconds,
                            });
                        }

                        // Chỉ cho phép logtime trong vòng 2d từ ngày tạo task
                        var limitDate = task.CreatedDate.AddDays(2);
                        if (currentStopTime > limitDate)
                        {
                            return ErrorResult("Chỉ được log trong vòng 2 ngày của task");
                        }

                        // Nếu i là phần tử cuối thì ko check
                        if (i != sortListRequest.Count() - 1)
                        {
                            var nextItem = sortListRequest[i + 1];
                            if (nextItem != null)
                            {
                                var nextStartTime = nextItem.StartTime;
                                var nextStopTime = nextItem.StopTime;
                                if (nextStopTime < nextStartTime)
                                {
                                    return ErrorResult("Thời gian làm task không hợp lệ");
                                }

                                if (nextStartTime < currentStopTime)
                                {
                                    return ErrorResult("Thời gian bị trùng lặp");
                                }
                            }
                        }
                        var logTime = logTimeOfTask.FirstOrDefault(o => o.Id == currentItem.Id.GetValueOrDefault());
                        if (logTime == null)
                        {
                            listInsertLogTime.Add(new ProjectTimesheetLogTime
                            {
                                ProjectTimesheetId = id,
                                StartTime = currentItem.StartTime,
                                StopTime = currentItem.StopTime,
                                IsBillable = currentItem.IsBillable,
                                IssueType = string.IsNullOrWhiteSpace(currentItem.IssueType) ? null : currentItem.IssueType.Trim(),
                                ProjectStageId = currentItem.ProjectStageId,
                                TimeSpentSeconds = (int)(currentItem.StopTime - currentItem.StartTime).TotalSeconds
                            });
                        }
                        else
                        {
                            logTime.StartTime = currentItem.StartTime;
                            logTime.StopTime = currentItem.StopTime;
                            logTime.IsBillable = currentItem.IsBillable;
                            logTime.IssueType = string.IsNullOrWhiteSpace(currentItem.IssueType) ? null : currentItem.IssueType.Trim();
                            logTime.ProjectStageId = currentItem.ProjectStageId;
                            logTime.TimeSpentSeconds = (int)(currentItem.StopTime - currentItem.StartTime).TotalSeconds;
                            listUpdateLogTime.Add(logTime);
                        }

                        if (!string.IsNullOrWhiteSpace(currentItem.IssueType?.Trim()) && !projectIssueTypes.Any(o => o.IssueType.Equals(currentItem.IssueType.Trim(), StringComparison.OrdinalIgnoreCase)))
                        {
                            newProjectIssueTypes.Add(new ProjectIssueType
                            {
                                IssueType = currentItem.IssueType.Trim(),
                                ProjectId = project.Id,
                                CreatedDate = DateTime.UtcNow.UTCToIct()
                            });
                        }
                    }
                }
            }

            // Thêm LogTime
            var isNeedSaveChange = false;
            if (listInsertLogTime.Any())
            {
                await _unitOfWork.ProjectTimesheetLogTimeRepository.CreateRangeAsync(listInsertLogTime);
                isNeedSaveChange = true;
            }

            // Cập nhật LogTask
            if (listUpdateLogTime.Any())
            {
                await _unitOfWork.ProjectTimesheetLogTimeRepository.UpdateRangeAsync(listUpdateLogTime);
                isNeedSaveChange = true;
            }

            // Thêm IssueType
            if (newProjectIssueTypes.Any())
            {
                await _unitOfWork.ProjectIssueTypeRepository.CreateRangeAsync(newProjectIssueTypes);
                isNeedSaveChange = true;
            }

            if (isNeedSaveChange)
            {
                await _unitOfWork.SaveChangesAsync();
            }

            return SuccessResult("Ghi thời gian làm task thành công");
        }

        [HttpDelete("timesheet/task/{id}/log-time/{logTimeId}")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> DeleteLogTimeOfTaskAsync([FromRoute] int id, [FromRoute] int logTimeId)
        {

            var task = await _unitOfWork.ProjectTimesheetRepository.GetByIdAsync(id);
            if (task == null)
            {
                return ErrorResult("Task không tồn tại");
            }

            if (task.ProcessStatus == (int)EProcessStatus.Running)
            {
                return ErrorResult("Task đang chạy! Không thể xóa log time");
            }

            var logTime = await _unitOfWork.ProjectTimesheetLogTimeRepository.GetByIdAsync(logTimeId);
            if (logTime == null)
            {
                return ErrorResult("Thời gian log task này không tồn tại");
            }

            await _unitOfWork.ProjectTimesheetLogTimeRepository.DeleteAsync(logTime);
            await _unitOfWork.SaveChangesAsync();
            return SuccessResult("Xóa thời gian làm task thành công");
        }
        [HttpPut("timesheet/task/start")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> StartLogTimeForTaskAsync([FromBody] ProjectTimesheetLogTimeRequest request)
        {
            var user = GetCurrentUser();
            var res = await _projectManagementService.PrepareStartTaskAsync(request, user.Id);
            if (!res.Status || !string.IsNullOrEmpty(res.ErrorMessage) || res.Data == null)
            {
                return ErrorResult(res.ErrorMessage ?? "Lỗi khi bắt đầu task");
            }
            await _unitOfWork.ProjectTimesheetLogTimeRepository.CreateAsync(res.Data.ProjectTimesheetLogTime);
            await _unitOfWork.ProjectTimesheetRepository.UpdateAsync(res.Data.ProjectTimeSheet);
            await _unitOfWork.SaveChangesAsync();
            return SuccessResult("Bắt đầu task thành công");
        }
        [HttpPut("timesheet/task/stop")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> StopLogTimeForTaskAsync([FromBody] ProjectTimesheetLogTimeRequest request)
        {
            var res = await _projectManagementService.PrepareStopTaskAsync(request);
            if (!res.Status || !string.IsNullOrEmpty(res.ErrorMessage) || res.Data == null)
            {
                return ErrorResult(res.ErrorMessage ?? "Lỗi khi dừng task");
            }
            if (res.Data.ProjectTimesheetLogTime != null)
            {
                await _unitOfWork.ProjectTimesheetLogTimeRepository.UpdateAsync(res.Data.ProjectTimesheetLogTime);
            }
            await _unitOfWork.ProjectTimesheetRepository.UpdateAsync(res.Data.ProjectTimeSheet);
            await _unitOfWork.SaveChangesAsync();
            return SuccessResult("Dừng task thành công");
        }
        /// <summary>
        /// Kiểm tra xem user có quản lý dự án nào ko
        /// </summary>
        /// <returns></returns>
        [HttpGet("common/user-projects/checkManage")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> CheckProjectUserManage()
        {
            var user = GetCurrentUser();
            var response = await _clientService.ManagerGetDataProjectsAsync(user.Id);
            bool check = false;
            if (response.Any())
            {
                check = true;
            }
            return SuccessResult(check);
        }
        [HttpGet("manager/report/export")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> ManagerExportDataReportAsync([FromQuery] ManagerProjectReportRequest request)
        {
            var user = GetCurrentUser();
            var dataForExport = await _projectManagementService.ManagerGetReportDataAsync(user.Id, request);
            const string mediaType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            byte[] file = _projectManagementService.ManagerGetExportTimesheet(request.GroupBy, dataForExport);
            return FileResult(file, "ManagerReportTimesheet_" + DateTime.UtcNow.UTCToIct().Date.ToString("MM-dd-yyyy") + ".xlsx", mediaType);
        }

        /// <summary>
        /// Kiểm tra xem user có quản lý nhân viên khác không
        /// </summary>
        /// <returns></returns>
        [HttpGet("supervisor/has-member")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> CheckUserManageOther()
        {
            var user = GetCurrentUser();
            var relationDtoResponses = await _unitOfWork.UserRelationRepository.GetAllRelationDtoModelAsync();
            var response = await _projectManagementService.GetMemberByUserIdAsync(user.Id, relationDtoResponses);
            bool check = false;
            if (response.Any())
            {
                check = true;
            }
            return SuccessResult(check);
        }

        /// <summary>
        /// Lấy danh sách nhân viên quản lý của người này
        /// </summary>
        /// <returns></returns>
        [HttpGet("supervisor/members")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> GetMemberOfCurrentUser()
        {
            var user = GetCurrentUser();
            var relationDtoResponses = await _unitOfWork.UserRelationRepository.GetAllRelationDtoModelAsync();
            var response = await _projectManagementService.GetMemberByUserIdAsync(user.Id, relationDtoResponses);
            return SuccessResult(response);
        }

        /// <summary>
        /// Lấy danh sách dự án của nhân viên
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet("supervisor/member/{id}/projects")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> GetProjectsOfUser([FromRoute] int id)
        {
            // Tìm danh sách cấp dưới của người hiện tại
            var supervisor = GetCurrentUser();
            var relationDtoResponses = await _unitOfWork.UserRelationRepository.GetAllRelationDtoModelAsync();
            var memberOfUserResponses = await _projectManagementService.GetMemberByUserIdAsync(supervisor.Id, relationDtoResponses);

            // Kiểm tra xem nhân viên này có thuộc quản lý không ?
            var memberIds = memberOfUserResponses.Select(o => o.MemberUserId).ToList();
            if (!memberIds.Contains(id))
            {
                return ErrorResult("Bạn không phải là quản lý của nhân viên này");
            }

            var result = await _unitOfWork.ProjectMemberRepository.GetProjectsByUserIdAsync(id);
            return SuccessResult(result);
        }

        /// <summary>
        /// Phân trang timesheet nhân viên mà user quản lý
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        [HttpGet("supervisor/timesheet")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> SupervisorTimesheetPaging([FromQuery] SupervisorProjectTimesheetPagingRequest request)
        {
            var supervisor = GetCurrentUser();
            var result = await _projectManagementService.SupervisorGetTimesheetPagingAsync(supervisor.Id, request);
            return SuccessResult(result);
        }

        [HttpGet("project/favorite")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> UserGetProjectFavoriteAsync()
        {
            var user = GetCurrentUser();
            var response = await _unitOfWork.ProjectFavoriteRepository.GetListByUserIdAsync(user.Id);
            return SuccessResult(response);
        }

        [HttpPost("project/favorite")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> UserVoteFavoriteProjectAsync([FromBody] ProjectFavoriteRequest request)
        {
            var user = GetCurrentUser();

            var project = await _unitOfWork.ProjectRepository.GetByIdAsync(request.ProjectId);
            if (project == null || !project.IsActive || project.IsDeleted)
            {
                return ErrorResult("Dự án không tồn tại");
            }

            var projectMember = project.ProjectMembers.FirstOrDefault(o => o.UserInternalId == user.Id);
            if (projectMember == null)
            {
                return ErrorResult("Bạn không phải là thành viên của dự án này");
            }

            // Create Or Delete
            var projectFavorite = project.ProjectFavorites.FirstOrDefault(o => o.ProjectId == project.Id && o.UserId == user.Id);
            if (projectFavorite == null)
            {
                await _unitOfWork.ProjectFavoriteRepository.CreateAsync(new ProjectFavorite
                {
                    ProjectId = project.Id,
                    UserId = user.Id,
                    CreatedDate = DateTime.UtcNow.UTCToIct()
                });
                await _unitOfWork.SaveChangesAsync();
                return SuccessResult("Thêm dự án vào danh sách ưu tiên thành công");
            }
            else
            {
                await _unitOfWork.ProjectFavoriteRepository.DeleteAsync(projectFavorite);
                await _unitOfWork.SaveChangesAsync();
                return SuccessResult("Xóa dự án khỏi danh sách ưu tiên thành công");
            }
        }

        [HttpGet("timesheet/issue-type/{projectId}/dropdown")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> UserGetIssueTypeDropdown([FromRoute] int projectId)
        {
            var projectIssueTypes = await _unitOfWork.ProjectIssueTypeRepository.GetListByProjectIdAsync(projectId);

            return SuccessResult(projectIssueTypes.Select(o => o.IssueType).OrderBy(o => o).ToList());
        }

        [HttpGet("timesheet/tag/{projectId}/dropdown")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> UserGetTagDropdown([FromRoute] int projectId)
        {
            var projectTags = await _unitOfWork.ProjectTagRepository.GetListByProjectIdAsync(projectId);

            return SuccessResult(projectTags.Select(o => o.Tag).OrderBy(o => o).ToList());
        }
    }
}
