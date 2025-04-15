using Hangfire;
using InternalPortal.API.Filters;
using InternalPortal.ApplicationCore.Enums;
using InternalPortal.ApplicationCore.Interfaces;
using InternalPortal.ApplicationCore.Interfaces.Business;
using InternalPortal.ApplicationCore.Interfaces.Utilities.Microsoft;
using InternalPortal.ApplicationCore.Models;
using InternalPortal.ApplicationCore.Models.LeaveApplication;
using InternalPortal.ApplicationCore.Models.Microsoft;
using InternalPortal.ApplicationCore.ValueObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using static InternalPortal.ApplicationCore.ValueObjects.Global;

namespace InternalPortal.API.Controllers
{
    public class LeaveApplicationController : BaseApiController
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILeaveApplicationService _leaveApplicationService;
        private readonly IUserInternalService _userInternalService;
        private readonly IMicrosoftApiService _microsoftApiService;

        public LeaveApplicationController(
            IUnitOfWork unitOfWork, 
            ILeaveApplicationService leaveApplicationService, 
            IUserInternalService userInternalService,
            IMicrosoftApiService microsoftApiService
            )
        {
            _unitOfWork = unitOfWork;
            _leaveApplicationService = leaveApplicationService;
            _userInternalService = userInternalService;
            _microsoftApiService = microsoftApiService;
        }

        [Route("paging")]
        [HttpGet]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> PagingAsync([FromQuery] LeaveApplicationPagingRequest request)
        {
            var user = GetCurrentUser();
            bool isManager = user.Roles.Any(x => x == AppRole.Manager);

            // prevent when user role search
            if (!isManager)
            {
                if (request.SearchUserId.HasValue)
                {
                    return SuccessResult(new PagingResponseModel<LeaveApplicationPagingModel>()
                    {
                        Items = new List<LeaveApplicationPagingModel>(),
                        TotalRecord = 0
                    });
                }
            }

            var result = await _leaveApplicationService.GetAllWithPagingAsync(request, user.Id);
            return SuccessResult(result);
        }

        [HttpPost]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> CreateAsync([FromBody] LeaveApplicationRequest request)
        {
            var register = GetCurrentUser();
            var result = await _leaveApplicationService.PrepareCreateAsync(request, register);
            if (!result.Status || !string.IsNullOrEmpty(result.ErrorMessage))
            {
                return ErrorResult(result.ErrorMessage ?? "Lỗi khi tạo đơn nghỉ phép");
            }

            //Send Mail
            BackgroundJob.Enqueue<ILeaveApplicationService>(x => x.SendEmailAsync(result.Data!.Id));

            ////Send Notification
            BackgroundJob.Enqueue<ILeaveApplicationService>(x => x.SendNotificationAsync(result.Data!));
            return SuccessResult("Đơn xin nghỉ phép được tạo thành công");
        }

        [Route("{id}")]
        [HttpPut]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> UpdateAsync([FromRoute] int id, [FromBody] LeaveApplicationRequest model)
        {
            var register = GetCurrentUser();
            var result = await _leaveApplicationService.PrepareUpdateAsync(id, model, register);
            if (!result.Status  || !string.IsNullOrEmpty(result.ErrorMessage) || result.Data == null)
            {
                return ErrorResult(result.ErrorMessage ?? "Lỗi khi cập nhật đơn nghỉ phép");
            }

            if (result.Data.ReviewStatus == (int)EReviewStatus.Pending)
                BackgroundJob.Enqueue<ILeaveApplicationService>(x => x.SendEmailAsync(result.Data.Id));

            //Send Notification
            BackgroundJob.Enqueue<ILeaveApplicationService>(x => x.SendNotificationAsync(result.Data));
            return SuccessResult("Cập nhật đơn xin nghỉ phép thành công");
        }

