using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Models;
using InternalPortal.ApplicationCore.Models.UserIP;

namespace InternalPortal.ApplicationCore.Interfaces.Business
{
    public interface IUserIPService
    {
        Task<CombineResponseModel<List<UserIp>>> CreateAsync(UserIPCreateRequest request,int userId);
        Task<CombineResponseModel<UserIp>> UpdateAsync(int id, UserIPRequest request,int userId);
        Task<CombineResponseModel<UserIp>> DeleteAsync(int userId,int id);
    }
}
