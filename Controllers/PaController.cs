using InternalPortal.ApplicationCore.Interfaces;
using InternalPortal.ApplicationCore.Interfaces.Business;
using InternalPortal.ApplicationCore.ValueObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InternalPortal.API.Controllers
{
    public class PaController : BaseApiController
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IPaService _paService;
        public PaController(
            IUnitOfWork unitOfWork,
            IPaService paService
            )
        {
            _unitOfWork = unitOfWork;
            _paService = paService;
        }

        [HttpGet]
        [Route("me/annual")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> GetUserPaAnnual()
        {
            //Validate user
            var user = GetCurrentUser();
            var response = await _paService.GetMyPaAnnualAsync(user.Id);
            if (response == null)
            {
                return ErrorResult("Không có kỳ đánh giá nào");
            }
            return SuccessResult(response);
        }
        /// <summary>
        /// Lấy kỳ pa tái ký của user
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("me/manual")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> GetUserPaManual()
        {
            //Validate user
            var user = GetCurrentUser();
            var response = await _paService.GetMyPaManualAsync(user.Id);
            if (response == null)
            {
                return ErrorResult("Không có kỳ đánh giá nào");
            }
            return SuccessResult(response);
        }
    }
}
