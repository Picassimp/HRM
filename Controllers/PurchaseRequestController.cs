using Hangfire;
using InternalPortal.ApplicationCore.Enums;
using InternalPortal.ApplicationCore.Interfaces;
using InternalPortal.ApplicationCore.Interfaces.Business;
using InternalPortal.ApplicationCore.Models.PurchaseOrder;
using InternalPortal.ApplicationCore.Models.PurchaseOrder.Director;
using InternalPortal.ApplicationCore.Models.PurchaseOrder.Hr;
using InternalPortal.ApplicationCore.Models.PurchaseOrder.PurchaseRequest;
using InternalPortal.ApplicationCore.Models.User;
using InternalPortal.ApplicationCore.ValueObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using static InternalPortal.ApplicationCore.ValueObjects.Global;

namespace InternalPortal.API.Controllers
{
    public class PurchaseRequestController : BaseApiController
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IPurchaseRequestService _purchaseRequestService;
        private readonly IPurchaseRequestLineItemService _purchaseRequestLineItemService;

        public PurchaseRequestController(
            IUnitOfWork unitOfWork,
            IPurchaseRequestService purchaseRequestService,
            IPurchaseRequestLineItemService purchaseRequestLineItemService
            )
        {
            _unitOfWork = unitOfWork;
            _purchaseRequestService = purchaseRequestService;
            _purchaseRequestLineItemService = purchaseRequestLineItemService;
        }

