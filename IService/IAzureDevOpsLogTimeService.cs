using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Models;
using InternalPortal.ApplicationCore.Models.ProjectManagement;

namespace InternalPortal.ApplicationCore.Interfaces.Business
{
    public interface IAzureDevOpsLogTimeService
    {
        Task<CombineResponseModel<ProjectTimeSheet>> CreateAsync(AzureDevOpsLogTimeRequest request, string email);
        Task<CombineResponseModel<ProjectTimesheetLogTime>> UpdateAsync(string logworkId,AzureDevOpsLogTimeUpdateRequest request, string email);
        Task<CombineResponseModel<ProjectTimesheetLogTime>> DeleteAsync(string logworkId,string email);
    }
}
