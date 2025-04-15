using InternalPortal.ApplicationCore.Interfaces;
using InternalPortal.ApplicationCore.Models.Holiday;
using InternalPortal.ApplicationCore.ValueObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InternalPortal.API.Controllers
{
    public class HolidayController : BaseApiController
    {
        private readonly IUnitOfWork _unitOfWork;

        public HolidayController(
            IUnitOfWork unitOfWork
            )
        {
            _unitOfWork = unitOfWork;
        }

        [HttpGet]
        [Route("get-all")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> GetAllAsync()
        {
            var holidays = await _unitOfWork.HolidayRepository.GetAllAsync();
            if (holidays == null || !holidays.Any())
            {
                return ErrorResult("Ngày nghỉ không tồn tại");
            }
            var response = holidays.Select(o => new HolidayResponse
            {
                Id = o.Id,
                Name = o.Name!,
                HolidayDate = o.HolidayDate,
                Description = o.Description,
                IsHolidayByYear = o.IsHolidayByYear,
                SalaryGetPercent = o.SalaryGetPercent
            }).OrderByDescending(o => o.Name).ToList();
            return SuccessResult(response);
        }
    }
}
