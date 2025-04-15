using InternalPortal.ApplicationCore.Models;
using InternalPortal.ApplicationCore.Models.ChromeExtension;
using InternalPortal.ApplicationCore.Models.User;

namespace InternalPortal.ApplicationCore.Interfaces.Business
{
    public interface IChromeExtensionService
    {
        Task<ChromeExtensionResponse> GetTimesheetByUserIdAndDateAsync(UserDtoModel user);
        Task<CombineResponseModel<ChromeExtensionStartTaskRequest>> PrepareStartTaskAsync(int userId, ChromeExtensionStartTaskRequest request);
        Task<CombineResponseModel<ChromeExtensionStopTaskRequest>> PrepareStopTaskAsync(int userId, ChromeExtensionStopTaskRequest request);
    }
}
