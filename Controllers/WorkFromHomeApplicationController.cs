using Hangfire;
using InternalPortal.ApplicationCore.Enums;
using InternalPortal.ApplicationCore.Helpers;
using InternalPortal.ApplicationCore.Interfaces;
using InternalPortal.ApplicationCore.Interfaces.Business;
using InternalPortal.ApplicationCore.Models;
using InternalPortal.ApplicationCore.Models.CriteriaModel;
using InternalPortal.ApplicationCore.Models.PagingModel;
using InternalPortal.ApplicationCore.Models.WorkFromHomeApplicationModel;
using InternalPortal.ApplicationCore.ValueObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Web.Resource;
using System;

namespace InternalPortal.API.Controllers
{
    public class WorkFromHomeApplicationController : BaseApiController
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IWorkFromHomeApplicationService _workFromHomeApplicationService;

        public WorkFromHomeApplicationController(
            IWorkFromHomeApplicationService workFromHomeApplicationService,
            IUnitOfWork unitOfWork
            )
        {
            _workFromHomeApplicationService = workFromHomeApplicationService;
            _unitOfWork = unitOfWork;
        }

        [Route("paging")]
        [HttpGet]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> PagingAsync([FromQuery] WorkFromHomeApplicationCriteriaModel model)
        {
            var user = GetCurrentUser();

            bool isManager = user.Roles.Any(x => x == AppRole.Manager);
            if (!isManager)
            {
                if (model.SearchUserId.HasValue)
                {
                    return SuccessResult(new PagingResponseModel<WorkFromHomeApplicationPagingModel>()
                    {
                        Items = [],
                        TotalRecord = 0
                    });
                }
            }

            var result = await _workFromHomeApplicationService.GetAllWithPagingAsync(model, user.Id);
            return SuccessResult(result);
        }

        [HttpPost]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> CreateAsync([FromBody] WorkFromHomeApllicationRequest model)
        {
            var user = GetCurrentUser();

            var result = await _workFromHomeApplicationService.PrepareCreateAsync(model, user);

            if (!result.Status || !string.IsNullOrEmpty(result.ErrorMessage) || result.Data == null)
            {
                return ErrorResult(result.ErrorMessage ?? "Lỗi khi tạo đơn làm việc ở nhà");
            }

            //Send Mail
            BackgroundJob.Enqueue<IWorkFromHomeApplicationService>(x => x.SendMailAsync(result.Data.Id));

            //Send Notification
            BackgroundJob.Enqueue<IWorkFromHomeApplicationService>(x => x.SendNotificationAsync(result.Data));

            return SuccessResult("Đơn làm việc ở nhà được tạo thành công");
        }

        [Route("{id}")]
        [HttpPut]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> UpdateAsync([FromRoute] int id, [FromBody] WorkFromHomeApllicationRequest model)
        {
            var user = GetCurrentUser();

            var result = await _workFromHomeApplicationService.PrepareUpdateAsync(id, model, user);

            if (!result.Status || !string.IsNullOrEmpty(result.ErrorMessage) || result.Data == null)
            {
                return ErrorResult(result.ErrorMessage ?? "Lỗi khi cập nhật đơn làm việc ở nhà");
            }

            if (result.Data.ReviewStatus == (int)EReviewStatus.Pending)
            {
                //Send Mail
                BackgroundJob.Enqueue<IWorkFromHomeApplicationService>(x => x.SendMailAsync(result.Data.Id));
            }

            //Send Notification
            BackgroundJob.Enqueue<IWorkFromHomeApplicationService>(x => x.SendNotificationAsync(result.Data));

            return SuccessResult("Cập nhật đơn làm việc ở nhà thành công");
        }

        [Route("{id}")]
        [HttpDelete]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> DeleteAsync([FromRoute] int id)
        {
            var user = GetCurrentUser();

            var application = await _unitOfWork.WorkFromHomeApplicationRepository.GetByIdAsync(id);
            if (application == null)
            {
                return ErrorResult("Đơn làm việc ở nhà không tồn tại");
            }

            if (application.ReviewStatus != (int)EReviewStatus.Pending)
            {
                return ErrorResult("Đơn làm việc ở nhà đã được duyệt");
            }

            if (user.Id != application.UserId)
            {
                return ErrorResult("Người dùng không có quyền xoá đơn làm việc ở nhà");
            }

            await _unitOfWork.WorkFromHomeApplicationRepository.DeleteAsync(application);
            await _unitOfWork.SaveChangesAsync();

            return SuccessResult("Xóa đơn làm việc ở nhà thành công");
        }

