using InternalPortal.ApplicationCore.Interfaces;
using InternalPortal.ApplicationCore.Interfaces.Business;
using InternalPortal.ApplicationCore.Models.Device;
using InternalPortal.ApplicationCore.Models.User;
using InternalPortal.ApplicationCore.Models.UserInternal;
using InternalPortal.ApplicationCore.ValueObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InternalPortal.API.Controllers
{
    public class UserController : BaseApiController
    {
        private readonly IUserInternalService _userInternalService;
        private readonly IDeviceService _deviceService;
        public UserController(
            IUserInternalService userInternalService,
            IDeviceService deviceService
            )
        {
            _userInternalService = userInternalService;
            _deviceService = deviceService;
        }

        /// <summary>
        /// Nhân viên xem thông tin của bản thân (ObjectId)
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("me")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> UserGetInformationAsync()
        {
            var user = GetCurrentUser();
            var userDetail = await _userInternalService.GetDetailByObjectIdAsync(user.ObjectId!);
            if (userDetail == null)
            {
                return ErrorResult("Người dùng không tồn tại");
            }

            return SuccessResult(userDetail);
        }

        [HttpGet]
        [Route("profile")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> GetMyProfile()
        {
            // Validate user
            var user = GetCurrentUser();
            var userProfile = await _userInternalService.GetProfileAsync(user.Id);
            return SuccessResult(userProfile);
        }

        /// <summary>
        /// Nhân viên cập nhật thông tin của mình
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        [HttpPut]
        [Route("profile")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> UpdateMyProfile([FromBody] UserInternalNormalRequest model)
        {
            // Validate user
            var user = GetCurrentUser();
            var userUpdate = await _userInternalService.NormalUpdateAsync(user.Id, model);
            if (!userUpdate.Status || !string.IsNullOrEmpty(userUpdate.ErrorMessage))
            {
                return ErrorResult(userUpdate.ErrorMessage ?? "Lỗi khi cập nhật thông tin");
            }

            return SuccessResult(new { IsFirstLogin = userUpdate.Data }, "Update user successfully!");
        }

        /// <summary>
        /// Lấy danh sách manager
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("manager-users")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> GetAllManagerUsers()
        {
            var users = await _userInternalService.GetAllManagerUsersAsync();

            var result = users.Select(x => new UserInternalDropDown
            {
                Id = x.Id,
                FullName = !string.IsNullOrEmpty(x.FullName) ? x.FullName : x.Name
            });
            return SuccessResult(result);
        }

        /// <summary>
        /// GetDropdown
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> GetAllUsers()
        {
            var users = await _userInternalService.GetUsersAsync();

            return SuccessResult(users);
        }

        /// <summary>
        /// Nhân viên xem danh sách các loại ngày nghỉ của mình
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("dayoff-data")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> GetDayOffAsync()
        {
            var user = GetCurrentUser();
            var result = await _userInternalService.GetDayOffByUserIdAsync(user.Id);
            if (!result.Status || !string.IsNullOrEmpty(result.ErrorMessage) || result.Data == null)
            {
                return ErrorResult(result.ErrorMessage ?? "Lỗi khi lấy ngảy phép");
            }

            return SuccessResult(result.Data);
        }
        /// <summary>
        /// Tạo mới thiết bị
        /// </summary>
        /// <param name="deviceRequest"></param>
        /// <returns></returns>
        [HttpPost]
        [Route("save-device")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> SaveDevice([FromBody] DeviceRequest deviceRequest)
        {
            var user = GetCurrentUser();
            await _deviceService.SaveDeviceAsync(user.Id, deviceRequest.DeviceId, deviceRequest.RegistrationToken);
            return SuccessResult("Đã lưu");
        }

        [HttpGet]
        [Route("paging-dayoff")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> GetUsersDayOffWithPagingAsync([FromQuery] UserDayOffCriteriaModel request)
        {
            var user = GetCurrentUser();
            var result = await _userInternalService.GetDayOffWithPagingAsync(user.Id, request);
            return SuccessResult(result);
        }
    }
}
