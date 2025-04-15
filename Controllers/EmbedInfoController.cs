using InternalPortal.ApplicationCore.Interfaces.Utilities.PowerBI;
using Microsoft.AspNetCore.Mvc;

namespace InternalPortal.API.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class EmbedInfoController(IPbiEmbedService pbiEmbedService)
        : BaseApiController
    {
        [HttpGet]
        public IActionResult Get()
        {
            var listProjectLeads = new List<string>
            {
                "tai.vo@newoceaninfosys.com",
                "an.tran@nois.vn",
                "lam.huynh@nois.vn",
                "phong.pham@nois.vn",
                "sang.dao@nois.vn",
                "quan.ly@nois.vn",
                "dung.nguyen@nois.vn",
                "thao.pham@nois.vn",
                "hoa.le@nois.vn",
                "anh.tran@nois.vn",
                "andy.tran@nois.vn",
                "truong.huynh@nois.vn",
                "nhung.dong@nois.vn",
                "nam.vu@nois.vn",
                "truc.tran@nois.vn",
                "hai.hoang@nois.vn",
                "anh.nguyen.ngoc@nois.vn",
                "tho.phan@nois.vn",
                "duy.nguyen@nois.vn"
            };
            var user = GetCurrentUser();
            if (listProjectLeads.Contains(user.Email, StringComparer.OrdinalIgnoreCase))
            {
                var data = pbiEmbedService.GetEmbedParams("ec1b5c15-2ec3-49a4-8d7e-411eed387b37", "57a310fc-f441-40f6-b700-357216c31e50", "ProjectLeader", user.Email);
                return SuccessResult(data);
            }

            if (user.Email == "andy.tran@nois.vn")
            {
                var data = pbiEmbedService.GetEmbedParams("ec1b5c15-2ec3-49a4-8d7e-411eed387b37", "59ed868d-d07f-4a90-a1a1-2cd13df8ecf5", "Director", user.Email);
                return SuccessResult(data);
            }
            else
            {
                var data = pbiEmbedService.GetEmbedParams("ec1b5c15-2ec3-49a4-8d7e-411eed387b37", "7226a229-3bb7-4ee3-b9c8-707168cac666", "Staff", user.Email);
                return SuccessResult(data);
            }
        }
    }
}
