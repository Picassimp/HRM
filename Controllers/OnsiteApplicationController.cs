using Hangfire;
using InternalPortal.ApplicationCore.Enums;
using InternalPortal.ApplicationCore.Helpers;
using InternalPortal.ApplicationCore.Interfaces;
using InternalPortal.ApplicationCore.Interfaces.Business;
using InternalPortal.ApplicationCore.Models;
using InternalPortal.ApplicationCore.Models.CriteriaModel;
using InternalPortal.ApplicationCore.Models.OnsiteApplicationModel;
using InternalPortal.ApplicationCore.Models.PagingModel;
using InternalPortal.ApplicationCore.ValueObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InternalPortal.API.Controllers
{
    public class OnsiteApplicationController : BaseApiController
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IOnsiteApplicationService _onsiteApplicationService;

        public OnsiteApplicationController(
            IOnsiteApplicationService onsiteApplicationService,
            IUnitOfWork unitOfWork
            )
        {
            _onsiteApplicationService = onsiteApplicationService;
            _unitOfWork = unitOfWork;
        }

        [Route("paging")]
        [HttpGet]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> PagingAsync([FromQuery] OnsiteApplicationCriteriaModel model)
        {
            var user = GetCurrentUser();

            bool isManager = user.Roles.Any(x => x == AppRole.Manager);
            if (!isManager)
            {
                if (model.SearchUserId.HasValue)
                {
                    return SuccessResult(new PagingResponseModel<OnsiteApplicationPagingModel>()
                    {
                        Items = [],
                        TotalRecord = 0
                    });
                }
            }

            var result = await _onsiteApplicationService.GetAllWithPagingAsync(model, user.Id);
            return SuccessResult(result);
        }

        [HttpPost]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> CreateAsync([FromForm] OnsiteApplicationRequest model)
        {
            var user = GetCurrentUser();

            var result = await _onsiteApplicationService.PrepareCreateAsync(model, user);

            if (!result.Status || !string.IsNullOrEmpty(result.ErrorMessage) || result.Data == null)
            {
                return ErrorResult(result.ErrorMessage ?? "Lỗi khi tạo đơn công tác");
            }

            //Send Mail
            BackgroundJob.Enqueue<IOnsiteApplicationService>(x => x.SendMailAsync(result.Data.Id));

            //Send Notification
            BackgroundJob.Enqueue<IOnsiteApplicationService>(x => x.SendNotificationAsync(result.Data));

            return SuccessResult("Đơn công tác được tạo thành công");
        }

        [Route("{id}")]
        [HttpPut]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> UpdateAsync([FromRoute] int id, [FromForm] OnsiteApplicationUpdateRequest model)
        {
            var user = GetCurrentUser();

            var result = await _onsiteApplicationService.PrepareUpdateAsync(id, model, user.Id);

            if (!result.Status || !string.IsNullOrEmpty(result.ErrorMessage) || result.Data == null)
            {
                return ErrorResult(result.ErrorMessage ?? "Lỗi khi cập nhật đơn công tác");
            }

            if (result.Data.Status == EReviewStatus.Pending)
            {
                //Send Mail
                BackgroundJob.Enqueue<IOnsiteApplicationService>(x => x.SendMailAsync(result.Data.Id));
            }

            //Send Notification
            BackgroundJob.Enqueue<IOnsiteApplicationService>(x => x.SendNotificationAsync(result.Data));

            return SuccessResult("Cập nhật đơn công tác thành công");
        }

        [Route("{id}")]
        [HttpDelete]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> DeleteAsync([FromRoute] int id)
        {
            var user = GetCurrentUser();

            var application = await _unitOfWork.OnsiteApplicationRepository.GetByIdAsync(id);

            if (application == null)
            {
                return ErrorResult("Đơn công tác không tồn tại");
            }

            if (application.Status != (int)EReviewStatus.Pending)
            {
                return ErrorResult("Đơn công tác đã được duyệt");
            }
            if (application.UserId != user.Id)
            {
                return ErrorResult("Người dùng không có quyền xoá đơn công tác");
            }

            await _unitOfWork.OnsiteApplicationFileRepository.DeleteRangeAsync(application.OnsiteApplicationFiles.ToList());
            await _unitOfWork.OnsiteApplicationRepository.DeleteAsync(application);
            await _unitOfWork.SaveChangesAsync();

            return SuccessResult("Xóa đơn công tác thành công");
        }

        [Route("users-manager")]
        [HttpGet]
        [Authorize(Policy = AuthorizationPolicies.ManagerRoleRequired)]
        public async Task<IActionResult> GetUsersSubmitManagerAsync()
        {
            var user = GetCurrentUser();

            var users = await _unitOfWork.UserInternalRepository.GetUsersSubmitOnSiteApplicationManagerAsync(user.Id);

            return SuccessResult(users);
        }

        [Route("review/{id}")]
        [HttpPut]
        [Authorize(Policy = AuthorizationPolicies.ManagerRoleRequired)]
        public async Task<IActionResult> ReviewAsync([FromRoute] int id, [FromBody] ReviewModel model)
        {
            var application = await _unitOfWork.OnsiteApplicationRepository.GetByIdAsync(id);

            if (application == null)
            {
                return ErrorResult("Đơn công tác không tồn tại");
            }

            var user = GetCurrentUser();


            if (application.Status != (int)EReviewStatus.Pending)
            {
                return ErrorResult("Đơn công tác đã được duyệt");
            }

            if (user.Id != application.ReviewUserId)
            {
                return ErrorResult("Người duyệt không hợp lệ");
            }

            if (model.ReviewStatus != EReviewStatus.Rejected && model.ReviewStatus != EReviewStatus.Reviewed)
            {
                return ErrorResult("Trạng thái duyệt không hợp lệ");
            }

            application.UpdatedDate = DateTime.UtcNow.UTCToIct();
            application.ReviewDate = DateTime.UtcNow.UTCToIct();
            application.Status = (int)model.ReviewStatus;
            application.ReviewNote = model.ReviewNote;

            await _unitOfWork.OnsiteApplicationRepository.UpdateAsync(application);
            await _unitOfWork.SaveChangesAsync();

            //Send Mail
            BackgroundJob.Enqueue<IOnsiteApplicationService>(x => x.SendMailAsync(application.Id));

            var onsiteApplicationNotificationModel = new OnsiteApplicationNotificationModel()
            {
                Id = application.Id,
                FromDate = application.FromDate,
                ToDate = application.ToDate,
                Status = (EReviewStatus)application.Status,
                ReviewNote = application.ReviewNote,
                ProjectName = application.ProjectName!,
                Location = application.Location!,
                ReviewUserId = application.ReviewUserId,
                UserId = application.UserId,
                UserFullName = application.User.FullName,
            };

            //SendNotification
            BackgroundJob.Enqueue<IOnsiteApplicationService>(x => x.SendNotificationAsync(onsiteApplicationNotificationModel));

            return SuccessResult("Duyệt đơn công tác thành công");
        }

        [Route("mobile/paging")]
        [HttpGet]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> Paging([FromQuery] OnsiteApplicationMobileCriteriaModel model)
        {
            var user = GetCurrentUser();
            var result
                = await _onsiteApplicationService.GetAllWithPagingMobileAsync(model, user.Id);
            return SuccessResult(result);
        }
    }
}
