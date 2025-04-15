using InternalPortal.ApplicationCore.Helpers;
using InternalPortal.ApplicationCore.Interfaces;
using InternalPortal.ApplicationCore.Interfaces.Email;
using InternalPortal.ApplicationCore.Models;
using InternalPortal.ApplicationCore.Models.RedemptionCode;
using InternalPortal.ApplicationCore.ValueObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Data;

namespace InternalPortal.API.Controllers
{
    public class RedemptionCodeController : BaseApiController
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ISendMailDynamicTemplateService _sendMailDynamicTemplate;

        public RedemptionCodeController(
            IUnitOfWork unitOfWork,
            ISendMailDynamicTemplateService sendMailDynamicTemplate
            )
        {
            _unitOfWork = unitOfWork;
            _sendMailDynamicTemplate = sendMailDynamicTemplate;
        }

        [HttpGet]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> Get()
        {
            var user = GetCurrentUser();
            getNewCode:
            var redemptionCode = await _unitOfWork.RedemptionCodeRepository.GetCodeAsync();
            if (redemptionCode == null)
            {
                return ErrorResult("Vui lòng mua thêm license");
            }
            redemptionCode.IssuedDate = DateTime.UtcNow.UTCToIct();
            redemptionCode.UserId = user.Id;
            try
            {
                await _unitOfWork.SaveChangesAsync();
            }
            catch (DBConcurrencyException dbConcurrencyException)
            {
                goto getNewCode;
            }
            catch (Exception ex)
            {
                return ErrorResult(ex.ToString());
            }
            var objSendMail = new ObjSendMail()
            {
                FileName = "RedemptionCodeTemplate.html",
                Mail_To = new List<string>() { user.Email! },
                Title = "[No reply] Link cài đặt ứng dụng trên iOS",
                Mail_cc = new List<string>(),
                JsonObject = JsonConvert.SerializeObject(new RedemptionCodeEmailModel
                {
                    StaffName = user.FullName,
                    CodeRedemptionLink = redemptionCode.CodeRedemptionLink
                })
            };
            await _sendMailDynamicTemplate.SendMailAsync(objSendMail);
            return SuccessResult("Link cài đặt đã gửi qua email. Vui lòng kiểm tra email");
        }
    }
}
