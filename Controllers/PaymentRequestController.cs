using Hangfire;
using InternalPortal.ApplicationCore.Enums;
using InternalPortal.ApplicationCore.Interfaces;
using InternalPortal.ApplicationCore.Interfaces.Business;
using InternalPortal.ApplicationCore.Models.PaymentRequest;
using InternalPortal.ApplicationCore.ValueObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using static InternalPortal.ApplicationCore.ValueObjects.Global;

namespace InternalPortal.API.Controllers
{
    public class PaymentRequestController : BaseApiController
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IPaymentRequestService _paymentRequestService;
        private readonly IConfiguration _configuration;
        public PaymentRequestController(IUnitOfWork unitOfWork,
            IPaymentRequestService paymentRequestService,
            IConfiguration configuration)
        {
            _unitOfWork = unitOfWork;
            _paymentRequestService = paymentRequestService;
            _configuration = configuration;
        }
        #region Nhân viên
        [HttpGet]
        [Route("{id}")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> UserGetAsync([FromRoute] int id)
        {
            var user = GetCurrentUser();
            var paymentRequest = await _unitOfWork.PaymentRequestRepository.GetByIdAsync(id);
            if(paymentRequest == null)
            {
                return ErrorResult("Không tồn tại đơn yêu cầu");
            }
            if(paymentRequest.CreateUserId != user.Id)
            {
                return ErrorResult("Đơn này không phải của bạn");
            }
            var totalPrice = paymentRequest.PaymentRequestDetails.Sum(t => Math.Round((decimal)((t.Quantity * t.Price) + (t.VatPrice == 0 ? t.Quantity * t.Price * t.Vat / 100 : t.VatPrice))));
            var totalPaymentPlan = paymentRequest.PaymentRequestPlans.Sum(t => Math.Round(t.PaymentAmount));
            var response = new PaymentRequestResponse()
            {
                Id = paymentRequest.Id,
                Name = paymentRequest.Name,
                FullName = paymentRequest.CreateUser.FullName,
                CreateUserId = paymentRequest.CreateUserId,
                DepartmentId = paymentRequest.DepartmentId ?? paymentRequest.CreateUser.DepartmentId,
                JobTitle = paymentRequest.CreateUser.JobTitle,
                CreateDate = paymentRequest.CreateDate,
                ReviewStatus = paymentRequest.ReviewStatus,
                ReviewUserId = paymentRequest.ReviewUserId,
                Type = paymentRequest.Type,
                Note = paymentRequest.Note,
                RejectReason = paymentRequest.RejectReason,
                PaymentRequestDetails = paymentRequest.PaymentRequestDetails.Select(t => new PaymentRequestDetailResponse()
                {
                    Id = t.Id,
                    Name = t.Name,
                    Price = Math.Round(t.Price),
                    Quantity = t.Quantity,
                    Vat = t.Vat ?? 0,
                    VatPrice = t.VatPrice
                }).ToList(),
                TotalPrice = totalPrice,
                IsUpdatePaymentPlan = totalPrice != totalPaymentPlan ? true : false,
                VendorId = paymentRequest.VendorId,
                AccountantComment = paymentRequest.AccountantComment ?? string.Empty,
                DirectorComment = paymentRequest.DirectorComment ?? string.Empty,
                PaymentMethod = paymentRequest.PaymentMethod ?? (int)EPaymentMethod.Cash,
                BankName = paymentRequest.BankName ?? string.Empty,
                BankNumber = paymentRequest.BankNumber ?? string.Empty,
                IsClose = paymentRequest.Isclose ?? false
            };
            return SuccessResult(response);
        }
        [HttpPost]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> CreateAsync([FromForm] PaymentRequestCreateModel model)
        {
            var user = GetCurrentUser();
            var res = await _paymentRequestService.CreateAsync(model, user.Id,user.FullName ?? string.Empty);
            if(!res.Status || !string.IsNullOrEmpty(res.ErrorMessage) || res.Data == null)
            {
                return ErrorResult(res.ErrorMessage ?? "Lỗi khi tạo yêu cầu");
            }
            return SuccessResult(res.Data.Id,"Tạo yêu cầu thanh toán thành công");
        }
        [HttpPut]
        [Route("{id}")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> UpdateAsync([FromRoute] int id, [FromForm] PaymentRequestUpdateModel model)
        {
            var user = GetCurrentUser();
            var res = await _paymentRequestService.UpdateAsync(model, user.Id,id);
            if (!res.Status || !string.IsNullOrEmpty(res.ErrorMessage) || res.Data == null)
            {
                return ErrorResult(res.ErrorMessage ?? "Lỗi khi cập nhật yêu cầu");
            }
            return SuccessResult("Cập nhật yêu cầu thành công");
        }
        [HttpDelete]
        [Route("{id}")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> DeleteAsync([FromRoute] int id)
        {
            var user = GetCurrentUser();
            var res = await _paymentRequestService.DeleteAsync(id,user.Id);
            if (!res.Status || !string.IsNullOrEmpty(res.ErrorMessage) || res.Data == null)
            {
                return ErrorResult(res.ErrorMessage ?? "Lỗi khi xóa yêu cầu");
            }
            await _unitOfWork.PaymentRequestRepository.DeleteAsync(res.Data);
            await _unitOfWork.SaveChangesAsync();
            return SuccessResult("Xóa yêu cầu thành công");
        }
        [HttpGet]
        [Route("{id}/file")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> UserGetFileAsync([FromRoute] int id)
        {
            var user = GetCurrentUser();
            var domainUrl = _configuration["BlobDomainUrl"];
            var paymentRequest = await _unitOfWork.PaymentRequestRepository.GetByIdAsync(id);
            if (paymentRequest == null)
            {
                return ErrorResult("Không tồn tại đơn yêu cầu");
            }
            if (paymentRequest.CreateUserId != user.Id)
            {
                return ErrorResult("Đơn này không phải của bạn");
            }
            var response = paymentRequest.PaymentRequestFiles.Count > 0 ? paymentRequest.PaymentRequestFiles.Select(t => new PaymentRequestFileResponse()
            {
                Id = t.Id,
                Name = domainUrl + "/" + t.Url,
            }).ToList() : new List<PaymentRequestFileResponse>();
            return SuccessResult(response); 
        }
        [HttpPost]
        [Route("{id}/file")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> AddFileAsync([FromRoute] int id, [FromForm] PaymentRequestFileRequest request)
        {
            var user = GetCurrentUser();
            var res = await _paymentRequestService.AddFileAsync(id,user.Id,request);
            if (!res.Status || !string.IsNullOrEmpty(res.ErrorMessage) || res.Data == null)
            {
                return ErrorResult(res.ErrorMessage ?? "Lỗi khi cập nhật File");
            }
            await _unitOfWork.PaymentRequestFileRepository.CreateRangeAsync(res.Data);
            await _unitOfWork.SaveChangesAsync();
            return SuccessResult("Cập nhật file thành công");
        }
        [HttpGet]
        [Route("paging")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> GetUserPagingAsync([FromQuery] PaymentRequestPagingModel model)
        {
            var user = GetCurrentUser();
            var res = await _paymentRequestService.GetUserPagingAsync(model, user.Id);
            return SuccessResult(res);
        }
        [HttpPost]
        [Route("{id}/copy")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> CopyRequest([FromRoute] int id)
        {
            var user = GetCurrentUser();
            var res = await _paymentRequestService.CopyAsync(id,user.Id);
            if(!res.Status || !string.IsNullOrEmpty(res.ErrorMessage) || res.Data == null)
            {
                return ErrorResult(res.ErrorMessage ?? "Lỗi khi copy đơn yêu cầu");
            }
            await _unitOfWork.PaymentRequestRepository.CreateAsync(res.Data);
            await _unitOfWork.SaveChangesAsync();
            BackgroundJob.Enqueue<IPaymentRequestService>(t => t.SendMail(res.Data.Id, user.FullName));
            return SuccessResult(res.Data.Id,"Copy đơn thanh toán thành công");
        }
        #endregion
        #region Manager
        [HttpGet]
        [Route("manager/paging")]
        [Authorize(Policy = AuthorizationPolicies.ManagerRoleRequired)]
        public async Task<IActionResult> GetManagerPagingAsync([FromQuery] PaymentRequestPagingModel model)
        {
            var user = GetCurrentUser();
            var res = await _paymentRequestService.GetManagerPagingAsync(model, user.Id);
            return SuccessResult(res);
        }
        [HttpGet]
        [Route("manager/view/{id}/file")]
        [Authorize(Policy = AuthorizationPolicies.ManagerRoleRequired)]
        public async Task<IActionResult> ManagerGetFileAsync([FromRoute] int id)
        {
            var user = GetCurrentUser();
            var domainUrl = _configuration["BlobDomainUrl"];
            var paymentRequest = await _unitOfWork.PaymentRequestRepository.GetByIdAsync(id);
            if (paymentRequest == null)
            {
                return ErrorResult("Không tồn tại đơn yêu cầu");
            }
            if (paymentRequest.ReviewUserId != user.Id)
            {
                return ErrorResult("Bạn không phải manager của đơn yêu cầu này");
            }
            var response = paymentRequest.PaymentRequestFiles.Count > 0 ? paymentRequest.PaymentRequestFiles.Select(t => new PaymentRequestFileResponse()
            {
                Id = t.Id,
                Name = domainUrl + "/" + t.Url,
            }).ToList() : new List<PaymentRequestFileResponse>();
            return SuccessResult(response);
        }
        [HttpGet]
        [Route("manager/view/{id}")]
        [Authorize(Policy = AuthorizationPolicies.ManagerRoleRequired)]
        public async Task<IActionResult> ManagerGetAsync([FromRoute] int id)
        {
            var user = GetCurrentUser();
            var paymentRequest = await _unitOfWork.PaymentRequestRepository.GetByIdAsync(id);
            if (paymentRequest == null)
            {
                return ErrorResult("Không tồn tại đơn yêu cầu");
            }
            if (paymentRequest.ReviewUserId != user.Id)
            {
                return ErrorResult("Bạn không phải manager của đơn yêu cầu này");
            }
            var totalPrice = paymentRequest.PaymentRequestDetails.Sum(t => Math.Round((decimal)((t.Quantity * t.Price) + (t.VatPrice == 0 ? t.Quantity * t.Price * t.Vat / 100 : t.VatPrice))));
            var totalPaymentPlan = paymentRequest.PaymentRequestPlans.Sum(t => Math.Round(t.PaymentAmount));
            var response = new PaymentRequestResponse()
            {
                Id = paymentRequest.Id,
                Name = paymentRequest.Name,
                CreateUserId  = paymentRequest.CreateUserId,
                DepartmentId = paymentRequest.DepartmentId ?? paymentRequest.CreateUser.DepartmentId,
                JobTitle = paymentRequest.CreateUser.JobTitle,
                FullName = paymentRequest.CreateUser.FullName,
                CreateDate = paymentRequest.CreateDate,
                ReviewStatus = paymentRequest.ReviewStatus,
                ReviewUserId = paymentRequest.ReviewUserId,
                Type = paymentRequest.Type,
                Note = paymentRequest.Note,
                RejectReason = paymentRequest.RejectReason,
                PaymentRequestDetails = paymentRequest.PaymentRequestDetails.Select(t => new PaymentRequestDetailResponse()
                {
                    Id = t.Id,
                    Name = t.Name,
                    Price = Math.Round(t.Price),
                    Quantity = t.Quantity,
                    Vat = t.Vat ?? 0,
                    VatPrice = t.VatPrice
                }).ToList(),
                TotalPrice = totalPrice,
                IsUpdatePaymentPlan = totalPrice != totalPaymentPlan ? true : false,
                VendorId = paymentRequest.VendorId,
                AccountantComment = paymentRequest.AccountantComment ?? string.Empty,
                DirectorComment = paymentRequest.DirectorComment ?? string.Empty,
                PaymentMethod = paymentRequest.PaymentMethod ?? (int)EPaymentMethod.Cash,
                BankName = paymentRequest.BankName ?? string.Empty,
                BankNumber = paymentRequest.BankNumber ?? string.Empty,
                IsClose = paymentRequest.Isclose ?? false,
            };
            return SuccessResult(response);
        }
        [HttpPut]
        [Route("manager/review/{id}")]
        [Authorize(Policy = AuthorizationPolicies.ManagerRoleRequired)]
        public async Task<IActionResult> ManagerReviewAsync([FromRoute] int id, [FromBody] PaymentRequestReviewModel model)
        {
            var user = GetCurrentUser();
            var res = await _paymentRequestService.ManagerReviewAsync(id, user.Id, model);
            if(!res.Status || !string.IsNullOrEmpty(res.ErrorMessage) || res.Data == null)
            {
                return ErrorResult(res.ErrorMessage ?? "Lỗi khi duyệt đơn yêu cầu");
            }
            await _unitOfWork.PaymentRequestRepository.UpdateAsync(res.Data);
            await _unitOfWork.SaveChangesAsync();
            return SuccessResult("Duyệt đơn yêu cầu thành công");
        }
        [HttpPut]
        [Route("manager/multireview")]
        [Authorize(Policy = AuthorizationPolicies.ManagerRoleRequired)]
        public async Task<IActionResult> ManagerMultiReviewAsync([FromBody] PaymentRequestMultiReviewModel model)
        {
            var user = GetCurrentUser();
            var res = await _paymentRequestService.ManagerMultiReviewAsync(model, user.Id);
            if(!res.Status || !string.IsNullOrEmpty(res.ErrorMessage) || res.Data == null)
            {
                return ErrorResult(res.ErrorMessage ?? "Lỗi khi duyệt đơn");
            }
            await _unitOfWork.PaymentRequestRepository.UpdateRangeAsync(res.Data);
            await _unitOfWork.SaveChangesAsync();
            foreach(var dt in res.Data)
            {
                BackgroundJob.Enqueue<IPaymentRequestService>(t => t.SendMail(dt.Id, user.FullName));
            }
            return SuccessResult("Duyệt đơn thành công");
        }
        #endregion
        #region Accountant
        [HttpPut]
        [Route("accountant/review/{id}")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> AccountantReviewAsync([FromRoute] int id, [FromBody] PaymentRequestReviewModel model)
        {
            var user = GetCurrentUser();
            var res = await _paymentRequestService.AccountantReviewAsync(id,model,user.FullName,user.Email);
            if (!res.Status || !string.IsNullOrEmpty(res.ErrorMessage) || res.Data == null)
            {
                return ErrorResult(res.ErrorMessage ?? "Lỗi khi duyệt đơn yêu cầu");
            }
            await _unitOfWork.PaymentRequestRepository.UpdateAsync(res.Data);
            await _unitOfWork.SaveChangesAsync();
            return SuccessResult("Duyệt đơn yêu cầu thành công");
        }
        [HttpGet]
        [Route("accountant/paging")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> GetAccountantPagingAsync([FromQuery] PaymentRequestPagingModel model)
        {
            var user = GetCurrentUser();
            var accountantEmails = await _unitOfWork.GlobalConfigurationRepository.GetByNameAsync(Constant.PaymentRequestAccountEmails);
            if (accountantEmails == null || string.IsNullOrEmpty(accountantEmails.Value))
            {
                return ErrorResult("Người dùng chưa được config");
            }
            var accountantEmail = accountantEmails.Value.Split(",").Select(t => t.Trim()).FirstOrDefault(y => y.Equals(user.Email, StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrEmpty(accountantEmail))
            {
                return ErrorResult("Người dùng chưa được config");
            }
            else
            {
                if (!accountantEmail.Equals(user.Email, StringComparison.OrdinalIgnoreCase))
                {
                    return ErrorResult("Bạn không có quyền xem thông tin này");
                }
            }
            var res = await _paymentRequestService.GetAccountantPagingAsync(model);
            return SuccessResult(res);
        }
        [HttpPut]
        [Route("accountant/multireview")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> AccountantMultiReviewAsync([FromBody] PaymentRequestMultiReviewModel model)
        {
            var user = GetCurrentUser();
            var res = await _paymentRequestService.AccountantMultiReviewAsync(model,user.Email);
            if (!res.Status || !string.IsNullOrEmpty(res.ErrorMessage) || res.Data == null)
            {
                return ErrorResult(res.ErrorMessage ?? "Lỗi khi duyệt đơn");
            }
            await _unitOfWork.PaymentRequestRepository.UpdateRangeAsync(res.Data);
            await _unitOfWork.SaveChangesAsync();
            foreach (var dt in res.Data)
            {
                BackgroundJob.Enqueue<IPaymentRequestService>(t => t.SendMail(dt.Id, user.FullName));
            }
            return SuccessResult("Duyệt đơn thành công");
        }
        [HttpPut]
        [Route("accountant/update/{id}")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> AccountantUpdateRequestAsync([FromRoute] int id, [FromBody] UpdateRequestModel model)
        {
            var user = GetCurrentUser();
            var res = await _paymentRequestService.AccountantUpdateRequestAsync(id, user.Email, model);
            if(!res.Status || !string.IsNullOrEmpty(res.ErrorMessage) || res.Data == null)
            {
                return ErrorResult(res.ErrorMessage ?? "Lỗi khi yêu cầu cập nhật");
            }
            await _unitOfWork.PaymentRequestRepository.UpdateAsync(res.Data);
            await _unitOfWork.SaveChangesAsync();
            BackgroundJob.Enqueue<IPaymentRequestService>(t=>t.SendMail(res.Data.Id, user.FullName));
            return SuccessResult("Yêu cầu cập nhật thành công");
        }
        [HttpPut]
        [Route("accountant/reset/{id}")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> AccountantResetAsync([FromRoute] int id)
        {
            var user = GetCurrentUser();
            var res = await _paymentRequestService.AccountantResetAsync(id, user.Email);
            if(!res.Status || !string.IsNullOrEmpty(res.ErrorMessage) || res.Data == null)
            {
                return ErrorResult(res.ErrorMessage ?? "Lỗi khi chuyển đổi trạng thái");
            }
            await _unitOfWork.PaymentRequestRepository.UpdateAsync(res.Data);
            await _unitOfWork.SaveChangesAsync();
            return SuccessResult("Chuyển đổi trạng thái thành công");
        }
        [HttpPut]
        [Route("accountant/cancel/{id}")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> AccountantCancelAsync([FromRoute] int id)
        {
            var user = GetCurrentUser();
            var res = await _paymentRequestService.AccountantCancelAsync(id, user.Email);
            if (!res.Status || !string.IsNullOrEmpty(res.ErrorMessage) || res.Data == null)
            {
                return ErrorResult(res.ErrorMessage ?? "Lỗi khi hủy đơn");
            }
            await _unitOfWork.PaymentRequestRepository.UpdateAsync(res.Data);
            await _unitOfWork.SaveChangesAsync();
            return SuccessResult("Hủy đơn yêu cầu thành công");
        }
        [HttpPut]
        [Route("accountant/{id}")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> AccountantUpdateAsync([FromRoute] int id, [FromBody] PaymentRequestAccountantUpdateModel model)
        {
            var user = GetCurrentUser();
            var res = await _paymentRequestService.AccountantUpdateAsync(id, user.Email,model);
            if (!res.Status || !string.IsNullOrEmpty(res.ErrorMessage) || res.Data == null)
            {
                return ErrorResult(res.ErrorMessage ?? "Lỗi khi cập nhật đơn yêu cầu");
            }
            await _unitOfWork.PaymentRequestRepository.UpdateAsync(res.Data);
            await _unitOfWork.SaveChangesAsync();
            BackgroundJob.Enqueue<IPaymentRequestService>(t => t.SendMail(res.Data.Id, user.FullName));
            return SuccessResult("Cập nhật đơn thành công");
        }
        [HttpPut]
        [Route("accountant/close/{id}")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> AccountantCloseAsync([FromRoute] int id)
        {
            var user = GetCurrentUser();
            var res = await _paymentRequestService.AccountantCloseAsync(id, user.Email);
            if (!res.Status || !string.IsNullOrEmpty(res.ErrorMessage) || res.Data == null)
            {
                return ErrorResult(res.ErrorMessage ?? "Lỗi khi đóng đơn yêu cầu");
            }
            await _unitOfWork.PaymentRequestRepository.UpdateAsync(res.Data);
            await _unitOfWork.SaveChangesAsync();
            return SuccessResult("Đóng đơn yêu cầu thành công");
        }
        #endregion
        #region Director
        [HttpPut]
        [Route("director/review/{id}")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> DirectorReviewAsync([FromRoute] int id, [FromBody] PaymentRequestReviewModel model)
        {
            var user = GetCurrentUser();
            var res = await _paymentRequestService.DirectorReviewAsync(id, model, user.FullName, user.Email);
            if (!res.Status || !string.IsNullOrEmpty(res.ErrorMessage) || res.Data == null)
            {
                return ErrorResult(res.ErrorMessage ?? "Lỗi khi duyệt đơn yêu cầu");
            }
            await _unitOfWork.PaymentRequestRepository.UpdateAsync(res.Data);
            await _unitOfWork.SaveChangesAsync();
            return SuccessResult("Duyệt đơn yêu cầu thành công");
        }
        [HttpGet]
        [Route("director/paging")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> GetDirectorPagingAsync([FromQuery] PaymentRequestPagingModel model)
        {
            var user = GetCurrentUser();
            var accountantEmails = await _unitOfWork.GlobalConfigurationRepository.GetByNameAsync(Constant.PaymentRequestDirectorEmails);
            if (accountantEmails == null || string.IsNullOrEmpty(accountantEmails.Value))
            {
                return ErrorResult("Người dùng chưa được config");
            }
            var accountantEmail = accountantEmails.Value.Split(",").Select(t => t.Trim()).FirstOrDefault(y => y.Equals(user.Email, StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrEmpty(accountantEmail))
            {
                return ErrorResult("Người dùng chưa được config");
            }
            else
            {
                if (!accountantEmail.Equals(user.Email, StringComparison.OrdinalIgnoreCase))
                {
                    return ErrorResult("Bạn không có quyền xem thông tin này");
                }
            }
            var res = await _paymentRequestService.GetDirectorPagingAsync(model);
            return SuccessResult(res);
        }
        [HttpPut]
        [Route("director/multireview")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> DirectorMultiReviewAsync([FromBody] PaymentRequestMultiReviewModel model)
        {
            var user = GetCurrentUser();
            var res = await _paymentRequestService.DirectorMultiReviewAsync(model, user.Email);
            if (!res.Status || !string.IsNullOrEmpty(res.ErrorMessage) || res.Data == null)
            {
                return ErrorResult(res.ErrorMessage ?? "Lỗi khi duyệt đơn");
            }
            await _unitOfWork.PaymentRequestRepository.UpdateRangeAsync(res.Data);
            await _unitOfWork.SaveChangesAsync();
            foreach (var dt in res.Data)
            {
                BackgroundJob.Enqueue<IPaymentRequestService>(t => t.SendMail(dt.Id, user.FullName));
            }
            return SuccessResult("Duyệt đơn thành công");
        }
        [HttpPut]
        [Route("director/update/{id}")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> DiretorUpdateAsync([FromRoute] int id, [FromBody] UpdateRequestModel model)
        {
            var user = GetCurrentUser();
            var res = await _paymentRequestService.DirectorUpdateRequestAsync(id, user.Email, model);
            if (!res.Status || !string.IsNullOrEmpty(res.ErrorMessage) || res.Data == null)
            {
                return ErrorResult(res.ErrorMessage ?? "Lỗi khi yêu cầu cập nhật");
            }
            await _unitOfWork.PaymentRequestRepository.UpdateAsync(res.Data);
            await _unitOfWork.SaveChangesAsync();
            BackgroundJob.Enqueue<IPaymentRequestService>(t => t.SendMail(res.Data.Id, user.FullName));
            return SuccessResult("Yêu cầu cập nhật thành công");
        }
        #endregion
        #region API chung cho Accountant và Director
        [HttpGet]
        [Route("view/{id}")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> GetAsync([FromRoute] int id)
        {
            var user = GetCurrentUser();
            var paymentRequest = await _unitOfWork.PaymentRequestRepository.GetByIdAsync(id);
            if (paymentRequest == null)
            {
                return ErrorResult("Không tồn tại đơn yêu cầu");
            }
            var names = new List<string>()
            {
                Constant.PaymentRequestAccountEmails,
                Constant.PaymentRequestDirectorEmails
            };
            var emails = await _unitOfWork.GlobalConfigurationRepository.GetByMultiNameAsync(names);
            if (emails.Count == 0)
            {
                return ErrorResult("Người dùng chưa được config");
            }
            var isHasAllowEmail = string.Join(",", emails.Select(t => t.Value)).Split(",").Any(y => y.Equals(user.Email, StringComparison.OrdinalIgnoreCase));
            if (!isHasAllowEmail)
            {
                return ErrorResult("Bạn không có quyền xem thông tin này");
            }
            var totalPrice = paymentRequest.PaymentRequestDetails.Sum(t => Math.Round((decimal)((t.Quantity * t.Price) + (t.VatPrice == 0 ? t.Quantity * t.Price * t.Vat / 100 : t.VatPrice))));
            var totalPaymentPlan = paymentRequest.PaymentRequestPlans.Sum(t => Math.Round(t.PaymentAmount));
            var response = new PaymentRequestResponse()
            {
                Id = paymentRequest.Id,
                Name = paymentRequest.Name,
                CreateUserId = paymentRequest.CreateUserId,
                DepartmentId = paymentRequest.DepartmentId ?? paymentRequest.CreateUser.DepartmentId,
                JobTitle =  paymentRequest.CreateUser.JobTitle,
                FullName = paymentRequest.CreateUser.FullName,
                CreateDate = paymentRequest.CreateDate,
                ReviewStatus = paymentRequest.ReviewStatus,
                ReviewUserId = paymentRequest.ReviewUserId,
                Type = paymentRequest.Type,
                Note = paymentRequest.Note,
                RejectReason = paymentRequest.RejectReason,
                PaymentRequestDetails = paymentRequest.PaymentRequestDetails.Select(t => new PaymentRequestDetailResponse()
                {
                    Id = t.Id,
                    Name = t.Name,
                    Price = Math.Round(t.Price),
                    Quantity = t.Quantity,
                    Vat = t.Vat ?? 0,
                    VatPrice = t.VatPrice
                }).ToList(),
                TotalPrice = totalPrice,
                IsUpdatePaymentPlan = totalPrice != totalPaymentPlan ? true : false,
                VendorId = paymentRequest.VendorId,
                AccountantComment = paymentRequest.AccountantComment ?? string.Empty,
                DirectorComment = paymentRequest.DirectorComment ?? string.Empty,
                PaymentMethod = paymentRequest.PaymentMethod ?? (int)EPaymentMethod.Cash,
                BankName = paymentRequest.BankName ?? string.Empty,
                BankNumber = paymentRequest.BankNumber ?? string.Empty,
                IsAllowToAccUpdate = paymentRequest.PaymentRequestPlans.Any(t => (bool)t.PaymentStatus),
                IsClose = paymentRequest.Isclose ?? false,
                IsAllowToClose = paymentRequest.PaymentRequestPlans.Any(t => (bool)!t.PaymentStatus) ? false : true
            };
            return SuccessResult(response);
        }
        [HttpGet]
        [Route("view/{id}/file")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> GetFileAsync([FromRoute] int id)
        {
            var user = GetCurrentUser();
            var domainUrl = _configuration["BlobDomainUrl"];
            var paymentRequest = await _unitOfWork.PaymentRequestRepository.GetByIdAsync(id);
            if (paymentRequest == null)
            {
                return ErrorResult("Không tồn tại đơn yêu cầu");
            }
            var names = new List<string>()
            {
                Constant.PaymentRequestAccountEmails,
                Constant.PaymentRequestDirectorEmails
            };
            var emails = await _unitOfWork.GlobalConfigurationRepository.GetByMultiNameAsync(names);
            if (emails.Count == 0)
            {
                return ErrorResult("Người dùng chưa được config");
            }
            var isHasAllowEmail = string.Join(",", emails.Select(t => t.Value)).Split(",").Any(y => y.Equals(user.Email, StringComparison.OrdinalIgnoreCase));
            if (!isHasAllowEmail)
            {
                return ErrorResult("Bạn không có quyền xem thông tin này");
            }
            var response = paymentRequest.PaymentRequestFiles.Count > 0 ? paymentRequest.PaymentRequestFiles.Select(t => new PaymentRequestFileResponse()
            {
                Id = t.Id,
                Name = domainUrl + "/" + t.Url,
            }).ToList() : new List<PaymentRequestFileResponse>();
            return SuccessResult(response);
        }
        #endregion
    }
}
