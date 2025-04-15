using InternalPortal.ApplicationCore.Interfaces;
using InternalPortal.ApplicationCore.Interfaces.Business;
using InternalPortal.ApplicationCore.Models.Calendar;
using InternalPortal.ApplicationCore.ValueObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InternalPortal.API.Controllers
{
    public class CalendarController : BaseApiController
    {
        private readonly ICalendarService _calendarService;
        private readonly IUnitOfWork _unitOfWork;
        public CalendarController (
            ICalendarService calendarService,
            IUnitOfWork unitOfWork
            )
        {
            _calendarService = calendarService;
            _unitOfWork = unitOfWork;
        }
        [Route("paging")]
        [HttpGet]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> PagingAsync([FromQuery] CalendarRequest request)
        {
            var user = GetCurrentUser();
            var result = await _calendarService.GetAllWithPagingAsync(request, user.Id);
            return SuccessResult(result);
        }

        [Route("users-manager")]
        [HttpGet]
        [Authorize(Policy = AuthorizationPolicies.ManagerRoleRequired)]
        public async Task<IActionResult> GetUsersSubmitManagerAsync()
        {
            var user = GetCurrentUser();
            var response = new List<CalendarUserDropdown>();

            var onsiteUsers = await _unitOfWork.UserInternalRepository.GetUsersSubmitOnSiteApplicationManagerAsync(user.Id);
            var wfhUsers = await _unitOfWork.UserInternalRepository.GetUsersSubmitWorkFromHomeApplicationManagerAsync(user.Id);
            var leaveUsers = await _unitOfWork.UserInternalRepository.GetUsersInLeaveApplicationByManagerIdAsync(user.Id);

            response.AddRange(onsiteUsers.Select(o => new CalendarUserDropdown
            {
                Id = o.Id,
                FullName = o.FullName ?? "",
                Name = o.Name ?? "",
                Email = o.Email ?? "",
                JobTitle = o.JobTitle ?? ""
            }).ToList());

            response.AddRange(wfhUsers.Select(o => new CalendarUserDropdown
            {
                Id = o.Id,
                FullName = o.FullName ?? "",
                Name = o.Name ?? "",
                Email = o.Email ?? "",
                JobTitle = o.JobTitle ?? ""
            }).ToList());

            response.AddRange(leaveUsers.Select(o => new CalendarUserDropdown
            {
                Id = o.Id,
                FullName = o.FullName ?? "",
                Name = o.Name ?? "",
                Email = o.Email ?? "",
                JobTitle = o.JobTitle ?? ""
            }).ToList());

            response = response.DistinctBy(o => o.Id).OrderBy(z => z.FullName).ToList();
            return SuccessResult(response);
        }
    }
}