        [Route("{id}")]
        [HttpDelete]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> DeleteAsync([FromRoute] int id)
        {
            var user = GetCurrentUser();
            var leaveApplication = await _unitOfWork.LeaveApplicationRepository.GetByIdAsync(id);
            if (leaveApplication == null)
            {
                return ErrorResult("Đơn nghỉ phép không tồn tại");
            }

            if (leaveApplication.ReviewStatus != (int)EReviewStatus.Pending)
            {
                return ErrorResult("Đơn xin nghỉ phép đã được duyệt");
            }

            if (user.Id != leaveApplication.UserId)
            {
                return ErrorResult("Người dùng không có quyền xóa đơn xin nghỉ phép");
            }

            await _unitOfWork.LeaveApplicationRepository.DeleteAsync(leaveApplication);
            await _unitOfWork.SaveChangesAsync();
            return SuccessResult("Xóa đơn xin nghỉ phép thành công");
        }

        [Route("{id}")]
        [HttpGet]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> GetAsync([FromRoute] int id)
        {
            var leaveApplication = await _unitOfWork.LeaveApplicationRepository.GetByIdAsync(id);
            if (leaveApplication == null)
            {
                return ErrorResult("Đơn nghỉ phép không tồn tại");
            }

            var response = new LeaveApplicationModel
            {
                Id = leaveApplication.Id,
                RegisterDate = leaveApplication.RegisterDate.Date,
                FromDate = leaveApplication.FromDate,
                ToDate = leaveApplication.ToDate,
                NumberDayOff = leaveApplication.NumberDayOff,
                LeaveApplicationNote = leaveApplication.LeaveApplicationNote!,
                ReviewStatus = leaveApplication.ReviewStatus,
                ReviewNote = leaveApplication.ReviewNote,
                LeaveApplicationTypeId = leaveApplication.LeaveApplicationTypeId,
                ReviewUserId = leaveApplication.ReviewUserId,
                ReviewUserName = !string.IsNullOrEmpty(leaveApplication.ReviewUser.FullName) ? leaveApplication.ReviewUser.FullName : leaveApplication.ReviewUser.Name!,
                PeriodType = leaveApplication.PeriodType,
                RegistUser = !string.IsNullOrEmpty(leaveApplication.User.FullName) ? leaveApplication.User.FullName : leaveApplication.User.Name!,
                JobTitle = leaveApplication.User.JobTitle,
                RelatedUserIds = !string.IsNullOrEmpty(leaveApplication.RelatedUserIds) ? leaveApplication.RelatedUserIds.Split(',').Select(int.Parse).ToList() : new List<int>(),
                ReviewDate = leaveApplication.ReviewDate.HasValue ? leaveApplication.ReviewDate.Value.Date : default(DateTime?),
                HandoverUserId = leaveApplication.HandoverUserId,
                IsAlertCustomer = leaveApplication.IsAlertCustomer,
            };
            return SuccessResult(response);
        }

