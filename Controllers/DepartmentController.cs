using InternalPortal.ApplicationCore.Interfaces;
using InternalPortal.ApplicationCore.Models.Department;
using InternalPortal.ApplicationCore.ValueObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InternalPortal.API.Controllers
{
    public class DepartmentController : BaseApiController
    {
        private readonly IUnitOfWork _unitOfWork;

        public DepartmentController(
            IUnitOfWork unitOfWork
        )
        {
            _unitOfWork = unitOfWork;
        }

        [HttpGet]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> GetDropDownAsync()
        {
            var departments = await _unitOfWork.DepartmentRepository.GetAllAsync();
            var response = departments.Select(o => new DepartmentResponse()
            {
                Id = o.Id,
                Name = o.Name,
                Managers = o.OwnerOfDepartments.Select(y => new DepartmentManagerModel()
                {
                    ManagerId = y.User.Id,
                    ManagerName = y.User.FullName
                }).ToList()
            }).ToList().OrderBy(o => o.Name);

            return SuccessResult(response);
        }
        [HttpGet]
        [Route("drop-down")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> GetDepartmentDropdown()
        {
            var res = await _unitOfWork.DepartmentRepository.GetAllAsync();
            var dropdown = res.Select(t => new DepartmentDropdownResponse()
            {
                Id = t.Id,
                Name = t.Name
            }).ToList();
            return SuccessResult(dropdown);
        }
    }
}

