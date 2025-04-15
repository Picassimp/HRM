
using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Helpers;
using InternalPortal.ApplicationCore.Interfaces;
using InternalPortal.ApplicationCore.Interfaces.Business;
using InternalPortal.ApplicationCore.Models;
using InternalPortal.ApplicationCore.Models.UserIP;

namespace InternalPortal.Infrastructure.Services.Business
{
    public class UserIPService : IUserIPService
    {
        private readonly IUnitOfWork _unitOfWork;
        public UserIPService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }
        public async Task<CombineResponseModel<List<UserIp>>> CreateAsync(UserIPCreateRequest request, int userId)
        {
            var res = new CombineResponseModel<List<UserIp>>();
            var isHasOverLength = request.UserIPs.Any(t => !string.IsNullOrEmpty(t.Note) && t.Note.Length > 500);
            if (isHasOverLength)
            {
                res.ErrorMessage = "Độ dài không vượt quá 500 kí tự";
                return res;
            }
            var group = request.UserIPs.GroupBy(t => t.IpAddress).Select(s => new { key = s.Key, count = s.Count() });
            if (group.Any(a => a.count > 1))
            {
                res.ErrorMessage = "Đã có IP trùng";
                return res;
            }
            var isExist = await _unitOfWork.UserIPRepository.CheckAnyIpAddressExist(request.UserIPs.Select(t=>t.IpAddress).ToList());
            if (isExist)
            {
                res.ErrorMessage = "Đã có địa chỉ IP tồn tại";
                return res;
            }
            var listIps = new List<UserIp>();
            foreach (var ip in request.UserIPs)
            {
                var userIp = new UserIp()
                {
                    UserId = userId,
                    Note = ip.Note,
                    IpAddress = ip.IpAddress,
                    CreateDate = DateTime.UtcNow.UTCToIct()
                };
                listIps.Add(userIp);
            }
            res.Status = true;
            res.Data = listIps;
            return res;
        }

        public async Task<CombineResponseModel<UserIp>> DeleteAsync(int userId, int id)
        {
            var res = new CombineResponseModel<UserIp>();
            var userIp = await _unitOfWork.UserIPRepository.GetByIdAsync(id);
            if (userIp == null)
            {
                res.ErrorMessage = "Không tồn tại địa chỉ IP";
                return res;
            }
            if (userIp.UserId != userId)
            {
                res.ErrorMessage = "Địa chỉ IP này không thuộc về bạn";
                return res;
            }
            res.Status = true;
            res.Data = userIp;
            return res;
        }

        public async Task<CombineResponseModel<UserIp>> UpdateAsync(int id, UserIPRequest request,int userId)
        {
            var res = new CombineResponseModel<UserIp>();
            if(!string.IsNullOrEmpty(request.Note) && request.Note.Length > 500)
            {
                res.ErrorMessage = "Độ dài không vượt quá 500 kí tự";
                return res;
            }
            var userIp = await _unitOfWork.UserIPRepository.GetByIdAsync(id);
            if (userIp == null)
            {
                res.ErrorMessage = "Không tồn tại địa chỉ IP";
                return res;
            }
            if (userIp.UserId != userId)
            {
                res.ErrorMessage = "Địa chỉ IP này thuộc về người khác";
                return res;
            }
            var userIPByIPAddress = await _unitOfWork.UserIPRepository.GetbyIPAddressAsync(request.IpAddress);
            if (userIPByIPAddress != null)
            {
                if (userIPByIPAddress.UserId == userId)
                {
                    if (userIPByIPAddress.Id != id)
                    {
                        res.ErrorMessage = "Địa chỉ IP đã tồn tại";
                        return res;
                    }
                }
                else
                {
                    res.ErrorMessage = "Địa chỉ IP này thuộc về người khác";
                    return res;
                }
            }
            userIp.IpAddress = request.IpAddress;
            userIp.Note = request.Note;
            userIp.UpdateDate = DateTime.UtcNow.UTCToIct();
            res.Status = true;
            res.Data = userIp;
            return res;
        }
    }
}
