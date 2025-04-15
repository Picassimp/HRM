using InternalPortal.ApplicationCore.Interfaces;
using InternalPortal.ApplicationCore.Interfaces.Business;
using InternalPortal.ApplicationCore.Models.UserIP;
using InternalPortal.ApplicationCore.ValueObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InternalPortal.API.Controllers
{
    public class UserIPController : BaseApiController
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IUserIPService _userIPService;
        public UserIPController(IUnitOfWork unitOfWork,
            IUserIPService userIPService)
        {
            _unitOfWork = unitOfWork;
            _userIPService = userIPService;
        }
        [HttpGet]
        [Route("myips")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> GetMyIPsAsync()
        {
            var user = GetCurrentUser();
            var myIp = await _unitOfWork.UserIPRepository.GetByUserIdAsync(user.Id);
            var response = myIp.Count > 0 ? myIp.Select(t => new UserIPResponse()
            {
                Id = t.Id,
                IpAddress = t.IpAddress.Trim(),
                Note = t.Note ?? string.Empty,
            }).ToList() : new List<UserIPResponse>();
            return SuccessResult(response);
        }
        [HttpPost]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> CreateAsync([FromBody] UserIPCreateRequest request)
        {
            var user = GetCurrentUser();
            var res = await _userIPService.CreateAsync(request,user.Id);
            if(!res.Status || !string.IsNullOrEmpty(res.ErrorMessage) || res.Data == null)
            {
                return ErrorResult(res.ErrorMessage ?? "Lỗi khi tạo địa chỉ IP");
            }
            await _unitOfWork.UserIPRepository.CreateRangeAsync(res.Data);
            await _unitOfWork.SaveChangesAsync();
            return SuccessResult("Tạo địa chỉ IP thành công");
        }
        [HttpPut]
        [Route("{id}")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> UpdateAsync([FromRoute] int id, [FromBody] UserIPRequest request)
        {
            var user = GetCurrentUser();
            var res = await _userIPService.UpdateAsync(id,request,user.Id);
            if (!res.Status || !string.IsNullOrEmpty(res.ErrorMessage) || res.Data == null)
            {
                return ErrorResult(res.ErrorMessage ?? "Lỗi khi cập nhật địa chỉ IP");
            }
            await _unitOfWork.UserIPRepository.UpdateAsync(res.Data);
            await _unitOfWork.SaveChangesAsync();
            return SuccessResult("Cập nhật địa chỉ IP thành công");
        }
        [HttpDelete]
        [Route("{id}")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> DeleteAsync([FromRoute] int id)
        {
            var user = GetCurrentUser();
            var res = await _userIPService.DeleteAsync(user.Id,id);
            if (!res.Status || !string.IsNullOrEmpty(res.ErrorMessage) || res.Data == null)
            {
                return ErrorResult(res.ErrorMessage ?? "Lỗi xóa địa chỉ IP");
            }
            await _unitOfWork.UserIPRepository.DeleteAsync(res.Data);
            await _unitOfWork.SaveChangesAsync();
            return SuccessResult("Xóa địa chỉ IP thành công");
        }
    }
}
