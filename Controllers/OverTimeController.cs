using Hangfire;
using InternalPortal.ApplicationCore.Enums;
using InternalPortal.ApplicationCore.Helpers;
using InternalPortal.ApplicationCore.Interfaces;
using InternalPortal.ApplicationCore.Interfaces.Business;
using InternalPortal.ApplicationCore.Models.CriteriaModel;
using InternalPortal.ApplicationCore.Models.Holiday;
using InternalPortal.ApplicationCore.Models.OverTimeApplicationModel;
using InternalPortal.ApplicationCore.ValueObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InternalPortal.API.Controllers
{
    public class OverTimeController : BaseApiController
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IOverTimeApplicationService _overTimeApplicationService;

        public OverTimeController(
            IUnitOfWork unitOfWork,
            IOverTimeApplicationService overTimeApplicationService
            ) 
        {
            _unitOfWork = unitOfWork;
            _overTimeApplicationService = overTimeApplicationService;
        }

        [HttpGet]
        [Route("paging")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> GetOverTimeApplicationsAsync([FromQuery] OverTimeCriteriaModel model)
        {
            var user = GetCurrentUser();

            // Get Overtime application by Status
            var result = await _overTimeApplicationService.GetAllWithPagingAsync(model, user.Id);

            return SuccessResult(result);
        }

        [HttpPost]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> CreateApplicationAsync(OverTimeApplicationRequest request)
        {
            var user = GetCurrentUser();

            var result = await _overTimeApplicationService.PrepareCreateAsync(request, user);

            if (!result.Status || !string.IsNullOrEmpty(result.ErrorMessage) || result.Data == null)
            {
                return ErrorResult(result.ErrorMessage ?? "Lỗi khi tạo đơn xin làm thêm giờ");
            }

            //Send Mail
            BackgroundJob.Enqueue<IOverTimeApplicationService>(x => x.SendMailAsync(result.Data.Id));

            //Send Notification
            BackgroundJob.Enqueue<IOverTimeApplicationService>(x => x.SendNotificationAsync(result.Data));

            return SuccessResult("Đơn xin làm thêm giờ được tạo thành công");
        }

        [HttpPut("{id}")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> UpdateApplicationAsync(int id, OverTimeApplicationRequest request)
        {
            var user = GetCurrentUser();

            var result = await _overTimeApplicationService.PrepareUpdateAsync(id, request, user.Id);

            if (!result.Status || !string.IsNullOrEmpty(result.ErrorMessage) || result.Data == null)
            {
                return ErrorResult(result.ErrorMessage ?? "Lỗi khi cập nhật đơn xin làm thêm giờ");
            }

            //Send Mail
            BackgroundJob.Enqueue<IOverTimeApplicationService>(x => x.SendMailAsync(result.Data.Id));

            //Send Notification
            BackgroundJob.Enqueue<IOverTimeApplicationService>(x => x.SendNotificationAsync(result.Data));

            return SuccessResult("Đơn xin làm thêm giờ được cập nhật thành công");
        }

        [HttpDelete("{id}")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> DeleteApplicationAsync(int id)
        {
            var user = GetCurrentUser();

            var application = await _unitOfWork.OverTimeApplicationRepository.GetByIdAsync(id);

            if (application == null)
            {
                return ErrorResult("Đơn xin làm thêm giờ không tồn tại");
            }

            if (application.Status != (int)EReviewStatus.Pending)
            {
                return ErrorResult("Đơn xin làm thêm giờ đã được duyệt nên không thể xóa.");
            }

            if (application.UserId != user.Id)
            {
                return ErrorResult("Không thể xóa đơn làm thêm giờ của người khác");
            }

            await _unitOfWork.OverTimeApplicationRepository.DeleteAsync(application);
            await _unitOfWork.SaveChangesAsync();

            return SuccessResult("Xóa đơn xin làm thêm giờ thành công");
        }

        [HttpPut("review/{id}")]
        [Authorize(Policy = AuthorizationPolicies.ManagerRoleRequired)]
        public async Task<IActionResult> ReviewAsync([FromRoute] int id, [FromBody] ReviewModel model)
        {
            // Find Application by Id
            var application = await _unitOfWork.OverTimeApplicationRepository.GetByIdAsync(id);

            if (application == null)
            {
                return ErrorResult("Đơn xin làm thêm giờ không tồn tại");
            }

            var user = GetCurrentUser();

            var holidays = _unitOfWork.HolidayRepository.GetAll();
            var holidayHelpers = holidays.Select(h => new HolidayHelper
            {
                Date = h.HolidayDate,
                IsHolidayByYear = h.IsHolidayByYear,
            }).ToList();

            if (!application.CreatedDate.IsValidToDateForOT(holidayHelpers))
            {
                return ErrorResult("Đã quá hạn duyệt đơn!");
            }

            if (application.ReviewUserId != user.Id)
            {
                return ErrorResult("Người duyệt không hợp lệ");
            }

            if (application.Status != (int)EReviewStatus.Pending)
            {
                return ErrorResult("Đơn xin làm thêm giờ đã được duyệt");
            }

            if (model.ReviewStatus != EReviewStatus.Rejected && model.ReviewStatus != EReviewStatus.Reviewed)
            {
                return ErrorResult("Trạng thái duyệt không hợp lệ");
            }

            // Update Status
            application.Status = (int)model.ReviewStatus;
            application.UpdatedDate = DateTime.UtcNow.UTCToIct();
            application.ReviewNote = model.ReviewNote;
            application.ReviewDate = DateTime.UtcNow.UTCToIct();

            await _unitOfWork.OverTimeApplicationRepository.UpdateAsync(application);
            await _unitOfWork.SaveChangesAsync();

            //Send Mail
            BackgroundJob.Enqueue<IOverTimeApplicationService>(x => x.SendMailAsync(application.Id));

            var overtimeApplicationNotificationModel = new OvertimeApplicationNotificationModel()
            {
                Id = application.Id,
                FromDate = application.FromDate,
                ToDate = application.ToDate,
                Status = (EReviewStatus)application.Status,
                OverTimeNote = application.OverTimeNote!,
                ReviewNote = application.ReviewNote,
                ReviewUserId = application.ReviewUserId,
                UserId = application.UserId,
                UserFullName = application.User.FullName,
            };

            //Send Notification
            BackgroundJob.Enqueue<IOverTimeApplicationService>(x => x.SendNotificationAsync(overtimeApplicationNotificationModel));

            return SuccessResult("Duyệt đơn xin làm thêm giờ thành công");
        }

        [Route("users-manager")]
        [HttpGet]
        [Authorize(Policy = AuthorizationPolicies.ManagerRoleRequired)]
        public async Task<IActionResult> GetUsersSubmitManagerAsync()
        {
            var user = GetCurrentUser();

            var users = await _unitOfWork.UserInternalRepository.GetUsersSubmitOverTimeApplicationManagerAsync(user.Id);

            return SuccessResult(users);
        }
        [HttpGet]
        [Route("mobile/paging")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> GetOverTimeApplications([FromQuery] OverTimeCriteriaModel model)
        {
            var user = GetCurrentUser();    
            var result = await _overTimeApplicationService.GetAllWithPagingForMobileAsync(model, user.Id);
            return SuccessResult(result);
        }
    }
}
