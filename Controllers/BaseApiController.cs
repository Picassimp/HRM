using InternalPortal.ApplicationCore.Models;
using InternalPortal.ApplicationCore.Models.User;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Web.Resource;

namespace InternalPortal.API.Controllers
{
    [Route("[controller]")]
    [ApiController]
    [Authorize]
    public class BaseApiController : ControllerBase
    {
        /// <summary>
        /// prepare error result
        /// </summary>
        /// <param name="errorMessages"></param>
        /// <returns></returns>
        [NonAction]
        public IActionResult ErrorResult(string errorMessages)
        {
            var dataResult = new ErrorResponseModel
            {
                Status = false,
                ErrorMessage = errorMessages,
            };
            return Ok(dataResult);
        }

        /// <summary>
        /// prepare success result
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        [NonAction]
        public IActionResult SuccessResult(string message)
        {
            var dataResult = new SuccessResponseModel<object>
            {
                Status = true,
                Message = message,
            };
            return Ok(dataResult);
        }

        /// <summary>
        /// prepare success result
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        [NonAction]
        public IActionResult SuccessResult(object obj, string message = "")
        {
            var dataResult = new SuccessResponseModel<object>
            {
                Status = true,
                Message = message,
                Data = obj,
            };
            return Ok(dataResult);
        }

        /// <summary>
        /// File Result
        /// </summary>
        /// <param name="file"></param>
        /// <param name="fileName"></param>
        /// <param name="contentType"></param>
        /// <returns></returns>
        [NonAction]
        public IActionResult FileResult(byte[] file, string fileName, string contentType)
        {
            if (file == null) return ErrorResult("No results were found for your selections.");
            return File(file, contentType, fileName);
        }

        [NonAction]
        public UserDtoModel GetCurrentUser()
        {
            var user = HttpContext.Items["CurrentUser"] as UserDtoModel;
            return user!;
        }
    }
}
