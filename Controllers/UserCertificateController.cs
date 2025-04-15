using InternalPortal.ApplicationCore.Interfaces;
using InternalPortal.ApplicationCore.Models.UserCertificate;
using Microsoft.AspNetCore.Mvc;

namespace InternalPortal.API.Controllers
{
    public class UserCertificateController : BaseApiController
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IConfiguration _configuration;
        public UserCertificateController(IUnitOfWork unitOfWork, IConfiguration configuration)
        {
            _unitOfWork = unitOfWork;
            _configuration = configuration;
        }
        [HttpGet]
        [Route("mycertificates")]
        public async Task<IActionResult> GetMyCertificate()
        {
            var user =GetCurrentUser(); 
            var myCertificates = await _unitOfWork.UserCertificateRepository.GetMyCertificate(user.Id);
            var domainBlob = _configuration["BlobDomainUrl"];
            var myCertificatesResponse = myCertificates != null ? myCertificates.Select(t => new UserCertificateResponse
            {
                Id = t.Id,
                AchievementDate = t.AchievementDate,
                CertificateName = t.CertificateName ?? "",
                ExpiredDate = t.ExpiredDate,
                Name = t.User.FullName ?? t.User.Name,
                FileUrls = t.UserCertificateFiles.Count > 0 ? t.UserCertificateFiles.Select(t => domainBlob + "/" + t.FileUrl).ToList() : new List<string>()
            }).ToList() : new List<UserCertificateResponse>();
            return SuccessResult(myCertificatesResponse);
        }
    }
}