        [Route("review/{id}")]
        [HttpPut]
        [Authorize(AuthorizationPolicies.ManagerRoleRequired)]
        public async Task<IActionResult> ReviewAsync([FromRoute] int id, [FromBody] ReviewModel request)
        {
            var reviewer = GetCurrentUser();
            var result = await _leaveApplicationService.PrepareReviewAsync(id, request, reviewer);
            if (!result.Status || !string.IsNullOrEmpty(result.ErrorMessage) || result.Data == null)
            {
                return ErrorResult(result.ErrorMessage ?? "Lỗi khi duyệt đơn");
            }

            if (request.ReviewStatus == EReviewStatus.Reviewed)
            {
                var register = (await _unitOfWork.UserInternalRepository.GetByIdAsync(result.Data.UserId))!; // Result.Data = LeaveApplication
                var leaveApplicationType = await _unitOfWork.LeaveApplicationTypeRepository.GetByIdAsync(result.Data.LeaveApplicationTypeId);
                if (leaveApplicationType != null && leaveApplicationType.IsSubTractCumulation && leaveApplicationType.Name != Constant.SICK_LEAVE_APPLICATION_TYPE)
                {
                    //Update ngày nghỉ tích lũy
                    await _userInternalService.UpdateDayOffAsync(register, result.Data);
                }

                if (leaveApplicationType != null && leaveApplicationType.Name == Constant.SICK_LEAVE_APPLICATION_TYPE)
                {
                    register.SickDayOff -= result.Data.NumberDayOff;
                    register.OffDayForSick += result.Data.NumberDayOff;
                    await _unitOfWork.UserInternalRepository.UpdateAsync(register);
                    await _unitOfWork.SaveChangesAsync();
                }

                #region create event on outlook
                var accessToken = await _microsoftApiService.GetAccessTokenAsync();
                var subject = "";
                var startTime = "";
                var endTime = "";
                switch (result.Data.PeriodType)
                {
                    case (int)EPeriodType.AllDay:
                        subject = $"{register?.FullName} nghỉ nguyên ngày";
                        startTime = $"{result.Data.FromDate:yyyy-MM-dd}T08:00:00";
                        endTime = $"{result.Data.ToDate:yyyy-MM-dd}T17:00:00";
                        break;
                    case (int)EPeriodType.FirstHalf:
                        subject = $"{register?.FullName} nghỉ buổi sáng";
                        startTime = $"{result.Data.FromDate:yyyy-MM-dd}T08:00:00";
                        endTime = $"{result.Data.ToDate:yyyy-MM-dd}T12:00:00";
                        break;
                    case (int)EPeriodType.SecondHalf:
                        subject = $"{register?.FullName} nghỉ buổi chiều";
                        startTime = $"{result.Data.FromDate:yyyy-MM-dd}T13:00:00";
                        endTime = $"{result.Data.ToDate:yyyy-MM-dd}T17:00:00";
                        break;
                }
                //create event on outlook
                var eventModel = new CreateEventModel
                {
                    subject = subject,
                    showAs = "free",
                    body = new CreateEventBodyModel
                    {
                        content = result.Data.LeaveApplicationNote!,
                        contentType = "HTML"
                    },
                    start = new CreateEventTimeModel
                    {
                        dateTime = startTime,
                        timeZone = "Asia/Bangkok"
                    },
                    end = new CreateEventTimeModel
                    {
                        dateTime = endTime,
                        timeZone = "Asia/Bangkok"
                    }
                };
                BackgroundJob.Enqueue<IMicrosoftApiService>(p => p.CreateEventAsync(reviewer.ObjectId!, accessToken, eventModel));
                #endregion create event on outlook
            }
           
            //Send Mail
            BackgroundJob.Enqueue<ILeaveApplicationService>(x => x.SendEmailAsync(result.Data.Id));

            LeaveApplicationNotificationModel leaveApplicationNotificationModel = new LeaveApplicationNotificationModel()
            {
                Id = result.Data.Id,
                FromDate = result.Data.FromDate,
                ToDate = result.Data.ToDate,
                ReviewStatus = result.Data.ReviewStatus,
                LeaveApplicationNote = result.Data.LeaveApplicationNote!,
                ReviewNote = result.Data.ReviewNote,
                ReviewUserId = result.Data.ReviewUserId,
                UserId = result.Data.UserId,
                UserFullName = result.Data.User.FullName!,
            };

            //Send Notification
            BackgroundJob.Enqueue<ILeaveApplicationService>(x => x.SendNotificationAsync(leaveApplicationNotificationModel));
            return SuccessResult("Duyệt đơn xin nghỉ phép  thành công");
        }

        /// <summary>
        /// Manager lấy thông tin của nhân viên cần duyệt
        /// Dùng để chọn filter
        /// </summary>
        /// <returns></returns>
        [Route("users-manager")]
        [HttpGet]
        [Authorize(Policy = AuthorizationPolicies.ManagerRoleRequired)]
        public async Task<IActionResult> GetByManagerAsync()
        {
            var manager = GetCurrentUser();
            var users = await _unitOfWork.UserInternalRepository.GetUsersInLeaveApplicationByManagerIdAsync(manager.Id);

            return SuccessResult(users);
        }
        [Route("mobile/paging")]
        [HttpGet]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> Paging([FromQuery] LeaveApplicationSearchMobileModel model)
        {
            var user = GetCurrentUser();
            var result = await _leaveApplicationService.GetAllWithPagingMobileAsync(model, user.Id);
            return SuccessResult(result);
        }
    }
}
