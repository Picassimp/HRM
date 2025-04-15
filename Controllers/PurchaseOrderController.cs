using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Enums;
using InternalPortal.ApplicationCore.Helpers;
using InternalPortal.ApplicationCore.Interfaces;
using InternalPortal.ApplicationCore.Interfaces.Business;
using InternalPortal.ApplicationCore.Models.PaymentPlan;
using InternalPortal.ApplicationCore.Models.PurchaseOrder.Product;
using InternalPortal.ApplicationCore.Models.PurchaseOrder.PurchaseOrder;
using InternalPortal.ApplicationCore.Models.PurchaseOrder.PurchaseRequest;
using InternalPortal.ApplicationCore.ValueObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using static InternalPortal.ApplicationCore.ValueObjects.Global;

namespace InternalPortal.API.Controllers
{
    public class PurchaseOrderController : BaseApiController
    {
        private readonly IPurchaseOrderService _purchaseOrderService;
        private readonly IUnitOfWork _unitOfWork;
        public PurchaseOrderController(IPurchaseOrderService purchaseOrderService,
            IUnitOfWork unitOfWork)
        {
            _purchaseOrderService = purchaseOrderService;
            _unitOfWork = unitOfWork;
        }
        [HttpPost]
        [Route("createfrompr/{id}")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> CreatePOFromRequestAsync([FromRoute] int id, [FromBody] AdditionalPurchaseOrderRequest request)
        {
            var user = GetCurrentUser();
            var res = await _purchaseOrderService.CreatePOFromRequestAsync(id,user.Id,user.Email, request);
            if (!res.Status || res.Data == null || !string.IsNullOrEmpty(res.ErrorMessage))
            {
                return ErrorResult(res.ErrorMessage ?? "Lỗi khi tạo PO");
            }
            await _unitOfWork.PurchaseOrderRepository.CreateAsync(res.Data);
            await _unitOfWork.SaveChangesAsync();
            return SuccessResult(res.Data.Id,"Tạo PO thành công");
        }
        [HttpGet]
        [Route("hr/{id}")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> GetPurchaseOrder([FromRoute] int id)
        {
            var user = GetCurrentUser();
            var res = await _purchaseOrderService.GetPurchaseOrderAsync(user.Email,id);
            if(!res.Status || res.Data == null || !string.IsNullOrEmpty(res.ErrorMessage))
            {
                return ErrorResult(res.ErrorMessage ?? "Lỗi khi lấy thông tin PO");
            }
            return SuccessResult(res.Data);
        }
        [HttpGet]
        [Route("hr/detail/{id}")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> GetPoDetailAsync([FromRoute] int id)
        {
            var user = GetCurrentUser();
            var res = await _purchaseOrderService.GetPOPRLineItemAsync(user.Email,id);
            if (!res.Status || res.Data == null || !string.IsNullOrEmpty(res.ErrorMessage))
            {
                return ErrorResult(res.ErrorMessage ?? "Lỗi khi lấy thông tin");
            }
            return SuccessResult(res.Data);
        }
        [HttpGet]
        [Route("hr/detail/{id}/files")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> GetFilesPOAsync([FromRoute] int id)
        {
            var user = GetCurrentUser();
            var res = await _purchaseOrderService.GetPurchaseOrderFileAsync(user.Email,id);
            if (!res.Status || res.Data == null || !string.IsNullOrEmpty(res.ErrorMessage))
            {
                return ErrorResult(res.ErrorMessage ?? "Lỗi khi lấy thông tin");
            }
            return SuccessResult(res);
        }
        [HttpGet]
        [Route("hr/paging")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> GetPurchaseOrderPaging([FromQuery] PurchaseOrderPagingModel model)
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
            var res = await _purchaseOrderService.GetPurchaseOrderPagingResponseAsync(model);
            return SuccessResult(res);
        }
        [HttpPost]
        [Route("additem/{id}")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> AddItemFromRequest([FromRoute] int id, [FromBody] ItemCreateRequest request)
        {
            var res = await _purchaseOrderService.AddItemFromRequestAsync(id, request);
            if (!res.Status || !string.IsNullOrEmpty(res.ErrorMessage))
            {
                return ErrorResult(res.ErrorMessage ?? "Lỗi khi thêm sản phẩm");
            }
            return SuccessResult("Thêm sản phẩm thành công");
        }
        [HttpGet]
        [Route("{id}/paymentplan")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> GetPOPaymentPlan([FromRoute] int id)
        {
            var user = GetCurrentUser();
            var names = new List<string>()
            {
                Constant.FirstHrEmail,
                Constant.SecondHrEmail,
                Constant.DirectorEmail
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
            var purchaseOrder = await _unitOfWork.PurchaseOrderRepository.GetByIdAsync(id);
            if (purchaseOrder == null)
            {
                return ErrorResult("Đơn mua hàng không tồn tại");
            }
            var paymentPlans = purchaseOrder.PaymentPlans.Count > 0 ? purchaseOrder.PaymentPlans.OrderBy(t=>t.CreateDate).Select(t => new PaymentPlanResponse()
            {
                Id = t.Id,
                Note = t.Note,
                PayDate = t.PayDate,
                PaymentType = t.PayType,
                PaymentTypeName = CommonHelper.GetDescription((EPaymentType)t.PayType),
                Price = t.PaymentAmount,
                Status = t.PaymentStatus,
                RefundAmount = t.RefundAmount != null ? t.RefundAmount : 0,
            }).ToList() : new List<PaymentPlanResponse>();
            var rqItems = await _unitOfWork.PODetailRepository.GetByPurchaseOrderIdAsync(purchaseOrder.Id,true);

            var rqItemsPrices = rqItems.Count > 0
                ? rqItems.Sum(y => y.VatPrice == 0 // nếu $ thuế = 0 => default value tính theo công thức
                    ? y.PoprlineItems.Sum(t => (bool)t.IsReceived // đã nhận thì lấy theo sl nhận
                        ? t.QuantityReceived * t.PurchaseOrderDetail.Price
                            + (t.QuantityReceived * t.PurchaseOrderDetail.Price) * t.PurchaseOrderDetail.Vat / 100
                        : t.Quantity * t.PurchaseOrderDetail.Price
                            + (t.Quantity * t.PurchaseOrderDetail.Price) * t.PurchaseOrderDetail.Vat / 100)
                : y.PoprlineItems.Sum(t => (bool)t.IsReceived//nếu có tiền thuế thì tính tổng từng món + tiền thuế của món đó
                    ? t.QuantityReceived * t.PurchaseOrderDetail.Price
                    : t.Quantity * t.PurchaseOrderDetail.Price) + y.VatPrice)
                : 0;


            var remainAmount = (int)Math.Floor((decimal)rqItemsPrices) - (int)Math.Floor(paymentPlans.Sum(t => t.Price));
            var res = new POPaymentPlanResponse()
            {
                PaymentPlans = paymentPlans,
                TotalPrice = remainAmount < 0 ? 0 :remainAmount,//tính số tiền còn lại phải trả
                RefundAmount = remainAmount < 0 ? (int)Math.Floor((decimal)(Math.Abs((decimal)remainAmount) - paymentPlans.Sum(t=>t.RefundAmount))) : 0,
                IsCompensationPO = purchaseOrder.IsCompensationPo,
                Status = purchaseOrder.Status,
            };
            return SuccessResult(res);  
        }
        [HttpPost]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> CreateAsync([FromBody] PurchaseOrderRequest request)
        {
            var user = GetCurrentUser();
            var res = await _purchaseOrderService.CreateAsync(user.Id,user.Email,request);
            if(!res.Status || !string.IsNullOrEmpty(res.ErrorMessage) || res.Data == null)
            {
                return ErrorResult(res.ErrorMessage ?? "Lỗi khi tạo PO");
            }
            await _unitOfWork.PurchaseOrderRepository.CreateAsync(res.Data);
            await _unitOfWork.SaveChangesAsync();
            return SuccessResult(res.Data.Id, "Tạo đơn mua hàng thành công");
        }
        [HttpPut]
        [Route("{id}")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> UpdateAsync([FromRoute] int id,[FromBody] PurchaseOrderRequest request)
        {
            var user = GetCurrentUser();
            var res = await _purchaseOrderService.UpdateAsync(id,user.Email, request);
            if (!res.Status || !string.IsNullOrEmpty(res.ErrorMessage) || res.Data == null)
            {
                return ErrorResult(res.ErrorMessage ?? "Lỗi khi cập nhật PO");
            }
            await _unitOfWork.PurchaseOrderRepository.UpdateAsync(res.Data);
            await _unitOfWork.SaveChangesAsync();
            return SuccessResult("Cập nhật đơn mua hàng thành công");
        }
        [HttpDelete]
        [Route("{id}")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> DeleteAsync([FromRoute] int id)
        {
            var user = GetCurrentUser();
            var res = await _purchaseOrderService.DeleteAsync(id,user.Email);
            if (!res.Status || !string.IsNullOrEmpty(res.ErrorMessage) || res.Data == null)
            {
                return ErrorResult(res.ErrorMessage ?? "Lỗi khi xóa PO");
            }
            await _unitOfWork.PurchaseOrderRepository.DeleteAsync(res.Data);
            await _unitOfWork.SaveChangesAsync();
            return SuccessResult("Xóa đơn mua hàng thành công");
        }
        [HttpPost]
        [Route("{id}/product/add")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> AddNewProductAsync([FromRoute] int id, [FromBody] ProductRequest request)
        {
            var user = GetCurrentUser();
            var res = await _purchaseOrderService.AddNewProductAsync(id,user.Email, request);
            if(!res.Status || !string.IsNullOrEmpty(res.ErrorMessage) || res.Data == null)
            {
                return ErrorResult(res.ErrorMessage ?? "Lỗi khi thêm sản phẩm");
            }
            if (res.Data.Any())
            {
                await _unitOfWork.PODetailRepository.CreateRangeAsync(res.Data);
            }
            await _unitOfWork.SaveChangesAsync();   
            return SuccessResult("Thêm sản phẩm thành công");
        }
        [HttpPost]
        [Route("{id}/file")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> AddFileAsync([FromRoute] int id,[FromForm] PurchaseOrderFileRequest request)
        {
            var user = GetCurrentUser();
            var res = await _purchaseOrderService.AddFileAsync(id, user.Email, request);
            if (!res.Status || !string.IsNullOrEmpty(res.ErrorMessage) || res.Data == null)
            {
                return ErrorResult(res.ErrorMessage ?? "Lỗi khi cập nhật thông tin file");
            }
            if (res.Data.Any())
            {
                await _unitOfWork.PurchaseOrderFileRepository.CreateRangeAsync(res.Data);
            }
            await _unitOfWork.SaveChangesAsync();
            return SuccessResult("Cập nhật File thành công");
        }
        [HttpPut]
        [Route("receivefull/{id}")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> ReceiveFullAsync([FromRoute] int id)
        {
            var user = GetCurrentUser();
            var res = await _purchaseOrderService.ReceiveFullAsync(id,user.Email);
            if (!res.Status || !string.IsNullOrEmpty(res.ErrorMessage) || res.Data == null)
            {
                return ErrorResult(res.ErrorMessage ?? "Lỗi khi nhận hàng");
            }
            await _unitOfWork.PurchaseOrderRepository.UpdateAsync(res.Data);
            await _unitOfWork.SaveChangesAsync();
            return SuccessResult("Nhận hàng thành công");
        }
        [HttpPut]
        [Route("isreceivefull/{id}")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> CheckFullReceiveAsync([FromRoute] int id, [FromBody] bool isSkip)
        {
            var res = await _purchaseOrderService.CheckFullReceiveAsync(id,isSkip);
            if (!res.Status || !string.IsNullOrEmpty(res.ErrorMessage) || res.Data == null)
            {
                return ErrorResult(res.ErrorMessage ?? "Lỗi khi kiểm tra đơn hàng");
            }
            return SuccessResult(res.Data);
        }
        [HttpPost]
        [Route("createCompensationPO/{id}")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> CreateCompensationPOAsync([FromRoute] int id, [FromBody] AdditionalPurchaseOrderRequest request)
        {
            var user = GetCurrentUser();
            var res = await _purchaseOrderService.CreateCompensationPOAsync(id, user.Email, user.Id,request);
            if (!res.Status || !string.IsNullOrEmpty(res.ErrorMessage) || res.Data == null)
            {
                return ErrorResult(res.ErrorMessage ?? "Lỗi khi tạo đơn hàng");
            }
            await _unitOfWork.PurchaseOrderRepository.CreateAsync(res.Data);
            await _unitOfWork.SaveChangesAsync();
            var additionalPurchaseOrder = new AdditionalPurchaseOrderRef()
            {
                PurchaseOrderId = id,
                AdditionalPurchaseOrderId = res.Data.Id,
                CreateDate = DateTime.UtcNow.UTCToIct()
            };
            await _unitOfWork.AdditionalPurchaseOrderRefRepository.CreateAsync(additionalPurchaseOrder);
            await _unitOfWork.SaveChangesAsync();
            return SuccessResult(res.Data.Id, "Tạo đơn hàng thành công");
        }
        [HttpPut]
        [Route("changestatus/{id}")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> ChangeStatusAsync([FromRoute] int id, [FromBody] bool isPurchase)
        {
            var user = GetCurrentUser();
            var res = await _purchaseOrderService.ChangeStatusAsync(id, user.Email, isPurchase);
            if (!res.Status || !string.IsNullOrEmpty(res.ErrorMessage) || res.Data == null)
            {
                return ErrorResult(res.ErrorMessage ?? "Lỗi khi thay đổi trạng thái đơn hàng");
            }
            await _unitOfWork.PurchaseOrderRepository.UpdateAsync(res.Data);
            await _unitOfWork.SaveChangesAsync();
            return SuccessResult("Thay đổi trạng thái đơn hàng thành công");
        }
        [HttpGet]
        [Route("accountant/paging")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> AccountantGetPurchaseOrderPagingAsync([FromQuery] PurchaseOrderPagingModel model)
        {
            var user = GetCurrentUser();
            var accountantEmails = await _unitOfWork.GlobalConfigurationRepository.GetByNameAsync(Constant.SecondHrEmail);
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
            var res = await _purchaseOrderService.AccountantGetPurchaseOrderPagingResponseAsync(model);
            return SuccessResult(res);
        }
        [HttpPut]
        [Route("accountant/review/{id}")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> AccountantReviewAsync([FromRoute] int id,[FromBody] PurchaseOrderReviewModel model)
        {
            var user = GetCurrentUser();
            var res = await _purchaseOrderService.AccountantReviewAsync(user.FullName,user.Email,id, model);
            if(!res.Status || !string.IsNullOrEmpty(res.ErrorMessage) || res.Data == null)
            {
                return ErrorResult(res.ErrorMessage ?? "Lỗi khi duyệt đơn mua hàng");
            }
            await _unitOfWork.PurchaseOrderRepository.UpdateAsync(res.Data);
            await _unitOfWork.SaveChangesAsync();
            return SuccessResult("Duyệt đơn mua hàng thành công");
        }
        [HttpGet]
        [Route("director/paging")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> DirectorGetPurchaseOrderPagingAsync([FromQuery] PurchaseOrderPagingModel model)
        {
            var user = GetCurrentUser();
            var directorEmail = await _unitOfWork.GlobalConfigurationRepository.GetByNameAsync(Constant.DirectorEmail);
            if (directorEmail == null || string.IsNullOrEmpty(directorEmail.Value))
            {
                return ErrorResult("Người dùng chưa được config");
            }
            var accountantEmail = directorEmail.Value.Split(",").Select(t => t.Trim()).FirstOrDefault(y => y.Equals(user.Email, StringComparison.OrdinalIgnoreCase));
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
            var res = await _purchaseOrderService.DirectorGetPurchaseOrderPagingResponseAsync(model);
            return SuccessResult(res);
        }
        [HttpPut]
        [Route("director/review/{id}")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> DirectorReviewAsync([FromRoute] int id, [FromBody] PurchaseOrderReviewModel model)
        {
            var user = GetCurrentUser();
            var res = await _purchaseOrderService.DirectorReviewAsync(user.FullName, user.Email, id, model);
            if (!res.Status || !string.IsNullOrEmpty(res.ErrorMessage) || res.Data == null)
            {
                return ErrorResult(res.ErrorMessage ?? "Lỗi khi duyệt đơn mua hàng");
            }
            await _unitOfWork.PurchaseOrderRepository.UpdateAsync(res.Data);
            await _unitOfWork.SaveChangesAsync();
            return SuccessResult("Duyệt đơn mua hàng thành công");
        }
    }
}