        #region Nhân viên
        [HttpGet("paging")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> PagingAsync([FromQuery] PurchaseRequestPagingModel request)
        {
            var user = GetCurrentUser();
            var response = await _purchaseRequestService.GetAllWithPagingAsync(request, user.Id);
            return SuccessResult(response);
        }

        [HttpGet("{id}")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> GetAsync([FromRoute] int id)
        {
            var user = GetCurrentUser();
            var response = await _purchaseRequestService.GetByUserIdAndPrIdAsync(user.Id, id);
            if (!response.Status || !string.IsNullOrEmpty(response.ErrorMessage))
            {
                return ErrorResult(response.ErrorMessage ?? "Lỗi khi xem đơn yêu cầu đặt hàng");
            }
            return SuccessResult(response);
        }

        [HttpPost]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> CreateAsync([FromForm] PurchaseRequestCreateModel request)
        {
            var user = GetCurrentUser();
            var permission = await FindPermissionAsync(user);
            var response = await _purchaseRequestService.PrepareCreateAsync(user, permission, request);
            if (!response.Status || !string.IsNullOrEmpty(response.ErrorMessage))
            {
                return ErrorResult(response.ErrorMessage ?? "Lỗi khi tạo đơn yêu cầu đặt hàng");
            }

            //Send Mail
            BackgroundJob.Enqueue<IPurchaseRequestService>(x => x.SendEmailAsync(response.Data));
            return SuccessResult("Tạo đơn yêu cầu đặt hàng thành công");
        }

        [HttpPut("{id}")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> UpdateAsync([FromRoute] int id, [FromForm] PurchaseRequestUpdateModel request)
        {
            var user = GetCurrentUser();
            var permission = await FindPermissionAsync(user);
            var response = await _purchaseRequestService.PrepareUpdateAsync(id, user, permission, request);
            if (!response.Status || !string.IsNullOrEmpty(response.ErrorMessage))
            {
                return ErrorResult(response.ErrorMessage ?? "Lỗi khi cập nhật đơn yêu cầu đặt hàng");
            }
            //Send Mail
            BackgroundJob.Enqueue<IPurchaseRequestService>(x => x.SendEmailAsync(response.Data));

            return SuccessResult("Cập nhật đơn yêu cầu đặt hàng thành công");
        }

        [HttpPut("{id}/cancelled")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> CancelledAsync([FromRoute] int id)
        {
            var user = GetCurrentUser();
            var response = await _purchaseRequestService.PrepareCancelledAsync(id, user.Id);
            if (!response.Status || !string.IsNullOrEmpty(response.ErrorMessage))
            {
                return ErrorResult(response.ErrorMessage ?? "Lỗi khi hủy đơn yêu cầu đặt hàng");
            }
            return SuccessResult("Hủy đơn yêu cầu đặt hàng thành công");
        }

        [HttpGet("product-category-dropdown")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> GetProductCategoryAsync()
        {
            var response = await _unitOfWork.ProductCategoryRepository.GetDropdownAsync();
            return SuccessResult(response);
        }

        [HttpGet("product-dropdown")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> GetProductAsync()
        {
            var response = await _unitOfWork.ProductRepository.GetDropdownAsync();
            return SuccessResult(response);
        }

        [HttpDelete("{id}/deleted")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> DeleteAsync([FromRoute] int id)
        {
            var user = GetCurrentUser();
            var response = await _purchaseRequestService.PrepareDeleteAsync(id, user.Id);
            if (!response.Status || !string.IsNullOrEmpty(response.ErrorMessage))
            {
                return ErrorResult(response.ErrorMessage ?? "Lỗi khi xóa đơn yêu cầu đặt hàng");
            }
            return SuccessResult("Xóa đơn yêu cầu đặt hàng thành công");
        }
        #endregion

        #region Quản lý

        [HttpGet]
        [Route("manager-paging")]
        [Authorize(Policy = AuthorizationPolicies.ManagerRoleRequired)]
        public async Task<IActionResult> ManagerPagingAsync([FromQuery] PurchaseRequestManagerPagingRequest request)
        {
            var user = GetCurrentUser();
            var response = await _purchaseRequestService.GetAllWithPagingByManagerAsync(user.Id, request);
            return SuccessResult(response);
        }

        [HttpGet]
        [Route("manager-get-detail/{id}")]
        [Authorize(Policy = AuthorizationPolicies.ManagerRoleRequired)]
        public async Task<IActionResult> ManagerGetDetailAsync([FromRoute] int id)
        {
            var user = GetCurrentUser();

            var response = await _purchaseRequestService.GetByManagerIdAndPrIdAsync(user.Id, id);
            if (!response.Status || !string.IsNullOrEmpty(response.ErrorMessage))
            {
                return ErrorResult(response.ErrorMessage ?? "Lỗi khi xem đơn yêu cầu đặt hàng");
            }

            return SuccessResult(response);
        }

        [HttpPut]
        [Route("manager-review/{id}")]
        [Authorize(Policy = AuthorizationPolicies.ManagerRoleRequired)]
        public async Task<IActionResult> ManagerReviewAsync([FromRoute] int id, [FromBody] PurchaseRequestManagerReviewModel request)
        {
            var user = GetCurrentUser();

            var response = await _purchaseRequestService.PrepareReviewAsync(id, user, request);
            if (!response.Status || !string.IsNullOrEmpty(response.ErrorMessage))
            {
                return ErrorResult(response.ErrorMessage ?? "Lỗi khi duyệt đơn yêu cầu đặt hàng");
            }

            //Send Mail
            BackgroundJob.Enqueue<IPurchaseRequestService>(x => x.SendEmailFromManagerAsync(response.Data));

            return SuccessResult("Duyệt đơn yêu cầu đặt hàng thành công");
        }

        [HttpPut]
        [Route("manager-update-request/{id}")]
        [Authorize(Policy = AuthorizationPolicies.ManagerRoleRequired)]
        public async Task<IActionResult> ManagerUpdateRequestAsync([FromRoute] int id, PurchaseRequestManagerUpdateRequestModel request)
        {
            var user = GetCurrentUser();

            var response = await _purchaseRequestService.PrepareUpdateRequestAsync(id, user, request);
            if (!response.Status || !string.IsNullOrEmpty(response.ErrorMessage))
            {
                return ErrorResult(response.ErrorMessage ?? "Lỗi khi cập nhật đơn yêu cầu đặt hàng");
            }

            //Send Mail
            BackgroundJob.Enqueue<IPurchaseRequestService>(x => x.SendEmailFromManagerAsync(response.Data));
            return SuccessResult("Duyệt đơn yêu cầu đặt hàng thành công");
        }

        #endregion

        #region API dùng chung
        private async Task<PurchaseRequestPermissionModel> FindPermissionAsync(UserDtoModel user)
        {
            bool isManager = user.Roles.Any(x => x == AppRole.Manager);

            var hrValue = await _unitOfWork.GlobalConfigurationRepository.GetByNameAsync(Constant.FirstHrEmail);
            bool isHr = false;
            if (hrValue != null && !string.IsNullOrEmpty(hrValue.Value))
            {
                var hrEmails = hrValue.Value!.Split(",").Where(email => !string.IsNullOrEmpty(email)).Select(o => o.Trim()).ToList();
                if (hrEmails.Any(o => o.Equals(user.Email)))
                {
                    isHr = true;
                }
            }

            var accountantValue = await _unitOfWork.GlobalConfigurationRepository.GetByNameAsync(Constant.SecondHrEmail);
            bool isAccountant = false;
            if (accountantValue != null && !string.IsNullOrEmpty(accountantValue.Value))
            {
                var accountantEmails = accountantValue.Value!.Split(",").Where(email => !string.IsNullOrEmpty(email)).Select(o => o.Trim()).ToList();
                if (accountantEmails.Any(o => o.Equals(user.Email)))
                {
                    isAccountant = true;
                }
            }

            var directorValue = await _unitOfWork.GlobalConfigurationRepository.GetByNameAsync(Constant.DirectorEmail);
            bool isDirector = false;
            if (directorValue != null && !string.IsNullOrEmpty(directorValue.Value))
            {
                var directorEmails = directorValue.Value!.Split(",").Where(email => !string.IsNullOrEmpty(email)).Select(o => o.Trim()).ToList();
                if (directorEmails.Any(o => o.Equals(user.Email)))
                {
                    isDirector = true;
                }
            }
            return new PurchaseRequestPermissionModel
            {
                IsManager = isManager,
                IsHr = isHr,
                IsAccountant = isAccountant,
                IsDirector = isDirector
            };
        }

        [HttpGet("project-dropdown")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> GetProjectDropdownAsync([FromQuery] ProjectDropdownRequest request)
        {
            var response = await _unitOfWork.ProjectRepository.GetByUserIdsAsync(request.UserIds);
            return SuccessResult(response);
        }

        /// <summary>
        /// Hr/Accountant/Diretor lấy thông tin của PR có chứa PO/EB
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet("{id}/admin")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> GetPrDtoAsync([FromRoute] int id)
        {
            var user = GetCurrentUser();
            var permission = await FindPermissionAsync(user);
            if (!permission.IsHr && !permission.IsAccountant && !permission.IsDirector)
            {
                return ErrorResult("Bạn không có quyền xem yêu cầu này");
            }
            var response = await _purchaseRequestService.GetPrDtoByPrIdAsync(id);
            if (!response.Status || !string.IsNullOrEmpty(response.ErrorMessage))
            {
                return ErrorResult(response.ErrorMessage ?? "Lỗi khi xem đơn yêu cầu đặt hàng");
            }
            return SuccessResult(response);
        }

        /// <summary>
        /// Accountant/Director duyệt nhiều request
        /// </summary>
        /// <param name="id"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPut("admin/review")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> AdminReviewAsync([FromBody] AdminMultipleRequestReviewModel request)
        {
            var user = GetCurrentUser();
            var permission = await FindPermissionAsync(user);
            if (!permission.IsAccountant && !permission.IsDirector)
            {
                return ErrorResult("Bạn không có quyền duyệt đơn");
            }

            var response = await _purchaseRequestService.AdminPrepareReviewAsync(request);
            if (!response.Status || !string.IsNullOrEmpty(response.ErrorMessage) || response.Data == null)
            {
                return ErrorResult(response.ErrorMessage ?? "Lỗi khi duyệt danh sách đơn yêu cầu đặt hàng");
            }

            // Khi Accoutanta duyệt
            if (!request.IsDirector)
            {
                foreach (var prId in response.Data)
                {
                    BackgroundJob.Enqueue<IPurchaseRequestService>(z => z.AccountantSendEmailAsync(user.FullName!, prId));
                }
            }
            // Khi Director duyệt
            else
            {
                foreach (var prId in response.Data)
                {
                    BackgroundJob.Enqueue<IPurchaseRequestService>(z => z.DirectorSendEmailAsync(user.FullName!, prId));
                }
            }
            return SuccessResult("Duyệt danh sách đơn đặt hàng thành công");
        }

        /// <summary>
        /// Api này để cho hr 1 cheat status khi có yccn từ hr2/director
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpPut("hr/{id}/update-status")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> HrUpdateStatusAsync([FromRoute] int id)
        {
            var user = GetCurrentUser();
            var permission = await FindPermissionAsync(user);
            if (!permission.IsHr)
            {
                return ErrorResult("Bạn không có quyền truy cập");
            }

            var pr = await _unitOfWork.PurchaseRequestRepository.GetByIdAsync(id);
            if (pr == null)
            {
                return ErrorResult("Đơn yêu cầu không tồn tại");
            }

            if (pr.ReviewStatus != (int)EPurchaseRequestStatus.AccountantUpdateRequest &&
                pr.ReviewStatus != (int)EPurchaseRequestStatus.DirectorUpdateRequest)
            {
                return ErrorResult("Trạng thái đơn không hợp lệ");
            }

            if (!pr.PurchaseRequestLineItems.Any(o => o.PoprlineItems.Any() || o.ExportBillLineItems.Any()))
            {
                return ErrorResult("Đơn yêu cầu chưa có PO hoặc phiếu xuất kho");
            }

            pr.ReviewStatus = (int)EPurchaseRequestStatus.ManagerApproved;
            await _unitOfWork.PurchaseRequestRepository.UpdateAsync(pr);
            await _unitOfWork.SaveChangesAsync();
            return SuccessResult("Cập nhật đơn thành công");
        }
        #endregion

        #region Hr
        [HttpPut("hr/{id}/review")]
        [Authorize(Policy = AuthorizationPolicies.HumanResourceRoleRequired)]
        public async Task<IActionResult> HrReviewAsync([FromRoute] int id, [FromBody] HrReviewModel request)
        {
            var user = GetCurrentUser();
            var permission = await FindPermissionAsync(user);
            if (!permission.IsHr)
            {
                return ErrorResult("Bạn không có quyền duyệt");
            }

            var response = await _purchaseRequestService.HrPrepareReviewAsync(id, request);
            if (!response.Status || !string.IsNullOrEmpty(response.ErrorMessage))
            {
                return ErrorResult(response.ErrorMessage ?? "Lỗi khi xem đơn yêu cầu đặt hàng");
            }

            BackgroundJob.Enqueue<IPurchaseRequestService>(x => x.HrSendEmailAsync(response.Data));
            return SuccessResult("Duyệt đơn đặt hàng thành công");
        }
        [HttpGet]
        [Route("hr/paging")]
        [Authorize(Policy = AuthorizationPolicies.HumanResourceRoleRequired)]
        public async Task<IActionResult> GetPurchaseRequestPagingAsync([FromQuery] HRPurchaseRequestPagingRequest model)
        {
            var user = GetCurrentUser();
            var accountantEmails = await _unitOfWork.GlobalConfigurationRepository.GetByNameAsync(Constant.FirstHrEmail);
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
            var res = await _purchaseRequestService.HRGetAllWithPagingAsync(model);
            return SuccessResult(res);
        }
        [HttpGet]
        [Route("hr/purchaseOrder/{purchaseOrderId}/purchaseRequest/drop-down")]
        [Authorize(Policy = AuthorizationPolicies.HumanResourceRoleRequired)]
        public async Task<IActionResult> GetPurchaseOrderDropdownAsync([FromRoute] int purchaseOrderId)
        {
            var user = GetCurrentUser();
            var accountantEmails = await _unitOfWork.GlobalConfigurationRepository.GetByNameAsync(Constant.FirstHrEmail);
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
            var purchaseOrder = await _unitOfWork.PurchaseOrderRepository.GetByIdAsync(purchaseOrderId);
            if(purchaseOrder == null)
            {
                return ErrorResult("Không tồn tại đơn yêu cầu");
            }
            var rq = await _unitOfWork.PurchaseRequestRepository.GetPurchaseRequestsAsync(purchaseOrderId,purchaseOrder.IsCompensationPo);
            return SuccessResult(rq);
        }
        [HttpGet]
        [Route("hr/{purchaseRequestId}/purchaseorder/{purchaseOrderId}")]
        [Authorize(Policy = AuthorizationPolicies.HumanResourceRoleRequired)]
        public async Task<IActionResult> GetPurchaseRequestLineItemAsync([FromRoute] int purchaseRequestId, [FromRoute] int purchaseOrderId)
        {
            var user = GetCurrentUser();
            var accountantEmails = await _unitOfWork.GlobalConfigurationRepository.GetByNameAsync(Constant.FirstHrEmail);
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
            var res = await _purchaseRequestLineItemService.GetPurchaseRequestLineItemAsync(purchaseRequestId, purchaseOrderId, 0);
            return SuccessResult(res);
        }
        [HttpGet]
        [Route("hr/{purchaseRequestId}/exportbill/{exportBillId}")]
        [Authorize(Policy = AuthorizationPolicies.HumanResourceRoleRequired)]
        public async Task<IActionResult> GetPurchaseRequestLineItemForEBAsync([FromRoute] int purchaseRequestId, [FromRoute] int exportBillId)
        {
            var user = GetCurrentUser();
            var accountantEmails = await _unitOfWork.GlobalConfigurationRepository.GetByNameAsync(Constant.FirstHrEmail);
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
            var res = await _purchaseRequestLineItemService.GetPurchaseRequestLineItemAsync(purchaseRequestId, 0, exportBillId);
            return SuccessResult(res);
        }
        [HttpGet]
        [Route("hr/exportBill/{exportBillId}/purchaseRequest/drop-down")]
        [Authorize(Policy = AuthorizationPolicies.HumanResourceRoleRequired)]
        public async Task<IActionResult> GetExportBillDropdownAsync([FromRoute] int exportBillId)
        {
            var user = GetCurrentUser();
            var accountantEmails = await _unitOfWork.GlobalConfigurationRepository.GetByNameAsync(Constant.FirstHrEmail);
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
            var rq = await _unitOfWork.PurchaseRequestRepository.GetPurchaseRequestsForEBAsync(exportBillId);
            return SuccessResult(rq);
        }
        [HttpPut]
        [Route("changestatus/{id}")]
        [Authorize(Policy = AuthorizationPolicies.HumanResourceRoleRequired)]
        public async Task<IActionResult> ChangeStatusAsync([FromRoute] int id)
        {
            var user = GetCurrentUser();
            var accountantEmails = await _unitOfWork.GlobalConfigurationRepository.GetByNameAsync(Constant.FirstHrEmail);
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
                    return ErrorResult("Bạn không có quyền cập nhật thông tin này");
                }
            }
            var res = await _purchaseRequestService.ChangeStatusAsync(id);
            if(!res.Status || !string.IsNullOrEmpty(res.ErrorMessage) || res.Data == null)
            {
                return ErrorResult(res.ErrorMessage ?? "Lỗi khi cập nhật đơn yêu cầu"); 
            }
            await _unitOfWork.PurchaseRequestRepository.UpdateAsync(res.Data);
            await _unitOfWork.SaveChangesAsync();
            return SuccessResult("Cập nhật đơn yêu cầu thành công");
        }
        #endregion

        #region Accountant (HR 2)
        [HttpGet("accountant/paging")]
        [Authorize(Policy = AuthorizationPolicies.HumanResourceRoleRequired)]
        public async Task<IActionResult> AccountantPagingAsync([FromQuery] AccountantPurchaseRequestPagingModel request)
        {
            var user = GetCurrentUser();
            var permission = await FindPermissionAsync(user);
            if (!permission.IsAccountant)
            {
                return ErrorResult("Bạn không có quyền truy cập");
            }
            var response = await _purchaseRequestService.AccountantGetPrPagingAsync(request, false);
            return SuccessResult(response);
        }

        [HttpPut("accountant/{id}/review")]
        [Authorize(Policy = AuthorizationPolicies.HumanResourceRoleRequired)]
        public async Task<IActionResult> AccountantReviewAsync([FromRoute] int id, [FromBody] AccountantReviewModel request)
        {
            var user = GetCurrentUser();
            var permission = await FindPermissionAsync(user);
            if (!permission.IsAccountant)
            {
                return ErrorResult("Bạn không có quyền truy cập");
            }

            var response = await _purchaseRequestService.AccountantPrepareReviewAsync(id, request);
            if (!response.Status || !string.IsNullOrEmpty(response.ErrorMessage))
            {
                return ErrorResult(response.ErrorMessage ?? "Lỗi khi duyệt đơn yêu cầu đặt hàng");
            }

            BackgroundJob.Enqueue<IPurchaseRequestService>(x => x.AccountantSendEmailAsync(user.FullName!, response.Data));
            return SuccessResult("Duyệt đơn đặt hàng thành công");
        }
        #endregion

        #region Director
        [HttpGet("director/paging")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> DirectorPagingAsync([FromQuery] DirectorPurchaseRequestPagingModel request)
        {
            var user = GetCurrentUser();
            var permission = await FindPermissionAsync(user);
            if (!permission.IsDirector)
            {
                return ErrorResult("Bạn không có quyền truy cập");
            }
            var response = await _purchaseRequestService.AccountantGetPrPagingAsync(request, true);
            return SuccessResult(response);
        }

        [HttpPut("director/{id}/review")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> DirectorReviewAsync([FromRoute] int id, [FromBody] DirectorReviewModel request)
        {
            var user = GetCurrentUser();
            var permission = await FindPermissionAsync(user);
            if (!permission.IsDirector)
            {
                return ErrorResult("Bạn không có quyền truy cập");
            }

            var response = await _purchaseRequestService.DirectorPrepareReviewAsync(id, request);
            if (!response.Status || !string.IsNullOrEmpty(response.ErrorMessage))
            {
                return ErrorResult(response.ErrorMessage ?? "Lỗi khi xem đơn yêu cầu đặt hàng");
            }

            BackgroundJob.Enqueue<IPurchaseRequestService>(x => x.DirectorSendEmailAsync(user.FullName!, response.Data));
            return SuccessResult("Duyệt đơn đặt hàng thành công");
        }
        #endregion
    }
}
