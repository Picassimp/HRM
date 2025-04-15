using InternalPortal.ApplicationCore.Models;
using InternalPortal.ApplicationCore.Models.ProjectManagement;

namespace InternalPortal.ApplicationCore.Interfaces.Business
{
    public interface IProjectManagementService
    {
        Task<List<ProjectReportResponse>> ManagerGetReportDataAsync(int managerId, ManagerProjectReportRequest request);
        Task<List<ChartResponse>> ManagerGetChartDataAsync(int managerId, ManagerProjectChartRequest request);
        Task<List<ProjectDetailForReportingResponse>> ManagerGetDetailForReportingAsync(int managerId, ManagerProjectDetailRequest request);
        byte[] ManagerExportDetailReporting(List<ProjectDetailForReportingResponse> excelModel);
        Task<byte[]> ImportTask(int managerId, Stream stream);
        Task<List<CommonUserProjectsResponse>> UserGetProjectFilterAsync(int userId);
        Task<List<UserProjectTimesheetResponse>> UserGetTimesheetAsync(int userId, ProjectTimesheetUserPagingRequest model);
        Task<List<UserProjectTimesheetSelfResponse>> UserGetTimesheetSelfAsync(int userId, ProjectTimesheetSelfUserPagingRequest model);
        Task<List<ManageProjectTimesheetGroupResponse>> ManagerOrOwnerGetGroupAsync(ProjectTimesheetPagingRequest model);
        byte[] ManagerProjectExportExcel(string ProjectName, List<ManageProjectTimesheetPagingResponse> excelModel);
        Task<CombineResponseModel<TimesheetResponse>> PrepareStartTaskAsync(ProjectTimesheetLogTimeRequest request, int userId);
        Task<CombineResponseModel<TimesheetResponse>> PrepareStopTaskAsync(ProjectTimesheetLogTimeRequest request);
        byte[] ManagerGetExportTimesheet(string groupBy, List<ProjectReportResponse> excelModel);
        Task<List<SupervisorMemberProjectTimesheetGroupResponse>> SupervisorGetTimesheetPagingAsync(int supervisorId, SupervisorProjectTimesheetPagingRequest request);
        Task<List<MemberOfUserResponse>> GetMemberByUserIdAsync(int userId, List<MemberOfUserResponse> relations);
        Task SyncIssueTypeAsync();
        Task SyncIssueTypeInProjectAsync();
    }
}