        [Route("review/{id}")]
        [HttpPut]
        [Authorize(AuthorizationPolicies.ManagerRoleRequired)]
        public async Task<IActionResult> ReviewAsync([FromRoute] int id, [FromBody] ReviewModel model)
        {
            var user = GetCurrentUser();

            bool isAllowReview = user.Roles.Any(x => x == AppRole.Manager);
            if (!isAllowReview)
            {
                return ErrorResult("Người dùng không có quyền duyệt");
            }

            var application = await _unitOfWork.WorkFromHomeApplicationRepository.GetByIdAsync(id);
            if (application == null)
            {
                return ErrorResult("Đơn làm việc ở nhà không tồn tại");
            }

            if (application.ReviewStatus != (int)EReviewStatus.Pending)
            {
                return ErrorResult("Đơn làm việc ở nhà đã được duyệt");
            }

            if (user.Id != application.ReviewUserId)
            {
                return ErrorResult("Người duyệt không hợp lệ");
            }

            if (model.ReviewStatus != EReviewStatus.Rejected && model.ReviewStatus != EReviewStatus.Reviewed)
            {
                return ErrorResult("Trạng thái duyệt không hợp lệ");
            }

            application.ReviewStatus = (int)model.ReviewStatus;
            application.UpdatedDate = DateTime.UtcNow.UTCToIct();
            application.ReviewNote = model.ReviewNote;
            application.ReviewDate = DateTime.UtcNow.UTCToIct();

            await _unitOfWork.WorkFromHomeApplicationRepository.UpdateAsync(application);
            await _unitOfWork.SaveChangesAsync();

            //Send Mail
            BackgroundJob.Enqueue<IWorkFromHomeApplicationService>(x => x.SendMailAsync(application.Id));

            var notification = new WorkFromHomeApplicationNotificationModel()
            {
                Id = application.Id,
                FromDate = application.FromDate,
                ToDate = application.ToDate,
                ReviewStatus = (EReviewStatus)application.ReviewStatus,
                Note = application.Note,
                ReviewNote = application.ReviewNote,
                ReviewUserId = application.ReviewUserId,
                UserId = application.UserId,
                UserFullName = application.User.FullName,
            };

            //Send Notification
            BackgroundJob.Enqueue<IWorkFromHomeApplicationService>(x => x.SendNotificationAsync(notification));

            return SuccessResult("Duyệt đơn làm việc ở nhà thành công");
        }

        [Route("users-manager")]
        [HttpGet]
        [Authorize(Policy = AuthorizationPolicies.ManagerRoleRequired)]
        public async Task<IActionResult> GetUsersSubmitManagerAsync()
        {
            var user = GetCurrentUser();

            var users = await _unitOfWork.UserInternalRepository.GetUsersSubmitWorkFromHomeApplicationManagerAsync(user.Id);

            return SuccessResult(users);
        }

        [Route("{id}")]
        [HttpGet]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> Get([FromRoute] int id)
        {
            var application = await _unitOfWork.WorkFromHomeApplicationRepository.GetByIdAsync(id);
            if (application == null)
            {
                return ErrorResult("Đơn làm việc ở nhà không tồn tại");
            }

            var user = GetCurrentUser();
            if (application.UserId != user.Id)
            {
                return ErrorResult("Không thể xem đơn làm việc ở nhà của người khác");
            }

            var response = new WorkFromHomeApllicationModel
            {
                Id = application.Id,
                RegisterDate = application.RegisterDate.Date,
                FromDate = application.FromDate,
                ToDate = application.ToDate,
                Note = application.Note,
                ReviewStatus = (EReviewStatus)application.ReviewStatus,
                ReviewNote = application.ReviewNote,
                ReviewUserId = application.ReviewUserId,
                ReviewUserName = !string.IsNullOrEmpty(application.ReviewUser.FullName) ? application.ReviewUser.FullName : application.ReviewUser.Name,
                PeriodType = (EPeriodType)application.PeriodType,
                RegistUser = !string.IsNullOrEmpty(application.User.FullName) ? application.User.FullName : application.User.Name,
                JobTitle = application.User.JobTitle,
                RelatedUserIds = !string.IsNullOrEmpty(application.RelatedUserIds) ? application.RelatedUserIds.Split(',').Select(int.Parse).ToList() : [],
                ReviewDate = application.ReviewDate.HasValue ? application.ReviewDate.Value.Date : default(DateTime?),
                LevelName = application.User.Level!.Name!,
            };

            return SuccessResult(response);
        }
        [Route("mobile/paging")]
        [HttpGet]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> Paging([FromQuery] WorkFromHomeApplicationSearchMobileModel model)
        {
            var user = GetCurrentUser();
            var result = await _workFromHomeApplicationService.GetAllWithPagingMobileAsync(model, user.Id);
            return SuccessResult(result);
        }
    }
}
