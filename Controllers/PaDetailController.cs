using InternalPortal.ApplicationCore.Interfaces;
using InternalPortal.ApplicationCore.Interfaces.Business;
using InternalPortal.ApplicationCore.Models.Pa;
using InternalPortal.ApplicationCore.Models.PaDetail;
using InternalPortal.ApplicationCore.ValueObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Web.Resource;

namespace InternalPortal.API.Controllers
{
    public class PaDetailController : BaseApiController
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IPaDetailService _paDetailService;
        public PaDetailController(
            IUnitOfWork unitOfWork,
            IPaDetailService paDetailService
            )
        {
            _unitOfWork = unitOfWork;
            _paDetailService = paDetailService;
        }

        /// <summary>
        /// Nhân viên xem form đánh giá của bản thân
        /// </summary>
        /// <param name="paId"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("getuserpadetail")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> GetDetailsByUser([FromQuery] int paId)
        {
            // Validate user
            var user = GetCurrentUser();
            try
            {
                return SuccessResult(await _paDetailService.GetUserPaDetailAsync(paId, user.Id));
            }
            catch (Exception ex)
            {
                return ErrorResult(ex.Message);
            }
        }
        /// <summary>
        /// Nhân viên xem form đánh giá
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost]
        [Route("displayform-user")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> DisplayUserFormPa([FromBody] UserPaFormRequest request)
        {
            // Validate user
            var user = GetCurrentUser();
            try
            {
                return SuccessResult(await _paDetailService.GetUserPaFormAsync(request.PaId, request.EvaluateeId, user.Id));
            }
            catch (Exception ex)
            {
                return ErrorResult(ex.Message);
            }
        }
        /// <summary>
        /// Lấy danh sách nhóm câu hỏi
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost]
        [Route("getqg-user")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> GetQuestionGroupsUser([FromBody] UserPaFormRequest request)
        {
            // Validate user
            var user = GetCurrentUser();
            try
            {
                var pa = await _unitOfWork.PaRepository.GetByIdAsync(request.PaId);
                if (pa == null)
                {
                    return ErrorResult("Kỳ đánh giá này không tồn tại.");
                }

                var paDetail = pa.PaDetails.FirstOrDefault(x => x.UserId == request.EvaluateeId);
                if (paDetail == null)
                {
                    return ErrorResult("Đánh giá này không tồn tại.");
                }

                var paRelative = paDetail.PaRelatives.FirstOrDefault(x => x.AssessUserId == (request.EvaluateeId == user.Id ? null : user.Id));
                if (paRelative == null)
                {
                    return ErrorResult("Không thể xem đánh giá người tham gia này.");
                }

                var questionGroups = await _paDetailService.GetPaFormQuestionsAsync(pa.Id, paRelative.PaHistory.Id);
                return SuccessResult(questionGroups);
            }
            catch (Exception ex)
            {
                return ErrorResult(ex.Message);
            }
        }
        /// <summary>
        /// Lấy nhóm câu hỏi chung
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("get-ga")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> GetGeneralAssessments()
        {
            var generalAssessment = new
            {
                Id = 1,
                Name = "General assessment on development needs",
                Questions = new List<Object>()
                {
                    new { Id = 1, Name = "What should the individual START doing to be more effective?", },
                    new { Id = 2, Name = "What should the individual CONTINUE doing to be more effective?", },
                    new { Id = 3, Name = "What should the individual STOP doing to be more effective?", },
                    new { Id = 4, Name = "General Assessment", },
                }
            };
            return SuccessResult(generalAssessment);
        }
        /// <summary>
        /// Lấy nhóm câu hỏi lộ trình
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("get-cp")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> GetCareerPathQuestionGroups()
        {
            var careerPath = new
            {
                Id = 1,
                Name = "Career Path (Technical/Management Orientation)",
                Questions = new List<Object>()
                {
                    new { Id = 1, Name = "What would you do in next 12 months for developing your career?", },
                    new { Id = 2, Name = "How would you want to be in next 3 years?", },
                }
            };
            return SuccessResult(careerPath);
        }
        /// <summary>
        /// Nhân viên cập nhật form đánh giá của mình
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPut]
        [Route("update-pa")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> UpdateFormPa([FromBody] PaFormDataRequest request)
        {
            // Validate user
            var user = GetCurrentUser();
            try
            {
                var paHistory = await _paDetailService.UpdatePaFormAsync(user.Id, request);
                return SuccessResult("Lưu đánh giá thành công");
            }
            catch (Exception ex)
            {
                return ErrorResult(ex.Message);
            }
        }
        /// <summary>
        /// Đánh dấu đã hoàn thành form
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPut]
        [Route("complete-pa")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> CompleteFormPa([FromBody] CompleteFormPaRequest request)
        {
            // Validate user
            var user = GetCurrentUser();
            try
            {
                await _paDetailService.CompletePaFormAsync(user.Id, request.PaFormData);
                return SuccessResult("Bạn đã hoàn thành đánh giá này !!!");
            }
            catch (Exception ex)
            {
                return ErrorResult(ex.Message);
            }
        }
        /// <summary>
        /// Lấy ghi chú của manager lên form
        /// </summary>
        /// <param name="paId"></param>
        /// <param name="evaluateeId"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("get-pa-note")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> GetPaNote([FromQuery] int paId, [FromQuery] int evaluateeId)
        {
            try
            {
                return SuccessResult(await _paDetailService.GetOneToOneNoteAsync(paId, evaluateeId));
            }
            catch (Exception ex)
            {
                return ErrorResult(ex.Message);
            }
        }
        /// <summary>
        /// Cập nhật ghi chú
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPut]
        [Route("update-pa-note")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> UpdatePaNote([FromBody] PaNoteRequest request)
        {
            var user = GetCurrentUser();
            try
            {
                await _paDetailService.UpdateOneToOneNoteAsync(request, user.Id);
                return SuccessResult("Lưu ghi chú thành công");
            }
            catch (Exception ex)
            {
                return ErrorResult(ex.Message);
            }
        }
    }
}
