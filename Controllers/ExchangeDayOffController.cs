using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Enums;
using InternalPortal.ApplicationCore.Helpers;
using InternalPortal.ApplicationCore.Interfaces;
using InternalPortal.ApplicationCore.Models.ExchangeDayOff;
using InternalPortal.ApplicationCore.ValueObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using static InternalPortal.ApplicationCore.ValueObjects.Global;

namespace InternalPortal.API.Controllers
{
    public class ExchangeDayOffController : BaseApiController
    {
        private readonly IUnitOfWork _unitOfWork;

        public ExchangeDayOffController(
            IUnitOfWork unitOfWork
            )
        {
            _unitOfWork = unitOfWork;
        }

        [Route("common-data")]
        [HttpGet]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> GetCommonData()
        {
            var user = GetCurrentUser();
            var register = await _unitOfWork.UserInternalRepository.GetByIdAsync(user.Id);
            if (register == null)
            {
                return ErrorResult("Người gửi đơn không tồn tại");
            }

            var config = await _unitOfWork.GlobalConfigurationRepository.GetByNameAsync(Constant.ExchangeDayOffReviewerEmail);
            if (config == null)
            {
                return ErrorResult("Người duyệt không tồn tại");
            }

            var reviewer = await _unitOfWork.UserInternalRepository.GetByEmailAsync(config.Value!);
            if (reviewer == null)
            {
                return ErrorResult("Người duyệt không tồn tại");
            }

            var pendingLeaves = await _unitOfWork.LeaveApplicationRepository.GetPendingByUserIdAsync(user.Id);
            var pendingLeaveLastYear = pendingLeaves.Sum(o => o.NumberDayOffLastYear);

            return SuccessResult(new ExchangeDayOffResponse()
            {
                RemainDayOffLastYear = register.RemainDayOffLastYear - pendingLeaveLastYear,
                ReviewUserId = reviewer.Id,
                ReviewUserName = reviewer.FullName!
            });
        }

        [HttpPost]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> CreateAsync([FromBody] ExchangeDayOffRequest request)
        {
            var user = GetCurrentUser();
            var requestDate = DateTime.UtcNow.UTCToIct();
            // Hết ngày 25
            var expirationDate = new DateTime(requestDate.Year, 01, 26);
            if (requestDate > expirationDate)
            {
                return ErrorResult("Đã hết thời hạn quy đổi ngày phép");
            }

            if (request.DayOffExchange <= 0)
            {
                return ErrorResult("Ngày phép quy đổi không hợp lệ");
            }

            var register = await _unitOfWork.UserInternalRepository.GetByIdAsync(request.UserId);
            if (register == null)
            {
                return ErrorResult("Ngưởi gửi không tồn tại");
            }

            if (user.Id != register.Id)
            {
                return ErrorResult("Người tạo không hợp lệ");
            }

            var config = await _unitOfWork.GlobalConfigurationRepository.GetByNameAsync(Constant.ExchangeDayOffReviewerEmail);
            if (config == null)
            {
                return ErrorResult("Người duyệt không tồn tại");
            }

            var reviewer = await _unitOfWork.UserInternalRepository.GetByEmailAsync(config.Value!);
            if (reviewer == null)
            {
                return ErrorResult("Người duyệt không tồn tại");
            }

            if (request.ReviewUserId != reviewer.Id)
            {
                return ErrorResult("Người duyệt không hợp lệ");
            }

            var pendingLeaves = await _unitOfWork.LeaveApplicationRepository.GetPendingByUserIdAsync(user.Id);

            if (request.DayOffExchange > register.RemainDayOffLastYear - pendingLeaves.Sum(o => o.NumberDayOffLastYear))
            {
                return ErrorResult("Ngày phép quy đổi không thể lớn hơn ngày phép năm cũ");
            }
 
            var hasExchangeDayOff = await _unitOfWork.ExchangeDayOffRepository.HasExchangeByUserIdAndYearAsync(request.UserId, requestDate.Year);
            if (hasExchangeDayOff)
            {
                return ErrorResult("Chỉ được đổi ngày phép một lần trong năm");
            }

            var exchangeDayOff = new ExchangeDayOff()
            {
                ReviewUserId = reviewer.Id,
                UserId = request.UserId,
                DayOffExchange = request.DayOffExchange,
                CreatedDate = DateTime.UtcNow.UTCToIct(),
                ReviewStatus = (int)EReviewStatus.Pending,
            };
            await _unitOfWork.ExchangeDayOffRepository.CreateAsync(exchangeDayOff);
            await _unitOfWork.SaveChangesAsync();
            return SuccessResult(exchangeDayOff.Id, "Tạo đơn quy đổi ngày phép thành công");
        }
    }
}
