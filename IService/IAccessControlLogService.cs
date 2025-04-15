using InternalPortal.ApplicationCore.Models;
using InternalPortal.ApplicationCore.Models.AccessControl;
using InternalPortal.ApplicationCore.Models.Log;

namespace InternalPortal.ApplicationCore.Interfaces.Business
{
    public interface IAccessControlLogService
    {
        Task<CombineResponseModel<ErrorModel>> CallApiToAccessControl(AccessControlApiRequest requestModel);
        Task CreateLogAsync(ApiLogModel<AccessControlLogRequest> model);
    }
}
