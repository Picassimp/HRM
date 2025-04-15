using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Enums;
using InternalPortal.ApplicationCore.Helpers;
using InternalPortal.ApplicationCore.Interfaces;
using InternalPortal.ApplicationCore.Interfaces.Business;
using InternalPortal.ApplicationCore.Interfaces.Email;
using InternalPortal.ApplicationCore.Interfaces.Utilities.AzureBlob;
using InternalPortal.ApplicationCore.Models;
using InternalPortal.ApplicationCore.Models.PurchaseOrder;
using InternalPortal.ApplicationCore.Models.PurchaseOrder.Accountant;
using InternalPortal.ApplicationCore.Models.PurchaseOrder.Director;
using InternalPortal.ApplicationCore.Models.PurchaseOrder.Hr;
using InternalPortal.ApplicationCore.Models.PurchaseOrder.PurchaseRequest;
using InternalPortal.ApplicationCore.Models.User;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using static InternalPortal.ApplicationCore.ValueObjects.Global;

namespace InternalPortal.Infrastructure.Services.Business
{
    public class PurchaseRequestService : IPurchaseRequestService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IBlobService _blobService;
        private readonly string _blobDomainUrl;
        private readonly string _frontEndDomain;
        private readonly ISendMailDynamicTemplateService _sendMailDynamicTemplateService;
        private string backOffice => "Bộ phận quản lý nhân sự";
        private string boardOfDirector => "Ban giám đốc";
        private string needUrgently => "Cần gấp";
        private string normally => "Bình thường";

        public PurchaseRequestService(
            IUnitOfWork unitOfWork,
            IBlobService blobService,
            IConfiguration configuration,
            ISendMailDynamicTemplateService sendMailDynamicTemplateService
            )
        {
            _unitOfWork = unitOfWork;
            _blobService = blobService;
            _blobDomainUrl = configuration["BlobDomainUrl"]!;
            _frontEndDomain = configuration["FrontEndDomain"]!;
            _sendMailDynamicTemplateService = sendMailDynamicTemplateService;
        }

        #region Private Method
        private bool IsAllowUpdateByStatus(EPurchaseRequestStatus status)
        {
            switch (status)
            {
                case EPurchaseRequestStatus.Pending:
                case EPurchaseRequestStatus.ManagerUpdateRequest:
                case EPurchaseRequestStatus.HrUpdateRequest:
                    return true;
            }
            return false;
        }

        private int FindStatus(PurchaseRequestPermissionModel permission, int userId, int reviewUserId)
        {
            if (userId == reviewUserId)
            {
                if (permission.IsManager)
                {
                    return (int)EPurchaseRequestStatus.ManagerApproved;
                }

                else if (permission.IsHr)
                {
                    return (int)EPurchaseRequestStatus.ManagerApproved;
                    //return (int)EPurchaseRequestStatus.HrApproved;
                }

                else if (permission.IsAccountant)
                {
                    return (int)EPurchaseRequestStatus.ManagerApproved;
                    //return (int)EPurchaseRequestStatus.AccountantApproved;
                }

                else if (permission.IsDirector)
                {
                    return (int)EPurchaseRequestStatus.ManagerApproved;
                    //return (int)EPurchaseRequestStatus.DirectorApproved;
                }
            }

            return (int)EPurchaseRequestStatus.Pending;
        }

        private bool IsAllowUpdateRequestOrRejectByStatus(EPurchaseRequestStatus status)
        {
            switch (status)
            {
                case EPurchaseRequestStatus.Pending:
                case EPurchaseRequestStatus.ManagerUpdateRequest:
                case EPurchaseRequestStatus.HrUpdateRequest:
                case EPurchaseRequestStatus.ManagerApproved:

                // Cheat cho bug #48
                case EPurchaseRequestStatus.AccountantUpdateRequest:
                case EPurchaseRequestStatus.DirectorUpdateRequest:
                    return true;
            }
            return false;
        }

        private bool IsAllowMultipleReview(EPurchaseRequestStatus status, bool isDirector)
        {
            if (isDirector)
            {
                if (status == EPurchaseRequestStatus.AccountantApproved)
                {
                    return true;
                }
            }
            else
            {
                switch (status)
                {
                    case EPurchaseRequestStatus.ManagerApproved:
                        return true;
                }
            }
            return false;
        }

        private int CalculateRowSpan(AccountantLineItemSendEmailModel lineItem)
        {
            return lineItem.AccountantSubProductEmailModels == null ? 0 : lineItem.AccountantSubProductEmailModels.Count;
        }
        #endregion

        public async Task<PagingResponseModel<PurchaseRequestPagingResponse>> GetAllWithPagingAsync(PurchaseRequestPagingModel request, int userId)
        {
            var recordsRaw = await _unitOfWork.PurchaseRequestRepository.GetAllWithPagingAsync(request, userId);

            var totalRecords = recordsRaw.FirstOrDefault();
            if (totalRecords != null)
            {
                recordsRaw.Remove(totalRecords);
            }

            var records = recordsRaw.Select(o => new PurchaseRequestPagingResponse
            {
                Id = o.Id,
                CreatedDate = o.CreatedDate,
                Name = o.Name,
                Quantity = o.Quantity,
                DepartmentName = o.DepartmentName,
                ProjectName = o.ProjectName,
                ReviewUserId = o.ReviewUserId,
                ReviewUserName = o.ReviewUserName,
                ReviewStatus = o.ReviewStatus,
                IsUrgent = o.IsUrgent,
                EstimateDate = o.EstimateDate,
                TotalRecord = o.TotalRecord
            }).ToList();

            var res = new PagingResponseModel<PurchaseRequestPagingResponse>
            {
                Items = records,
                TotalRecord = totalRecords?.TotalRecord ?? 0
            };

            return res;
        }

        public async Task<CombineResponseModel<PurchaseRequestResponseModel>> GetByUserIdAndPrIdAsync(int userId, int id)
        {
            var res = new CombineResponseModel<PurchaseRequestResponseModel>();
            var purchaseRequest = await _unitOfWork.PurchaseRequestRepository.GetByIdAsync(id);
            if (purchaseRequest == null)
            {
                res.ErrorMessage = "Đơn đặt mua hàng không tồn tại";
                return res;
            }

            if (purchaseRequest.UserId != userId)
            {
                res.ErrorMessage = "Đơn đặt mua hàng này không thuộc về bạn";
                return res;
            }

            var productIds = purchaseRequest.PurchaseRequestLineItems.Select(o => o.ProductId).Distinct().ToList();
            var subProductModels = await _unitOfWork.ProductRepository.GetSubProductsByProductIdsAsync(productIds);

            var isAllowUpdate = IsAllowUpdateByStatus((EPurchaseRequestStatus)purchaseRequest.ReviewStatus);
            var isHasPoOrEb = purchaseRequest.PurchaseRequestLineItems.Any(o => o.ExportBillLineItems.Any() || o.PoprlineItems.Any());
            var data = new PurchaseRequestResponseModel
            {
                Id = purchaseRequest.Id,
                UserName = purchaseRequest.User.FullName ?? "",
                JobName = purchaseRequest.User.JobTitle ?? "",
                DepartmentId = purchaseRequest.DepartmentId ?? 0,
                DepartmentName = purchaseRequest.Department?.Name != null ? purchaseRequest.Department.Name : (purchaseRequest.User.Department?.Name ?? ""),
                PurchaseRequestName = purchaseRequest.Name,
                ReviewUserId = purchaseRequest.ReviewUserId,
                ProjectId = purchaseRequest.ProjectId.GetValueOrDefault(),
                IsUrgent = purchaseRequest.IsUrgent,
                EstimateDate = purchaseRequest.EstimateDate,
                Note = purchaseRequest.Note ?? "",
                ReviewStatus = purchaseRequest.ReviewStatus,
                ManagerComment = purchaseRequest.ManagerComment ?? "",
                FirstHrComment = purchaseRequest.FirstHrComment ?? "",
                RejectReason = purchaseRequest.RejectReason ?? "",
                FileUrls = purchaseRequest.PurchaseRequestAttachments.Select(o => $"{_blobDomainUrl}/{o.FileUrl}").ToList(),
                IsAllowUpdate = isAllowUpdate,
                IsReviewByMySelf = purchaseRequest.ReviewUserId == userId,
                IsHasPoOrEb = isHasPoOrEb,
                LineItems = purchaseRequest.PurchaseRequestLineItems.GroupBy(y => new
                {
                    ProductCategoryId = y.Product.ProductCategoryId,
                    ProductCategoryName = y.Product.ProductCategory.Name,
                    ProductId = y.ProductId,
                    ProductName = y.Product.Name,
                    ProductDescription = y.Product.Description ?? "",
                    Quantity = y.Quantity,
                    ShoppingUrl = y.ShoppingUrl,
                    Note = y.Note
                }).Select(o => new PurchaseRequestLineItemResponseModel
                {
                    ProductCategoryId = o.Key.ProductCategoryId,
                    ProductCategoryName = o.Key.ProductCategoryName,
                    ProductId = o.Key.ProductId,
                    ProductName = o.Key.ProductName,
                    ProductDescription = o.Key.ProductDescription,
                    Quantity = o.Key.Quantity,
                    ShoppingUrl = o.Key.ShoppingUrl ?? "",
                    Note = o.Key.Note ?? "",
                    SubProductModels = subProductModels.Where(t => t.ProductId == o.Key.ProductId).Select(z => new PurchaseRequestSubProductModel
                    {
                        SubProductId = z.SubProductId,
                        SubProductName = z.SubProductName,
                        SubProductDescription = z.SubProductDescription,
                        KitQuantity = z.KitQuantity,
                    }).OrderBy(o => o.SubProductName).ToList()
                }).OrderBy(o => o.ProductName).ToList()
            };

            res.Status = true;
            res.Data = data;
            return res;
        }

        public async Task<CombineResponseModel<int>> PrepareCreateAsync(UserDtoModel user, PurchaseRequestPermissionModel permission, PurchaseRequestCreateModel request)
        {
            var res = new CombineResponseModel<int>();
            var now = DateTime.UtcNow.UTCToIct();
            if (request.EstimateDate.HasValue && request.EstimateDate.Value.Date < now.Date)
            {
                res.ErrorMessage = "Thời gian nhận hàng không hợp lệ";
                return res;
            }

            if (request.LineItems.Count == 0)
            {
                res.ErrorMessage = "Chưa có sản phẩm nào!";
                return res;
            }

            if (request.LineItems.Any(o => o.Quantity <= 0))
            {
                res.ErrorMessage = "Số lượng sản phầm không hợp lệ";
                return res;
            }

            var department = await _unitOfWork.DepartmentRepository.GetByIdAsync(request.DepartmentId);
            if (department == null)
            {
                res.ErrorMessage = "Bộ phận không tồn tại";
                return res;
            }

            var reviewer = await _unitOfWork.UserInternalRepository.GetByIdAsync(request.ReviewUserId);
            if (reviewer == null || reviewer.IsDeleted || reviewer.HasLeft)
            {
                res.ErrorMessage = "Quản lý không tồn tại";
                return res;
            }

            if (request.ProjectId.HasValue && request.ProjectId.Value != 0)
            {
                var validateMemberModel = await _unitOfWork.PurchaseRequestRepository.IsValidMemberAsync(request.ProjectId.GetValueOrDefault(), user.Id);
                if (validateMemberModel == null)
                {
                    res.ErrorMessage = "Dự án không hợp lệ";
                    return res;
                }
            }

            var requestProductIds = request.LineItems.GroupBy(z => z.ProductId).Select(o => new ValidateProductModel
            {
                ProductId = o.Key,
                SubProductIds = o.Where(t => t.SubProductId.HasValue).Select(x => x.SubProductId!.Value).Distinct().ToList()
            }).ToList();

            foreach (var requestProduct in requestProductIds)
            {
                var product = await _unitOfWork.ProductRepository.GetByIdAsync(requestProduct.ProductId);
                if (product == null)
                {
                    res.ErrorMessage = "Tồn tại sản phẩm không hợp lệ";
                    return res;
                }

                if (requestProduct.SubProductIds != null && requestProduct.SubProductIds.Any())
                {
                    var subProducts = await _unitOfWork.ProductRepository.GetByIdsAsync(requestProduct.SubProductIds);
                    if (subProducts.Count != requestProduct.SubProductIds.Count)
                    {
                        res.ErrorMessage = "Tồn tại sản phẩm trong bộ không hợp lệ";
                        return res;
                    }
                }
            }

            var newAttachments = new List<string>();
            if (request.Files != null)
            {
                foreach (var file in request.Files)
                {
                    using (var ms = new MemoryStream())
                    {
                        await file.CopyToAsync(ms);
                        var fileBytes = ms.ToArray();
                        var filename = $"{now.Year}/{now.Month}/{Guid.NewGuid().ToString()}/{CommonHelper.RemoveDiacritics(file.FileName)}";
                        var imageUrl = await _blobService.UploadAsync(fileBytes, BlobContainerName.PurchaseRequest, filename, file.ContentType);
                        if (imageUrl == null)
                        {
                            res.ErrorMessage = "Tải hình lên lỗi";
                            return res;
                        }
                        newAttachments.Add(imageUrl.RelativeUrl!);
                    }
                }
            }

            var newPurchaseRequest = new PurchaseRequest
            {
                UserId = user.Id,
                Name = request.Name,
                ReviewStatus = FindStatus(permission, user.Id, reviewer.Id),
                ReviewUserId = reviewer.Id,
                ProjectId = request.ProjectId,
                IsUrgent = request.IsUrgent,
                EstimateDate = request.EstimateDate,
                Note = request.Note,
                CreatedDate = DateTime.UtcNow.UTCToIct(),
                DepartmentId = request.DepartmentId,
                PurchaseRequestAttachments = newAttachments.Select(o => new PurchaseRequestAttachment
                {
                    FileUrl = o,
                    CreatedDate = DateTime.UtcNow.UTCToIct()
                }).ToList(),
                PurchaseRequestLineItems = request.LineItems.Select(o => new PurchaseRequestLineItem
                {
                    ProductId = o.ProductId,
                    SubProductId = o.SubProductId,
                    Quantity = o.Quantity,
                    ShoppingUrl = o.ShoppingUrl,
                    Note = o.Note,
                    CreatedDate = DateTime.UtcNow.UTCToIct()
                }).ToList(),
            };

            await _unitOfWork.PurchaseRequestRepository.CreateAsync(newPurchaseRequest);
            await _unitOfWork.SaveChangesAsync();

            res.Status = true;
            res.Data = newPurchaseRequest.Id;
            return res;
        }

        public async Task<CombineResponseModel<int>> PrepareUpdateAsync(int id, UserDtoModel user, PurchaseRequestPermissionModel permission, PurchaseRequestUpdateModel request)
        {
            var res = new CombineResponseModel<int>();
            var now = DateTime.UtcNow.UTCToIct();
            if (request.EstimateDate.HasValue && request.EstimateDate.Value.Date < now.Date)
            {
                res.ErrorMessage = "Thời gian nhận hàng không hợp lệ";
                return res;
            }

            if (request.LineItems.Count == 0)
            {
                res.ErrorMessage = "Chưa có sản phẩm nào!";
                return res;
            }

            if (request.LineItems.Any(o => o.Quantity <= 0))
            {
                res.ErrorMessage = "Số lượng sản phầm không hợp lệ";
                return res;
            }

            var purchaseRequest = await _unitOfWork.PurchaseRequestRepository.GetByIdAsync(id);
            if (purchaseRequest == null)
            {
                res.ErrorMessage = "Đơn yêu cầu đặt hàng không tồn tại";
                return res;
            }

            if (purchaseRequest.UserId != user.Id)
            {
                res.ErrorMessage = "Đơn này không thuộc về bạn";
                return res;
            }

            if (!IsAllowUpdateByStatus((EPurchaseRequestStatus)purchaseRequest.ReviewStatus) && !permission.IsManager)
            {
                res.ErrorMessage = "Tình trạng đơn hàng không cho phép cập nhật";
                return res;
            }

            var reviewer = await _unitOfWork.UserInternalRepository.GetByIdAsync(request.ReviewUserId);
            if (reviewer == null || reviewer.IsDeleted || reviewer.HasLeft)
            {
                res.ErrorMessage = "Quản lý không tồn tại";
                return res;
            }

            if (request.ProjectId.HasValue && request.ProjectId.Value != 0)
            {
                var validateMemberModel = await _unitOfWork.PurchaseRequestRepository.IsValidMemberAsync(request.ProjectId.GetValueOrDefault(), user.Id);
                if (validateMemberModel == null)
                {
                    res.ErrorMessage = "Dự án không hợp lệ";
                    return res;
                }
            }

            var department = await _unitOfWork.DepartmentRepository.GetByIdAsync(request.DepartmentId);
            if (department == null)
            {
                res.ErrorMessage = "Bộ phận không tồn tại";
                return res;
            }

            var requestProductIds = request.LineItems.GroupBy(z => z.ProductId).Select(o => new ValidateProductModel
            {
                ProductId = o.Key,
                SubProductIds = o.Where(t => t.SubProductId.HasValue && t.SubProductId.GetValueOrDefault() != 0).Select(x => x.SubProductId!.Value).Distinct().ToList()
            }).ToList();

            foreach (var requestProduct in requestProductIds)
            {
                var product = await _unitOfWork.ProductRepository.GetByIdAsync(requestProduct.ProductId);
                if (product == null)
                {
                    res.ErrorMessage = "Tồn tại sản phẩm không hợp lệ";
                    return res;
                }

                if (requestProduct.SubProductIds != null && requestProduct.SubProductIds.Any())
                {
                    var subProducts = await _unitOfWork.ProductRepository.GetByIdsAsync(requestProduct.SubProductIds);
                    if (subProducts.Count != requestProduct.SubProductIds.Count)
                    {
                        res.ErrorMessage = "Tồn tại sản phẩm trong bộ không hợp lệ";
                        return res;
                    }
                }
            }

            // Khi PurchaseRequest đã có PO (mua hàng) hoặc PE (đơn xuất kho) thì ko dc cập nhật
            if (purchaseRequest.PurchaseRequestLineItems.Any(o => o.ExportBillLineItems.Any() || o.PoprlineItems.Any()))
            {
                res.ErrorMessage = "Đơn yêu cầu đã được duyệt! Không thể cập nhật";
                return res;
            }

            var newAttachments = new List<string>();
            if (request.Files != null)
            {
                foreach (var file in request.Files)
                {
                    using (var ms = new MemoryStream())
                    {
                        await file.CopyToAsync(ms);
                        var fileBytes = ms.ToArray();
                        var filename = $"{now.Year}/{now.Month}/{Guid.NewGuid().ToString()}/{CommonHelper.RemoveDiacritics(file.FileName)}";
                        var imageUrl = await _blobService.UploadAsync(fileBytes, BlobContainerName.PurchaseRequest, filename, file.ContentType);
                        if (imageUrl == null)
                        {
                            res.ErrorMessage = "Tải hình lên lỗi";
                            return res;
                        }
                        newAttachments.Add(imageUrl.RelativeUrl!);
                    }
                }
            }

            var removeAttachments = new List<PurchaseRequestAttachment>();
            if (string.IsNullOrEmpty(request.StringUrls))
            {
                removeAttachments.AddRange(purchaseRequest.PurchaseRequestAttachments);
            }
            else
            {
                var stringFileUrls = request.StringUrls.Split(",").ToList();
                if (stringFileUrls != null && stringFileUrls.Any())
                {
                    removeAttachments.AddRange(purchaseRequest.PurchaseRequestAttachments.Where(o => !stringFileUrls.Any(y => y.EndsWith(o.FileUrl))).ToList());
                }
            }

            purchaseRequest.Name = request.Name;
            purchaseRequest.ReviewUserId = request.ReviewUserId;
            purchaseRequest.ProjectId = request.ProjectId;
            purchaseRequest.IsUrgent = request.IsUrgent;
            purchaseRequest.EstimateDate = request.EstimateDate;
            purchaseRequest.Note = request.Note;
            purchaseRequest.ReviewStatus = FindStatus(permission, user.Id, reviewer.Id);
            purchaseRequest.DepartmentId = request.DepartmentId;
            purchaseRequest.UpdatedDate = DateTime.UtcNow.UTCToIct();

            if (removeAttachments.Any())
            {
                await _unitOfWork.PurchaseRequestAttachmentRepository.DeleteRangeAsync(removeAttachments);
            }
            if (newAttachments.Any())
            {
                await _unitOfWork.PurchaseRequestAttachmentRepository.CreateRangeAsync(newAttachments.Select(o => new PurchaseRequestAttachment
                {
                    PurchaseRequestId = purchaseRequest.Id,
                    FileUrl = o,
                    CreatedDate = DateTime.UtcNow.UTCToIct()
                }).ToList());
            }

            if (purchaseRequest.PurchaseRequestLineItems.Any())
            {
                await _unitOfWork.PurchaseRequestLineItemRepository.DeleteRangeAsync(purchaseRequest.PurchaseRequestLineItems.ToList());
            }

            var newLineItems = request.LineItems.Select(o => new PurchaseRequestLineItem
            {
                PurchaseRequestId = purchaseRequest.Id,
                ProductId = o.ProductId,
                SubProductId = o.SubProductId == 0 ? null : o.SubProductId,
                Quantity = o.Quantity,
                ShoppingUrl = o.ShoppingUrl,
                Note = o.Note,
                CreatedDate = DateTime.UtcNow.UTCToIct()
            }).ToList();

            await _unitOfWork.PurchaseRequestLineItemRepository.CreateRangeAsync(newLineItems);
            await _unitOfWork.PurchaseRequestRepository.UpdateAsync(purchaseRequest);
            await _unitOfWork.SaveChangesAsync();

            res.Status = true;
            res.Data = id;
            return res;
        }

        public async Task<CombineResponseModel<int>> PrepareCancelledAsync(int id, int userId)
        {
            var res = new CombineResponseModel<int>();
            var purchaseRequest = await _unitOfWork.PurchaseRequestRepository.GetByIdAsync(id);
            if (purchaseRequest == null)
            {
                res.ErrorMessage = "Đơn đặt hàng không tồn tại";
                return res;
            }

            if (purchaseRequest.UserId != userId)
            {
                res.ErrorMessage = "Đơn đặt hàng không thuộc về bạn";
                return res;
            }

            if (purchaseRequest.ReviewStatus != (int)EPurchaseRequestStatus.Pending)
            {
                res.ErrorMessage = "Tình trạng đơn hàng không cho phép hủy";
                return res;
            }

            purchaseRequest.ReviewStatus = (int)EPurchaseRequestStatus.Cancelled;
            purchaseRequest.UpdatedDate = DateTime.UtcNow.UTCToIct();
            await _unitOfWork.PurchaseRequestRepository.UpdateAsync(purchaseRequest);
            await _unitOfWork.SaveChangesAsync();

            res.Status = true;
            res.Data = purchaseRequest.Id;
            return res;

        }

        public async Task SendEmailAsync(int purchaseRequestId)
        {
            var purchaseRequest = await _unitOfWork.PurchaseRequestRepository.GetByIdAsync(purchaseRequestId);
            if (purchaseRequest != null)
            {
                string dateString = purchaseRequest.CreatedDate.ToString("dd/MM/yyyy");
                var lineItems = string.Empty;
                var productIds = purchaseRequest.PurchaseRequestLineItems.Select(o => o.ProductId).Distinct().ToList();
                var subProductModels = await _unitOfWork.ProductRepository.GetSubProductsByProductIdsAsync(productIds);
                var lineItemModels = purchaseRequest.PurchaseRequestLineItems.GroupBy(o => new
                {
                    ProductCategoryName = o.Product.ProductCategory.Name,
                    ProductName = o.Product.Name,
                    Quantity = o.Quantity,
                    ProductDescription = o.Product.Description,
                    ProductId = o.ProductId
                }).Select(y => new PurchaseRequestLineItemEmailModel
                {
                    ProductCategoryName = y.Key.ProductCategoryName,
                    ProductName = y.Key.ProductName,
                    ProductDescription = y.Key.ProductDescription,
                    Quantity = y.Key.Quantity,
                    ProductId = y.Key.ProductId,
                    SubProductEmailModels = subProductModels.Where(z => z.ProductId == y.Key.ProductId).Select(t => new PurchaseRequestSubProductEmailModel
                    {

                        SubProductName = t.SubProductName,
                        SubProductDescription = t.SubProductDescription,
                        Quantity = t.KitQuantity,
                    }).ToList()
                }).ToList();
                foreach (var lineItem in lineItemModels)
                {
                    if (lineItem.SubProductEmailModels != null && lineItem.SubProductEmailModels.Any())
                    {
                        var index = 0;
                        foreach (var item in lineItem.SubProductEmailModels)
                        {
                            lineItems += $@"<tr style='border-collapse: collapse;border: 1px solid black'>";
                            if (index == 0)
                            {
                                lineItems += $@"<td style='border-collapse: collapse;border: 1px solid black' rowSpan='{lineItem.SubProductEmailModels.Count}'>{lineItem.ProductCategoryName}</td>
                                                <td style='border-collapse: collapse;border: 1px solid black' rowSpan='{lineItem.SubProductEmailModels.Count}'>{lineItem.ProductName}</td>
                                                <td style='text-align: center; border-collapse: collapse;border: 1px solid black' rowSpan='{lineItem.SubProductEmailModels.Count}'>{lineItem.Quantity}</td>";
                            }
                            lineItems += $@"<td style='border-collapse: collapse;border: 1px solid black'>{item.SubProductName}</td>
                                            <td style='border-collapse: collapse;border: 1px solid black'>{item.SubProductDescription}</td>
                                        </tr>";
                            index++;
                        }
                    }
                    else
                    {
                        lineItems += $@"<tr style='border-collapse: collapse;border: 1px solid black'>
                                        <td style='border-collapse: collapse;border: 1px solid black'>{lineItem.ProductCategoryName}</td>
                                        <td style='border-collapse: collapse;border: 1px solid black'>{lineItem.ProductName}</td>
                                        <td style='text-align: center;border-collapse: collapse;border: 1px solid black'>{lineItem.Quantity}</td>
                                        <td></td>
                                        <td style='border-collapse: collapse;border: 1px solid black'>{lineItem.ProductDescription}</td>
                                    </tr>";
                    }
                }

                // Tự động duyệt (khi chọn người duyệt là chính mình) => gửi email cho hr 1
                if (purchaseRequest.ReviewStatus == (int)EPurchaseRequestStatus.ManagerApproved)
                {
                    var hrValue = await _unitOfWork.GlobalConfigurationRepository.GetByNameAsync(Constant.FirstHrEmail);
                    if (hrValue != null && !string.IsNullOrEmpty(hrValue.Value))
                    {
                        var hrEmails = hrValue.Value.Split(',').Where(email => !string.IsNullOrEmpty(email)).Select(o => o.Trim()).ToList();
                        var emailModelToHR = new PurchaseRequestSendMailModel()
                        {
                            Reviewer = purchaseRequest.ReviewUser.FullName!,
                            PurchaseRequestName = purchaseRequest.Name,
                            ProjectName = purchaseRequest.Project?.Name ?? "",
                            CreatedDate = purchaseRequest.CreatedDate.ToString("dd/MM/yyyy"),
                            Register = purchaseRequest.User.FullName!,
                            ReviewLink = _frontEndDomain + Constant.HrReviewPurchaseRequestPath,
                            EstimateDate = purchaseRequest.EstimateDate.HasValue ? purchaseRequest.EstimateDate.Value.ToString("dd/MM/yyyy") : "",
                            IsUrgent = purchaseRequest.IsUrgent ? needUrgently : normally,
                            LineItems = lineItems,
                            Receiver = backOffice
                        };

                        var objSendMailToHR = new ObjSendMail()
                        {
                            FileName = "PurchaseRequestManagerToHrTemplate.html",
                            Mail_To = hrEmails,
                            Title = "[Purchase Request] Đơn yêu cầu cấp/mua hàng của nhân viên " + purchaseRequest.User.FullName + " ngày " + purchaseRequest.CreatedDate.ToString("dd/MM/yyyy"),
                            Mail_cc = [],
                            JsonObject = JsonConvert.SerializeObject(emailModelToHR)
                        };

                        await _sendMailDynamicTemplateService.SendMailAsync(objSendMailToHR);
                    }
                }

                // Chọn người duyệt là người khác => gửi email cho người duyệt đó
                else
                {
                    var emailModel = new PurchaseRequestSendMailModel()
                    {
                        Reviewer = purchaseRequest.ReviewUser.FullName!,
                        PurchaseRequestName = purchaseRequest.Name,
                        ProjectName = purchaseRequest.Project?.Name ?? "",
                        CreatedDate = dateString,
                        Register = purchaseRequest.User.FullName!,
                        ReviewLink = _frontEndDomain + Constant.ManagerReviewPurchaseRequestPath,
                        EstimateDate = purchaseRequest.EstimateDate.HasValue ? purchaseRequest.EstimateDate.Value.ToString("dd/MM/yyyy") : "",
                        IsUrgent = purchaseRequest.IsUrgent ? needUrgently : normally,
                        LineItems = lineItems
                    };

                    var objSendMail = new ObjSendMail()
                    {
                        FileName = "PurchaseRequestUserToManagerTemplate.html",
                        Mail_To = [purchaseRequest.ReviewUser.Email!],
                        Title = "[Purchase Request] Yêu cầu duyệt đơn yêu cầu cấp/mua hàng của " + purchaseRequest.User.FullName + " ngày " + dateString,
                        Mail_cc = new List<string>(),
                        JsonObject = JsonConvert.SerializeObject(emailModel)
                    };
                    await _sendMailDynamicTemplateService.SendMailAsync(objSendMail);
                }
            }
        }

        public async Task<CombineResponseModel<int>> PrepareUpdateRequestAsync(int id, UserDtoModel user, PurchaseRequestManagerUpdateRequestModel request)
        {
            var response = new CombineResponseModel<int>();

            var purchaseRequest = await _unitOfWork.PurchaseRequestRepository.GetByIdAsync(id);
            if (purchaseRequest == null)
            {
                response.ErrorMessage = "Đơn yêu cầu đặt hàng không tồn tại";
                return response;
            }

            if (purchaseRequest.ReviewUserId != user.Id)
            {
                response.ErrorMessage = "Người duyệt không hợp lệ";
                return response;
            }

            if (purchaseRequest.ReviewStatus != (int)EPurchaseRequestStatus.Pending)
            {
                response.ErrorMessage = "Tình trạng đơn hàng không cho phép yêu cầu cập nhật";
                return response;
            }

            if (string.IsNullOrEmpty(request.ManagerComment))
            {
                response.ErrorMessage = "Vui lòng nhập yêu cầu cập nhật";
                return response;
            }

            purchaseRequest.ReviewStatus = (int)EPurchaseRequestStatus.ManagerUpdateRequest;
            purchaseRequest.UpdatedDate = DateTime.UtcNow.UTCToIct();
            purchaseRequest.ManagerComment = request.ManagerComment.Trim();

            await _unitOfWork.PurchaseRequestRepository.UpdateAsync(purchaseRequest);
            await _unitOfWork.SaveChangesAsync();

            response.Status = true;
            response.Data = id;
            return response;
        }

        public async Task<CombineResponseModel<int>> PrepareReviewAsync(int id, UserDtoModel user, PurchaseRequestManagerReviewModel request)
        {
            var response = new CombineResponseModel<int>();

            if (request.ReviewStatus != EPurchaseRequestStatus.ManagerRejected &&
                request.ReviewStatus != EPurchaseRequestStatus.ManagerApproved)
            {
                response.ErrorMessage = "Trạng thái duyệt không hợp lệ";
                return response;
            }

            var purchaseRequest = await _unitOfWork.PurchaseRequestRepository.GetByIdAsync(id);
            if (purchaseRequest == null)
            {
                response.ErrorMessage = "Đơn yêu cầu cấp/mua hàng không tồn tại";
                return response;
            }

            if (purchaseRequest.ReviewUserId != user.Id)
            {
                response.ErrorMessage = "Người duyệt không hợp lệ";
                return response;
            }

            if (purchaseRequest.ReviewStatus != (int)EPurchaseRequestStatus.Pending)
            {
                response.ErrorMessage = "Đơn yêu cầu đặt hàng đã được xử lý";
                return response;
            }

            if (purchaseRequest.ReviewStatus == (int)EPurchaseRequestStatus.ManagerRejected && string.IsNullOrEmpty(request.RejectReason))
            {
                response.ErrorMessage = "Vui lòng nhập nội dung từ chối";
                return response;
            }

            if (request.ReviewStatus == EPurchaseRequestStatus.ManagerRejected)
            {
                purchaseRequest.RejectReason = request.RejectReason;
            }

            purchaseRequest.ReviewStatus = (int)request.ReviewStatus;
            purchaseRequest.UpdatedDate = DateTime.UtcNow.UTCToIct();

            await _unitOfWork.PurchaseRequestRepository.UpdateAsync(purchaseRequest);
            await _unitOfWork.SaveChangesAsync();

            response.Status = true;
            response.Data = id;

            return response;
        }

        public async Task<CombineResponseModel<PurchaseRequestResponseModel>> GetByManagerIdAndPrIdAsync(int managerId, int id)
        {
            var response = new CombineResponseModel<PurchaseRequestResponseModel>();
            var purchaseRequest = await _unitOfWork.PurchaseRequestRepository.GetByIdAsync(id);
            if (purchaseRequest == null)
            {
                response.ErrorMessage = "Đơn đặt đặt hàng không tồn tại";
                return response;
            }

            if (purchaseRequest.ReviewUserId != managerId)
            {
                response.ErrorMessage = "Bạn không phải là người duyệt đơn yêu cầu đặt đặt hàng này";
                return response;
            }

            var productIds = purchaseRequest.PurchaseRequestLineItems.Select(o => o.ProductId).Distinct().ToList();
            var subProductModels = await _unitOfWork.ProductRepository.GetSubProductsByProductIdsAsync(productIds);

            var data = new PurchaseRequestManagerResponseModel
            {
                Id = purchaseRequest.Id,
                UserName = purchaseRequest.User.FullName ?? "",
                JobName = purchaseRequest.User.JobTitle ?? "",
                DepartmentName = purchaseRequest.Department?.Name != null ? purchaseRequest.Department.Name : purchaseRequest.User.Department?.Name ?? "",
                PurchaseRequestName = purchaseRequest.Name,
                ReviewUserId = purchaseRequest.ReviewUserId,
                ProjectId = purchaseRequest.ProjectId.GetValueOrDefault(),
                ProjectName = purchaseRequest.Project?.Name ?? "",
                IsUrgent = purchaseRequest.IsUrgent,
                EstimateDate = purchaseRequest.EstimateDate,
                Note = purchaseRequest.Note ?? "",
                ReviewStatus = purchaseRequest.ReviewStatus,
                ManagerComment = purchaseRequest.ManagerComment ?? "",
                FirstHrComment = purchaseRequest.FirstHrComment ?? "",
                SecondHrComment = purchaseRequest.SecondHrComment ?? "",
                DirectorComment = purchaseRequest.DirectorComment ?? "",
                RejectReason = purchaseRequest.RejectReason ?? "",
                FileUrls = purchaseRequest.PurchaseRequestAttachments.Select(o => $"{_blobDomainUrl}/{o.FileUrl}").ToList(),
                LineItems = purchaseRequest.PurchaseRequestLineItems.GroupBy(y => new
                {
                    ProductCategoryId = y.Product.ProductCategoryId,
                    ProductCategoryName = y.Product.ProductCategory.Name,
                    ProductId = y.ProductId,
                    ProductName = y.Product.Name,
                    ProductDescription = y.Product.Description ?? "",
                    Quantity = y.Quantity,
                    ShoppingUrl = y.ShoppingUrl,
                    Note = y.Note
                }).Select(o => new PurchaseRequestLineItemResponseModel
                {
                    ProductCategoryId = o.Key.ProductCategoryId,
                    ProductCategoryName = o.Key.ProductCategoryName,
                    ProductId = o.Key.ProductId,
                    ProductName = o.Key.ProductName,
                    ProductDescription = o.Key.ProductDescription,
                    Quantity = o.Key.Quantity,
                    ShoppingUrl = o.Key.ShoppingUrl ?? "",
                    Note = o.Key.Note ?? "",
                    SubProductModels = subProductModels.Where(t => t.ProductId == o.Key.ProductId).Select(z => new PurchaseRequestSubProductModel
                    {
                        SubProductId = z.SubProductId,
                        SubProductName = z.SubProductName,
                        SubProductDescription = z.SubProductDescription,
                        KitQuantity = z.KitQuantity,
                    }).ToList()
                }).ToList()
            };

            response.Status = true;
            response.Data = data;

            return response;
        }

        public async Task<PagingResponseModel<PurchaseRequestManagerPagingResponse>> GetAllWithPagingByManagerAsync(int managerId, PurchaseRequestManagerPagingRequest request)
        {
            var recordsRaw = await _unitOfWork.PurchaseRequestRepository.GetAllWithPagingByManagerAsync(managerId, request);

            var totalRecords = recordsRaw.FirstOrDefault();
            if (totalRecords != null)
            {
                recordsRaw.Remove(totalRecords);
            }

            var records = recordsRaw.Select(o => new PurchaseRequestManagerPagingResponse
            {
                Id = o.Id,
                CreatedDate = o.CreatedDate,
                Name = o.Name,
                Quantity = o.Quantity,
                DepartmentName = o.DepartmentName,
                ProjectName = o.ProjectName,
                RegisterId = o.RegisterId,
                RegisterName = o.RegisterName,
                ReviewStatus = o.ReviewStatus,
                ReviewStatusName = CommonHelper.GetDescription((EPurchaseRequestStatus)o.ReviewStatus),
                IsUrgent = o.IsUrgent,
                EstimateDate = o.EstimateDate,
                TotalRecord = o.TotalRecord
            }).ToList();

            var response = new PagingResponseModel<PurchaseRequestManagerPagingResponse>
            {
                Items = records,
                TotalRecord = totalRecords?.TotalRecord ?? 0
            };

            return response;
        }

        public async Task SendEmailFromManagerAsync(int purchaseRequestId)
        {
            var purchaseRequest = await _unitOfWork.PurchaseRequestRepository.GetByIdAsync(purchaseRequestId);
            if (purchaseRequest == null)
            {
                return;
            }

            var productIds = purchaseRequest.PurchaseRequestLineItems.Select(o => o.ProductId).Distinct().ToList();
            var subProductModels = await _unitOfWork.ProductRepository.GetSubProductsByProductIdsAsync(productIds);

            var lineItems = string.Empty;
            var lineItemModels = purchaseRequest.PurchaseRequestLineItems.GroupBy(o => new
            {
                ProductCategoryName = o.Product.ProductCategory.Name,
                ProductName = o.Product.Name,
                Quantity = o.Quantity,
                ProductDescription = o.Product.Description,
                ProductId = o.ProductId
            }).Select(y => new PurchaseRequestLineItemEmailModel
            {
                ProductCategoryName = y.Key.ProductCategoryName,
                ProductName = y.Key.ProductName,
                ProductDescription = y.Key.ProductDescription,
                Quantity = y.Key.Quantity,
                ProductId = y.Key.ProductId,
                SubProductEmailModels = subProductModels.Where(z => z.ProductId == y.Key.ProductId).Select(t => new PurchaseRequestSubProductEmailModel
                {
                    SubProductName = t.SubProductName,
                    SubProductDescription = t.SubProductDescription,
                    Quantity = t.KitQuantity,
                }).ToList()
            }).ToList();

            foreach (var lineItem in lineItemModels)
            {
                if (lineItem.SubProductEmailModels != null && lineItem.SubProductEmailModels.Count != 0)
                {
                    var index = 0;
                    foreach (var item in lineItem.SubProductEmailModels)
                    {
                        lineItems += $@"<tr style='border-collapse: collapse;border: 1px solid black'>";
                        if (index == 0)
                        {
                            lineItems += $@"<td style='border-collapse: collapse;border: 1px solid black' rowSpan='{lineItem.SubProductEmailModels.Count}'>{lineItem.ProductCategoryName}</td>
                                                <td style='border-collapse: collapse;border: 1px solid black' rowSpan='{lineItem.SubProductEmailModels.Count}'>{lineItem.ProductName}</td>
                                                <td style='text-align: center;border-collapse: collapse;border: 1px solid black' rowSpan='{lineItem.SubProductEmailModels.Count}'>{lineItem.Quantity}</td>";
                        }
                        lineItems += $@"<td style='border-collapse: collapse;border: 1px solid black'>{item.SubProductName}</td>
                                                <td style='border-collapse: collapse;border: 1px solid black'>{item.SubProductDescription}</td>
                                            </tr>";
                        index++;
                    }
                }
                else
                {
                    lineItems += $@"<tr style='border-collapse: collapse;border: 1px solid black'>
                                        <td style='border-collapse: collapse;border: 1px solid black'>{lineItem.ProductCategoryName}</td>
                                        <td style='border-collapse: collapse;border: 1px solid black'>{lineItem.ProductName}</td>
                                        <td style='text-align: center;border-collapse: collapse;border: 1px solid black'>{lineItem.Quantity}</td>
                                        <td></td>
                                        <td style='border-collapse: collapse;border: 1px solid black'>{lineItem.ProductDescription}</td>
                                    </tr>";
                }
            }

            if (purchaseRequest.ReviewStatus == (int)EPurchaseRequestStatus.ManagerApproved)
            {
                var hrValue = await _unitOfWork.GlobalConfigurationRepository.GetByNameAsync(Constant.FirstHrEmail);
                if (hrValue != null && !string.IsNullOrEmpty(hrValue.Value))
                {
                    var hrEmails = hrValue.Value.Split(',').Where(email => !string.IsNullOrEmpty(email)).Select(o => o.Trim()).ToList();
                    var emailModelToHR = new PurchaseRequestSendMailModel()
                    {
                        Reviewer = purchaseRequest.ReviewUser.FullName!,
                        PurchaseRequestName = purchaseRequest.Name,
                        ProjectName = purchaseRequest.Project?.Name ?? "",
                        CreatedDate = purchaseRequest.CreatedDate.ToString("dd/MM/yyyy"),
                        Register = purchaseRequest.User.FullName!,
                        ReviewLink = _frontEndDomain + Constant.HrReviewPurchaseRequestPath,
                        EstimateDate = purchaseRequest.EstimateDate.HasValue ? purchaseRequest.EstimateDate.Value.ToString("dd/MM/yyyy") : "",
                        IsUrgent = purchaseRequest.IsUrgent ? needUrgently : normally,
                        LineItems = lineItems,
                        Receiver = backOffice
                    };

                    var objSendMailToHR = new ObjSendMail()
                    {
                        FileName = "PurchaseRequestManagerToHrTemplate.html",
                        Mail_To = hrEmails,
                        Title = "[Purchase Request] Đơn yêu cầu cấp/mua hàng của nhân viên " + purchaseRequest.User.FullName + " ngày " + purchaseRequest.CreatedDate.ToString("dd/MM/yyyy"),
                        Mail_cc = [],
                        JsonObject = JsonConvert.SerializeObject(emailModelToHR)
                    };

                    await _sendMailDynamicTemplateService.SendMailAsync(objSendMailToHR);
                }
            }
            else
            {
                var reviewText = CommonHelper.GetDescription((EPurchaseRequestStatus)purchaseRequest.ReviewStatus);
                var noteText = string.Empty;

                if (purchaseRequest.ReviewStatus == (int)EPurchaseRequestStatus.ManagerRejected)
                {
                    noteText = purchaseRequest.RejectReason;
                }

                if (purchaseRequest.ReviewStatus == (int)EPurchaseRequestStatus.ManagerUpdateRequest)
                {
                    noteText = purchaseRequest.ManagerComment;
                }

                var emailModel = new PurchaseRequestManagerSendMailModel()
                {
                    Reviewer = purchaseRequest.ReviewUser.FullName!,
                    PurchaseRequestName = purchaseRequest.Name,
                    ProjectName = purchaseRequest.Project?.Name ?? "",
                    CreatedDate = purchaseRequest.CreatedDate.ToString("dd/MM/yyyy"),
                    Register = purchaseRequest.User.FullName!,
                    EstimateDate = purchaseRequest.EstimateDate.HasValue ? purchaseRequest.EstimateDate.Value.ToString("dd/MM/yyyy") : "",
                    IsUrgent = purchaseRequest.IsUrgent ? needUrgently : normally,
                    ReviewStatus = reviewText!,
                    Note = noteText!,
                    LineItems = lineItems
                };

                var objSendMail = new ObjSendMail()
                {
                    FileName = "PurchaseRequestManagerToUserTemplate.html",
                    Mail_To = [purchaseRequest.User.Email!],
                    Title = "[Purchase Request] Kết quả của đơn yêu cầu cấp/mua hàng",
                    Mail_cc = [],
                    JsonObject = JsonConvert.SerializeObject(emailModel)
                };

                await _sendMailDynamicTemplateService.SendMailAsync(objSendMail);
            }
        }

        public async Task<CombineResponseModel<AdminResponseModel>> GetPrDtoByPrIdAsync(int id)
        {
            var res = new CombineResponseModel<AdminResponseModel>();
            var purchaseRequest = await _unitOfWork.PurchaseRequestRepository.GetPrDtoByPrIdAsync(id);
            if (purchaseRequest == null || !purchaseRequest.Any())
            {
                res.ErrorMessage = "Đơn đặt mua hàng không tồn tại";
                return res;
            }

            var prIds = new List<int>()
            {
                id
            };
            var poInfos = await _unitOfWork.PurchaseOrderRepository.GetPoDtoByPrIdsAsync(prIds);

            var lineItemIds = purchaseRequest.Select(o => o.LineItemId).Distinct().ToList();
            var ebInfos = await _unitOfWork.ExportBillRepository.GetDtoByLineItemIdsAsync(lineItemIds);

            var fileUrls = new List<string>();
            purchaseRequest.ForEach(item =>
            {
                if (!string.IsNullOrEmpty(item.FileUrl) && !fileUrls.Any(o => o.EndsWith(item.FileUrl)))
                {
                    fileUrls.Add($"{_blobDomainUrl}/{item.FileUrl}");
                }

                if (!string.IsNullOrEmpty(item.PoFileUrl) && !fileUrls.Any(o => o.EndsWith(item.PoFileUrl)))
                {
                    fileUrls.Add($"{_blobDomainUrl}/{item.PoFileUrl}");
                }
            });

            var data = purchaseRequest.GroupBy(z => new
            {
                z.Id,
                z.UserName,
                z.PurchaseRequestName,
                z.JobTitle,
                z.DepartmentName,
                z.ReviewUserName,
                z.ProjectName,
                z.IsUrgent,
                z.EstimateDate,
                z.Note,
                z.ReviewStatus,
                z.ManagerComment,
                z.FirstHrComment,
                z.SecondHrComment,
                z.DirectorComment,
                z.RejectReason
            }).Select(o => new AdminPurchaseRequestResponseModel
            {
                Id = o.Key.Id,
                UserName = o.Key.UserName,
                PurchaseRequestName = o.Key.PurchaseRequestName,
                JobTitle = o.Key.JobTitle,
                DepartmentName = o.Key.DepartmentName,
                ReviewUserName = o.Key.ReviewUserName,
                ProjectName = o.Key.ProjectName ?? "",
                IsUrgent = o.Key.IsUrgent,
                EstimateDate = o.Key.EstimateDate,
                Note = o.Key.Note ?? "",
                ReviewStatus = o.Key.ReviewStatus,
                ManagerComment = o.Key.ManagerComment ?? "",
                FirstHrComment = o.Key.FirstHrComment ?? "",
                SecondHrComment = o.Key.SecondHrComment ?? "",
                DirectorHrComment = o.Key.DirectorComment ?? "",
                RejectReason = o.Key.RejectReason ?? "",
                FileUrls = fileUrls,
                AdminLineItems = o.GroupBy(t => new
                {
                    t.LineItemId,
                    t.ProductCategoryId,
                    t.ProductCategoryName,
                    t.ProductId,
                    t.ProductName,
                    t.ProductDescription,
                    t.LineItemQuantity,
                    t.LineItemShoppingUrl,
                    t.LineItemNote
                }).Select(y => new AdminPurchaseRequestLineItemResponseModel
                {
                    LineItemId = y.Key.LineItemId,
                    ProductCategoryId = y.Key.ProductCategoryId,
                    ProductCategoryName = y.Key.ProductCategoryName,
                    ProductId = y.Key.ProductId,
                    ProductName = y.Key.ProductName,
                    ProductDescription = y.Key.ProductDescription,
                    LineItemQuantity = y.Key.LineItemQuantity,
                    LineItemNote = y.Key.LineItemNote ?? "",
                    LineItemShoppingUrl = y.Key.LineItemShoppingUrl ?? "",
                    AdminSubProductModels = y.GroupBy(v => new
                    {
                        v.SubProductId,
                        v.SubProductName,
                        v.KitQuantity,
                        v.SubProductDescription
                    }).Select(q => new AdminPurchaseRequestSubProductModel
                    {
                        SubProductId = q.Key.SubProductId ?? 0,
                        SubProductName = q.Key.SubProductName ?? "",
                        SubProductDescription = q.Key.SubProductDescription ?? "",
                        KitQuantity = q.Key.KitQuantity ?? 0,
                    }).OrderBy(o => o.SubProductName).ToList(),
                    AdminPurchaseOrderModels = y.Where(i => i.PurchaseOrderId.HasValue).GroupBy(w => w.PurchaseOrderId).Select(w => new AdminPurchaseOrderModel
                    {
                        PoId = w.Key ?? 0,
                        Price = w.FirstOrDefault()?.Price ?? 0,
                        VendorId = w.FirstOrDefault()?.VendorId ?? 0,
                        VendorName = w.FirstOrDefault()?.VendorName ?? "",
                        PurchaseQuantity = w.FirstOrDefault()?.PurchaseQuantity ?? 0,
                        Vat = w.FirstOrDefault()?.Vat ?? 0
                    }).ToList(),
                    AdminExportBillModels = ebInfos.Where(o => o.LineItemId == y.Key.LineItemId).Select(r => new AdminExportBillModel
                    {
                        ExportId = r.ExportBillId,
                        ExportQuantity = r.ExportQuantity,
                    }).ToList()
                }).OrderBy(o => o.ProductName).ToList()
            }).FirstOrDefault();

            var totalPrice = 0M;
            var totalPriceWithVat = 0M;
            var totalVat = 0M;

            if (data != null)
            {
                foreach (var lineItem in data.AdminLineItems)
                {
                    if (lineItem.AdminPurchaseOrderModels != null && lineItem.AdminPurchaseOrderModels.Any())
                    {
                        foreach (var po in lineItem.AdminPurchaseOrderModels)
                        {
                            var totalPricePerPo = po.PurchaseQuantity * po.Price;
                            totalPrice += totalPricePerPo ?? 0;

                            var totalVatPerPo = (po.PurchaseQuantity * po.Price * po.Vat / 100) ?? 0;
                            totalVat += totalVatPerPo;

                            totalPriceWithVat += (totalPricePerPo + totalVatPerPo) ?? 0;
                        }
                    }
                }
            }

            var response = new AdminResponseModel
            {
                AdminPurchaseRequestResponseModel = data,
                TotalPrice = (int)Math.Floor(totalPrice),
                TotalVat = (int)Math.Floor(totalVat),
                TotalPriceWithVat = (int)Math.Floor(totalPriceWithVat)
            };
            res.Status = true;
            res.Data = response;
            return res;
        }

        public async Task<CombineResponseModel<int>> HrPrepareReviewAsync(int id, HrReviewModel request)
        {
            // Hr: chỉ có yêu cầu cập nhật & từ chối, không có duyệt (tạo po => để Accountant duyệt)
            var res = new CombineResponseModel<int>();

            if (request.ReviewStatus != EPurchaseRequestStatus.HrRejected &&
                request.ReviewStatus != EPurchaseRequestStatus.HrUpdateRequest)
            {
                res.ErrorMessage = "Trạng thái duyệt không hợp lệ";
                return res;
            }

            if (string.IsNullOrEmpty(request.ReviewNote))
            {
                if (request.ReviewStatus == EPurchaseRequestStatus.HrUpdateRequest)
                {
                    res.ErrorMessage = "Yêu cầu cập nhật không được trống";
                    return res;
                }
                else if (request.ReviewStatus == EPurchaseRequestStatus.HrRejected)
                {
                    res.ErrorMessage = "Lý do từ chối không được trống";
                    return res;
                }
            }

            var purchaseRequest = await _unitOfWork.PurchaseRequestRepository.GetByIdAsync(id);
            if (purchaseRequest == null)
            {
                res.ErrorMessage = "Đơn đặt hàng không tồn tại";
                return res;
            }

            if (!IsAllowUpdateRequestOrRejectByStatus((EPurchaseRequestStatus)purchaseRequest.ReviewStatus))
            {
                res.ErrorMessage = "Trạng thái đơn hàng không cho phép duyệt";
                return res;
            }

            purchaseRequest.ReviewStatus = (int)request.ReviewStatus;
            if (request.ReviewStatus == EPurchaseRequestStatus.HrUpdateRequest)
            {
                purchaseRequest.FirstHrComment = request.ReviewNote;
            }
            else if (request.ReviewStatus == EPurchaseRequestStatus.HrRejected)
            {
                purchaseRequest.RejectReason = request.ReviewNote;
            }

            await _unitOfWork.PurchaseRequestRepository.UpdateAsync(purchaseRequest);
            await _unitOfWork.SaveChangesAsync();

            res.Status = true;
            res.Data = id;
            return res;
        }

        public async Task HrSendEmailAsync(int purchaseRequestId)
        {
            var purchaseRequest = await _unitOfWork.PurchaseRequestRepository.GetByIdAsync(purchaseRequestId);
            if (purchaseRequest != null)
            {
                var productIds = purchaseRequest.PurchaseRequestLineItems.Select(o => o.ProductId).Distinct().ToList();
                var subProductModels = await _unitOfWork.ProductRepository.GetSubProductsByProductIdsAsync(productIds);
                string dateString = purchaseRequest.CreatedDate.ToString("dd/MM/yyyy");
                var lineItems = string.Empty;
                var lineItemModels = purchaseRequest.PurchaseRequestLineItems.GroupBy(o => new
                {
                    ProductCategoryName = o.Product.ProductCategory.Name,
                    ProductName = o.Product.Name,
                    Quantity = o.Quantity,
                    ProductDescription = o.Product.Description,
                    ProductId = o.ProductId
                }).Select(y => new PurchaseRequestLineItemEmailModel
                {
                    ProductCategoryName = y.Key.ProductCategoryName,
                    ProductName = y.Key.ProductName,
                    ProductDescription = y.Key.ProductDescription,
                    Quantity = y.Key.Quantity,
                    ProductId = y.Key.ProductId,
                    SubProductEmailModels = subProductModels.Where(z => z.ProductId == y.Key.ProductId).Select(t => new PurchaseRequestSubProductEmailModel
                    {
                        SubProductName = t.SubProductName,
                        SubProductDescription = t.SubProductDescription,
                        Quantity = t.KitQuantity,
                    }).ToList()
                }).ToList();
                foreach (var lineItem in lineItemModels)
                {
                    if (lineItem.SubProductEmailModels != null && lineItem.SubProductEmailModels.Any())
                    {
                        var index = 0;
                        foreach (var item in lineItem.SubProductEmailModels)
                        {
                            lineItems += $@"<tr style='border-collapse: collapse;border: 1px solid black'>";
                            if (index == 0)
                            {
                                lineItems += $@"<td style='border-collapse: collapse;border: 1px solid black' rowSpan='{lineItem.SubProductEmailModels.Count}'>{lineItem.ProductCategoryName}</td>
                                                <td style='border-collapse: collapse;border: 1px solid black' rowSpan='{lineItem.SubProductEmailModels.Count}'>{lineItem.ProductName}</td>
                                                <td style='text-align: center;border-collapse: collapse;border: 1px solid black' rowSpan='{lineItem.SubProductEmailModels.Count}'>{lineItem.Quantity}</td>";
                            }
                            lineItems += $@"<td style='border-collapse: collapse;border: 1px solid black'>{item.SubProductName}</td>
                                                <td style='border-collapse: collapse;border: 1px solid black'>{item.SubProductDescription}</td>
                                                <td style='text-align: center;border-collapse: collapse;border: 1px solid black'>{(item.Quantity == 0 ? "" : item.Quantity)}</td>
                                            </tr>";
                            index++;
                        }
                    }
                    else
                    {
                        lineItems += $@"<tr style='border-collapse: collapse;border: 1px solid black'>
                                        <td style='border-collapse: collapse;border: 1px solid black'>{lineItem.ProductCategoryName}</td>
                                        <td style='border-collapse: collapse;border: 1px solid black'>{lineItem.ProductName}</td>
                                        <td style='text-align: center;border-collapse: collapse;border: 1px solid black'>{lineItem.Quantity}</td>
                                        <td></td>
                                        <td style='border-collapse: collapse;border: 1px solid black'>{lineItem.ProductDescription}</td>
                                    </tr>";
                    }
                }

                var reviewNote = string.Empty;
                if (purchaseRequest.ReviewStatus == (int)EPurchaseRequestStatus.HrUpdateRequest)
                {
                    reviewNote = purchaseRequest.FirstHrComment;
                }
                else if (purchaseRequest.ReviewStatus == (int)EPurchaseRequestStatus.HrRejected)
                {
                    reviewNote = purchaseRequest.RejectReason;
                }
                var emailModel = new PurchaseRequestHrSendEmailModel()
                {
                    Reviewer = purchaseRequest.ReviewUser.FullName!,
                    PurchaseRequestName = purchaseRequest.Name,
                    ProjectName = purchaseRequest.Project?.Name ?? "",
                    CreatedDate = dateString,
                    Register = purchaseRequest.User.FullName!,
                    ReviewLink = "",
                    EstimateDate = purchaseRequest.EstimateDate.HasValue ? purchaseRequest.EstimateDate.Value.ToString("dd/MM/yyyy") : "",
                    IsUrgent = purchaseRequest.IsUrgent ? needUrgently : normally,
                    LineItems = lineItems,
                    Note = reviewNote ?? string.Empty,
                    ReviewStatus = CommonHelper.GetDescription((EPurchaseRequestStatus)purchaseRequest.ReviewStatus)
                };

                var objSendMail = new ObjSendMail()
                {
                    FileName = "PurchaseRequestHrToUserTemplate.html",
                    Mail_To = new List<string>() { purchaseRequest.User.Email! },
                    Title = "[Purchase Request] Kết quả của đơn yêu cầu cấp/mua hàng",
                    Mail_cc = new List<string>(),
                    JsonObject = JsonConvert.SerializeObject(emailModel)
                };
                await _sendMailDynamicTemplateService.SendMailAsync(objSendMail);
            }
        }

        public async Task<PagingResponseModel<AccountantPurchaseRequestPagingResponse>> AccountantGetPrPagingAsync(AccountantPurchaseRequestPagingModel request, bool isDirector)
        {
            var recordsRaw = await _unitOfWork.PurchaseRequestRepository.AccountantGetPrPagingAsync(request, isDirector);

            var totalRecords = recordsRaw.FirstOrDefault();
            if (totalRecords != null)
            {
                recordsRaw.Remove(totalRecords);
            }
            var prIds = recordsRaw.Select(o => o.Id).ToList();
            var poInfos = await _unitOfWork.PurchaseOrderRepository.GetPoDtoByPrIdsAsync(prIds);
            var ebInfos = await _unitOfWork.PurchaseOrderRepository.GetEbDtoByPrIdsAsync(prIds);
            var records = recordsRaw.Select(o =>
            {
                var poInfo = poInfos.Where(x => x.PurchaseRequestId == o.Id).ToList();
                var totalPrice = poInfo.Sum(o => o.TotalPricePerItemWithVat);
                var ebInfo = ebInfos.Where(x => x.PurchaseRequestId == o.Id).ToList();
                var record = new AccountantPurchaseRequestPagingResponse()
                {
                    Id = o.Id,
                    CreatedDate = o.CreatedDate,
                    Name = o.Name,
                    Quantity = o.Quantity,
                    DepartmentName = o.DepartmentName,
                    ProjectName = o.ProjectName,
                    RegisterId = o.RegisterId,
                    RegisterName = o.RegisterName,
                    ReviewStatus = o.ReviewStatus,
                    IsUrgent = o.IsUrgent,
                    TotalPrice = (long)Math.Floor(totalPrice),
                    EstimateDate = o.EstimateDate.HasValue ? o.EstimateDate.Value.ToString("dd/MM/yyyy") : "",
                    PoInfos = poInfo != null && poInfo.Any() ? poInfo.Where(p => p.PurchaseRequestId == o.Id).GroupBy(w => w.PurchaseOrderId).Select(z => new AccountantPurchaseOrderResponse
                    {
                        PurchaseOrderId = z.Key,
                        PoIdStr = CommonHelper.ToIdDisplayString("PO", z.Key),
                        Price = z.Sum(o => o.Price),
                        PurchaseQuantity = z.Sum(o => o.PurchaseQuantity),
                        VendorId = z.FirstOrDefault()?.VendorId ?? 0,
                        VendorName = z.FirstOrDefault()?.VendorName ?? "",
                        TotalPricePerPo = (long)Math.Floor(z.Sum(o => o.TotalPricePerItemWithVat))
                    }).ToList() : new List<AccountantPurchaseOrderResponse>(),
                    EbInfos = ebInfo != null && ebInfo.Any() ? ebInfo.Where(p => p.PurchaseRequestId == o.Id).GroupBy(r => r.ExportBillId).Select(v => new AccountantExportBillResponse
                    {
                        ExportBillId = v.Key,
                        EbIdStr = CommonHelper.ToIdDisplayString("XK", v.Key),
                        ExportQuantity = v.Sum(u => u.ExportQuantity),
                    }).ToList() : new List<AccountantExportBillResponse>(),
                    TotalRecord = o.TotalRecord
                };
                return record;
            }).ToList();

            var res = new PagingResponseModel<AccountantPurchaseRequestPagingResponse>
            {
                Items = records,
                TotalRecord = totalRecords?.TotalRecord ?? 0
            };

            return res;
        }

        public async Task<CombineResponseModel<int>> AccountantPrepareReviewAsync(int id, AccountantReviewModel request)
        {
            var res = new CombineResponseModel<int>();
            if (request.ReviewStatus != EPurchaseRequestStatus.AccountantRejected &&
                request.ReviewStatus != EPurchaseRequestStatus.AccountantUpdateRequest &&
                request.ReviewStatus != EPurchaseRequestStatus.AccountantApproved)
            {
                res.ErrorMessage = "Trạng thái duyệt không hợp lệ";
                return res;
            }

            if (string.IsNullOrEmpty(request.ReviewNote))
            {
                if (request.ReviewStatus == EPurchaseRequestStatus.AccountantUpdateRequest)
                {
                    res.ErrorMessage = "Yêu cầu cập nhật không được trống";
                    return res;
                }
                else if (request.ReviewStatus == EPurchaseRequestStatus.AccountantRejected)
                {
                    res.ErrorMessage = "Lý do từ chối không được trống";
                    return res;
                }
            }

            // Accountant chỉ duyệt/yêu cầu cập nhật đơn có purchase order hoặc export bill
            if (request.ReviewStatus == EPurchaseRequestStatus.AccountantApproved || request.ReviewStatus == EPurchaseRequestStatus.AccountantUpdateRequest)
            {
                if ((request.ReviewPurchaseOrderModels == null || !request.ReviewPurchaseOrderModels.Any()) && (request.ReviewExportBillModels == null || !request.ReviewExportBillModels.Any()))
                {
                    res.ErrorMessage = "Không thể duyệt yêu cầu không có phiếu đặt hàng và phiếu xuất kho";
                    return res;
                }
            }

            var purchaseRequest = await _unitOfWork.PurchaseRequestRepository.GetByIdAsync(id);
            if (purchaseRequest == null)
            {
                res.ErrorMessage = "Đơn đặt hàng không tồn tại";
                return res;
            }

            var isAllowReview = IsAllowUpdateRequestOrRejectByStatus((EPurchaseRequestStatus)purchaseRequest.ReviewStatus);
            if (!isAllowReview)
            {
                res.ErrorMessage = "Trạng thái đơn không cho phép duyệt";
                return res;
            }

            var poIds = new List<int>();

            // Kiểm tra đã đủ số lượng hay chưa?
            if (request.ReviewStatus == EPurchaseRequestStatus.AccountantApproved)
            {
                var quantityFromRequest = purchaseRequest.PurchaseRequestLineItems.Sum(o => o.Quantity);
                var quantityFromPoAndEb = request.ReviewPurchaseOrderModels?.Sum(o => o.PurchaseQuantity) + request.ReviewExportBillModels?.Sum(o => o.ExportQuantity);
                if (quantityFromRequest != quantityFromPoAndEb)
                {
                    res.ErrorMessage = "Số lượng đặt hàng và xuất kho không hợp lệ";
                    return res;
                }

                // Kiểm tra kế hoạch thanh toán của Po
                if (request.ReviewPurchaseOrderModels != null)
                {
                    poIds.AddRange(request.ReviewPurchaseOrderModels.Select(o => o.PurchaseOrderId).Distinct().ToList());

                    var vendors = await _unitOfWork.VendorRepository.GetAllAsync();
                    var paymentPlans = await _unitOfWork.PaymentPlanRepository.GetByPoIdsAsync(poIds);

                    var poPrices = await _unitOfWork.PurchaseOrderRepository.GetPoPriceByIdsAsync(poIds);
                    foreach (var poPrice in poPrices)
                    {
                        var paymentPlanByPo = paymentPlans.Where(o => o.PurchaseOrderId == poPrice.PurchaseOrderId).Sum(o => o.PaymentAmount);
                        if (paymentPlanByPo != (int)Math.Floor(poPrice.TotalPrice))
                        {
                            res.ErrorMessage = $"Chi phí kế hoạch thanh toán của PO '{CommonHelper.ToIdDisplayString("PO", poPrice.PurchaseOrderId)}' không hợp lệ";
                            return res;
                        }
                    }

                    foreach (var poModel in request.ReviewPurchaseOrderModels)
                    {
                        var vendor = vendors.Find(o => o.Id == poModel.VendorId);
                        if (vendor == null)
                        {
                            res.ErrorMessage = $"Nhà cung cấp của PO '{CommonHelper.ToIdDisplayString("PO", poModel.PurchaseOrderId)}' không tồn tại";
                            return res;
                        }
                    }
                }
            }
            // Update Status & Review Note
            await _unitOfWork.PurchaseRequestRepository.UpdatePurchaseRequestAsync(purchaseRequest.Id, request.ReviewNote, request.ReviewStatus);

            // Khi Accountant Reject => Xóa PR khỏi PO
            if (request.ReviewStatus == EPurchaseRequestStatus.AccountantRejected)
            {
                await _unitOfWork.PurchaseRequestRepository.RemovePoByPrIdAsync(id);

                await _unitOfWork.ExportBillRepository.RemoveExportBillByPrIdAsync(id);
            }
            else if (request.ReviewStatus == EPurchaseRequestStatus.AccountantApproved)
            {
                // Kiểm tra PR có trong những PO đó
                // Nếu trong po tồn tại pr có status != DirectorApprove => update status cho po = đang duyệt, ngược lại thì set status = đã duyệt
                await _unitOfWork.PurchaseOrderRepository.UpdateStatusAsync(poIds);
            }

            res.Status = true;
            res.Data = id;
            return res;
        }

        public async Task AccountantSendEmailAsync(string accountantName, int purchaseRequestId)
        {
            var purchaseRequest = await _unitOfWork.PurchaseRequestRepository.GetPrDtoByPrIdAsync(purchaseRequestId);
            if (purchaseRequest == null || !purchaseRequest.Any())
            {
                return;
            }

            var lineItemIds = purchaseRequest.Select(o => o.LineItemId).Distinct().ToList();
            var ebInfos = await _unitOfWork.ExportBillRepository.GetDtoByLineItemIdsAsync(lineItemIds);

            var data = purchaseRequest.GroupBy(z => new
            {
                z.Id,
                z.UserName,
                z.PurchaseRequestName,
                z.JobTitle,
                z.DepartmentName,
                z.ReviewUserName,
                z.ProjectName,
                z.IsUrgent,
                z.EstimateDate,
                z.Note,
                z.ReviewStatus,
                z.ManagerComment,
                z.FirstHrComment,
                z.SecondHrComment,
                z.DirectorComment,
                z.RejectReason,
                z.CreatedDate,
                z.Email
            }).Select(o => new AccountantSendEmailModel
            {
                Id = o.Key.Id,
                UserName = o.Key.UserName,
                Email = o.Key.Email,
                PurchaseRequestName = o.Key.PurchaseRequestName,
                JobName = o.Key.JobTitle,
                DepartmentName = o.Key.DepartmentName,
                ReviewUserName = o.Key.ReviewUserName,
                ProjectName = o.Key.ProjectName ?? "",
                IsUrgent = o.Key.IsUrgent,
                CreatedDate = o.Key.CreatedDate,
                EstimateDate = o.Key.EstimateDate,
                Note = o.Key.Note ?? "",
                ReviewStatus = o.Key.ReviewStatus,
                ManagerComment = o.Key.ManagerComment ?? "",
                FirstHrComment = o.Key.FirstHrComment ?? "",
                SecondHrComment = o.Key.SecondHrComment ?? "",
                DirectorComment = o.Key.DirectorComment ?? "",
                RejectReason = o.Key.RejectReason ?? "",
                FileUrls = [],
                IsAllowUpdate = false,
                AccountantLineItems = o.GroupBy(t => new
                {
                    t.LineItemId,
                    t.ProductCategoryId,
                    t.ProductCategoryName,
                    t.ProductId,
                    t.ProductName,
                    t.ProductDescription,
                    t.LineItemQuantity
                }).Select(y => new AccountantLineItemSendEmailModel
                {
                    LineItemId = y.Key.LineItemId,
                    ProductCategoryId = y.Key.ProductCategoryId,
                    ProductCategoryName = y.Key.ProductCategoryName,
                    ProductId = y.Key.ProductId,
                    ProductName = y.Key.ProductName,
                    ProductDescription = y.Key.ProductDescription,
                    LineItemQuantity = y.Key.LineItemQuantity,
                    AccountantSubProductEmailModels = y.GroupBy(v => new
                    {
                        v.SubProductId,
                        v.SubProductName,
                        v.KitQuantity,
                        v.SubProductDescription,
                    }).Select(q => new AccountantSubProductEmailModel
                    {
                        SubProductName = q.Key.SubProductName ?? "",
                        SubProductDescription = q.Key.SubProductDescription ?? "",
                        Quantity = q.Key.KitQuantity ?? 0,
                    }).OrderBy(o => o.SubProductName).ToList(),
                    AccountantPurchaseOrderModels = y.Where(i => i.PurchaseOrderId.HasValue).GroupBy(w => w.PurchaseOrderId).Select(w => new AccountantPurchaseOrderModel
                    {
                        PoIdStr = w.Key.HasValue ? CommonHelper.ToIdDisplayString("PO", w.Key.Value) : "",
                        Price = w.FirstOrDefault()?.Price ?? 0,
                        VendorName = w.FirstOrDefault()?.VendorName,
                        PurchaseOrderQuantity = w.FirstOrDefault()?.PurchaseQuantity ?? 0,
                    }).OrderBy(o => o.PoIdStr).ToList(),
                    AccountantExportBillModels = ebInfos.Where(o => o.LineItemId == y.Key.LineItemId).Select(r => new AccountantExportBillModel
                    {
                        ExportBillIdStr = CommonHelper.ToIdDisplayString("XK", r.ExportBillId),
                        ExportQuantity = r.ExportQuantity
                    }).ToList()
                }).OrderBy(o => o.ProductName).ToList()
            }).FirstOrDefault();

            if (data == null || (data.ReviewStatus != (int)EPurchaseRequestStatus.AccountantApproved &&
                                 data.ReviewStatus != (int)EPurchaseRequestStatus.AccountantUpdateRequest &&
                                 data.ReviewStatus != (int)EPurchaseRequestStatus.AccountantRejected))
            {
                return;
            }

            var lineItems = string.Empty;
            foreach (var lineItem in data.AccountantLineItems)
            {
                var rowSpan = CalculateRowSpan(lineItem);
                if (lineItem.AccountantSubProductEmailModels != null && lineItem.AccountantSubProductEmailModels.Count != 0)
                {
                    var index = 0; // index của AccountantSubProductEmailModels
                    if (data.ReviewStatus == (int)EPurchaseRequestStatus.AccountantApproved || data.ReviewStatus == (int)EPurchaseRequestStatus.AccountantUpdateRequest)
                    {
                        foreach (var item in lineItem.AccountantSubProductEmailModels)
                        {
                            if (item != null)
                            {
                                lineItems += $@"<tr style='border-collapse: collapse;border: 1px solid black'>";
                                if (index == 0)
                                {
                                    lineItems += $@"<td style='border-collapse: collapse;border: 1px solid black' rowSpan='{rowSpan}'>{lineItem.ProductCategoryName}</td>
                                                    <td style='border-collapse: collapse;border: 1px solid black' rowSpan='{rowSpan}'>{lineItem.ProductName}</td>
                                                    <td style='text-align:center; border-collapse: collapse;border: 1px solid black' rowSpan='{rowSpan}'>{lineItem.LineItemQuantity}</td>";
                                }
                                lineItems += $@"<td style='border-collapse: collapse;border: 1px solid black'>{item.SubProductName}</td>
                                                <td style='border-collapse: collapse;border: 1px solid black'>{item.SubProductDescription}</td>";

                                if (index == 0)
                                {
                                    if (lineItem.AccountantPurchaseOrderModels != null && lineItem.AccountantPurchaseOrderModels.Any())
                                    {
                                        lineItems += $@"<td style = 'text-align:center; border-collapse: collapse;border: 1px solid black' rowSpan='{rowSpan}'>";
                                        var poIndex = 0;
                                        foreach (var po in lineItem.AccountantPurchaseOrderModels)
                                        {
                                            lineItems += @$"<div style='border-bottom: {(poIndex < lineItem.AccountantPurchaseOrderModels.Count - 1 ? "1px solid #eceaea" : "none")}'><p>{(po.Price.HasValue ? (int)Math.Floor(po.Price.Value) : po.Price)}</p></div>";
                                            poIndex++;
                                        }
                                        lineItems += "</td>";

                                        poIndex = 0;
                                        lineItems += $@"<td style = 'text-align:center; border-collapse: collapse;border: 1px solid black' rowSpan='{rowSpan}'>";
                                        foreach (var po in lineItem.AccountantPurchaseOrderModels)
                                        {
                                            lineItems += @$"<div style='border-bottom: {(poIndex < lineItem.AccountantPurchaseOrderModels.Count - 1 ? "1px solid #eceaea" : "none")}'><p>{po.VendorName}</p></div>";
                                            poIndex++;
                                        }
                                        lineItems += "</td>";

                                        poIndex = 0;
                                        lineItems += $@"<td style = 'text-align:center; border-collapse: collapse;border: 1px solid black' rowSpan='{rowSpan}'>";
                                        foreach (var po in lineItem.AccountantPurchaseOrderModels)
                                        {
                                            lineItems += @$"<div style='border-bottom: {(poIndex < lineItem.AccountantPurchaseOrderModels.Count - 1 ? "1px solid #eceaea" : "none")}'><p>{po.PoIdStr}</p></div>";
                                            poIndex++;
                                        }
                                        lineItems += "</td>";

                                        poIndex = 0;
                                        lineItems += $@"<td style = 'text-align:center; border-collapse: collapse;border: 1px solid black' rowSpan='{rowSpan}'>";
                                        foreach (var po in lineItem.AccountantPurchaseOrderModels)
                                        {
                                            lineItems += @$"<div style='border-bottom: {(poIndex < lineItem.AccountantPurchaseOrderModels.Count - 1 ? "1px solid #eceaea" : "none")}'><p>{po.PurchaseOrderQuantity}</p></div>";
                                            poIndex++;
                                        }
                                        lineItems += "</td>";

                                        if (lineItem.AccountantExportBillModels != null && lineItem.AccountantExportBillModels.Any())
                                        {
                                            poIndex = 0;
                                            lineItems += $@"<td style = 'text-align:center; border-collapse: collapse;border: 1px solid black' rowSpan='{rowSpan}'>";
                                            foreach (var eb in lineItem.AccountantExportBillModels)
                                            {
                                                lineItems += @$"<div style='border-bottom: {(poIndex < lineItem.AccountantExportBillModels.Count - 1 ? "1px solid #eceaea" : "none")}'><p>{eb.ExportBillIdStr}</p></div>";
                                                poIndex++;
                                            }
                                            lineItems += "</td>";

                                            poIndex = 0;
                                            lineItems += $@"<td style = 'text-align:center; border-collapse: collapse;border: 1px solid black' rowSpan='{rowSpan}'>";
                                            foreach (var eb in lineItem.AccountantExportBillModels)
                                            {
                                                lineItems += @$"<div style='border-bottom: {(poIndex < lineItem.AccountantExportBillModels.Count - 1 ? "1px solid #eceaea" : "none")}'><p>{(eb.ExportQuantity == 0 ? "" : eb.ExportQuantity)}</p></div>";
                                                poIndex++;
                                            }
                                            lineItems += "</td>";
                                        }
                                        else
                                        {
                                            lineItems += $@"<td style = 'text-align:center; border-collapse: collapse;border: 1px solid black' rowSpan='{rowSpan}'>";
                                            lineItems += @$"<div style=''><p>{""}</p></div>";
                                            lineItems += "</td>";
                                            lineItems += $@"<td style = 'text-align:center; border-collapse: collapse;border: 1px solid black' rowSpan='{rowSpan}'>";
                                            lineItems += @$"<div style=''><p>{""}</p></div>";
                                            lineItems += "</td>";
                                        }
                                    }
                                    else
                                    {
                                        var poIndex = 0;
                                        lineItems += $@"<td style = 'text-align:center; border-collapse: collapse;border: 1px solid black' rowSpan='{rowSpan}'>";
                                        lineItems += @$"<div style=''><p>{""}</p></div>";
                                        lineItems += "</td>";
                                        lineItems += $@"<td style = 'text-align:center; border-collapse: collapse;border: 1px solid black' rowSpan='{rowSpan}'>";
                                        lineItems += @$"<div style=''><p>{""}</p></div>";
                                        lineItems += "</td>";
                                        lineItems += $@"<td style = 'text-align:center; border-collapse: collapse;border: 1px solid black' rowSpan='{rowSpan}'>";
                                        lineItems += @$"<div style=''><p>{""}</p></div>";
                                        lineItems += "</td>";
                                        lineItems += $@"<td style = 'text-align:center; border-collapse: collapse;border: 1px solid black' rowSpan='{rowSpan}'>";
                                        lineItems += @$"<div style=''><p>{""}</p></div>";
                                        lineItems += "</td>";

                                        if (lineItem.AccountantExportBillModels != null && lineItem.AccountantExportBillModels.Any())
                                        {
                                            poIndex = 0;
                                            lineItems += $@"<td style = 'text-align:center; border-collapse: collapse;border: 1px solid black' rowSpan='{rowSpan}'>";
                                            foreach (var eb in lineItem.AccountantExportBillModels)
                                            {
                                                lineItems += @$"<div style='border-bottom: {(poIndex < lineItem.AccountantExportBillModels.Count - 1 ? "1px solid #eceaea" : "none")}'><p>{eb.ExportBillIdStr}</p></div>";
                                                poIndex++;
                                            }
                                            lineItems += "</td>";

                                            poIndex = 0;
                                            lineItems += $@"<td style = 'text-align:center; border-collapse: collapse;border: 1px solid black' rowSpan='{rowSpan}'>";
                                            foreach (var eb in lineItem.AccountantExportBillModels)
                                            {
                                                lineItems += @$"<div style='border-bottom: {(poIndex < lineItem.AccountantExportBillModels.Count - 1 ? "1px solid #eceaea" : "none")}'><p>{(eb.ExportQuantity == 0 ? "" : eb.ExportQuantity)}</p></div>";
                                                poIndex++;
                                            }
                                            lineItems += "</td>";
                                        }
                                        else
                                        {
                                            lineItems += $@"<td style = 'text-align:center; border-collapse: collapse;border: 1px solid black' rowSpan='{rowSpan}'>";
                                            lineItems += @$"<div style=''><p>{""}</p></div>";
                                            lineItems += "</td>";
                                            lineItems += $@"<td style = 'text-align:center; border-collapse: collapse;border: 1px solid black' rowSpan='{rowSpan}'>";
                                            lineItems += @$"<div style=''><p>{""}</p></div>";
                                            lineItems += "</td>";
                                        }
                                    }
                                }
                                index++;
                            }
                        }
                    }
                    else if (data.ReviewStatus == (int)EPurchaseRequestStatus.AccountantRejected)
                    {
                        foreach (var item in lineItem.AccountantSubProductEmailModels)
                        {
                            lineItems += $@"<tr style='border-collapse: collapse;border: 1px solid black'>";
                            if (index == 0)
                            {
                                lineItems += $@"<td style='border-collapse: collapse;border: 1px solid black' rowspan='{lineItem.AccountantSubProductEmailModels.Count}'>{lineItem.ProductCategoryName}</td>
                                                <td style='border-collapse: collapse;border: 1px solid black' rowspan='{lineItem.AccountantSubProductEmailModels.Count}'>{lineItem.ProductName}</td>
                                                <td style='text-align:center; border-collapse: collapse;border: 1px solid black' rowspan='{lineItem.AccountantSubProductEmailModels.Count}'>{lineItem.LineItemQuantity}</td>";
                            }
                            lineItems += $@"<td style='border-collapse: collapse;border: 1px solid black'>{item.SubProductName}</td>
                                            <td style='border-collapse: collapse;border: 1px solid black'>{item.SubProductDescription}</td>
                                            <td style='text-align:center; border-collapse: collapse;border: 1px solid black'>{(item.Quantity == 0 ? "" : item.Quantity)}</td>
                                        </tr>";
                            index++;
                        }
                    }
                }
            }

            var emails = new List<string>();

            // Accountant yccn => gửi cho hr để update po/eb
            if (data.ReviewStatus == (int)EPurchaseRequestStatus.AccountantUpdateRequest)
            {
                var hrValue = await _unitOfWork.GlobalConfigurationRepository.GetByNameAsync(Constant.FirstHrEmail);
                if (hrValue != null && !string.IsNullOrEmpty(hrValue.Value))
                {
                    var hrEmails = hrValue.Value!.Split(",").Where(email => !string.IsNullOrEmpty(email)).Select(o => o.Trim()).ToList();
                    var emailModel = new AccoutantSendMailModel
                    {
                        PurchaseRequestName = data.PurchaseRequestName,
                        Register = data.UserName,
                        Note = data.SecondHrComment ?? "",
                        Sender = accountantName,
                        Receiver = backOffice,
                        ReviewLink = _frontEndDomain + Constant.HrReviewPurchaseRequestPath,
                        PoLineItems = lineItems,
                        CreatedDate = data.CreatedDate.ToString("dd/MM/yyyy"),
                        IsUrgent = data.IsUrgent ? needUrgently : normally,
                        ProjectName = data.ProjectName ?? "",
                        EstimateDate = data.EstimateDate.HasValue ? data.EstimateDate.Value.ToString("dd/MM/yyyy") : "",
                        ReviewStatus = CommonHelper.GetDescription((EPurchaseRequestStatus)data.ReviewStatus)
                    };

                    emails.AddRange(hrEmails);

                    var objSendMail = new ObjSendMail()
                    {
                        FileName = "PurchaseRequestAccountantToHrTemplate.html",
                        Mail_To = emails,
                        Title = "[Purchase Order] Yêu cầu cập nhật đơn mua hàng",
                        Mail_cc = new List<string>(),
                        JsonObject = JsonConvert.SerializeObject(emailModel)
                    };

                    await _sendMailDynamicTemplateService.SendMailAsync(objSendMail);
                }
            }
            // Accountant approved => gửi cho director
            else if (data.ReviewStatus == (int)EPurchaseRequestStatus.AccountantApproved)
            {
                var directorValue = await _unitOfWork.GlobalConfigurationRepository.GetByNameAsync(Constant.DirectorEmail);
                if (directorValue != null && !string.IsNullOrEmpty(directorValue.Value))
                {
                    var directorEmails = directorValue.Value!.Split(",").Where(email => !string.IsNullOrEmpty(email)).Select(o => o.Trim()).ToList();
                    var emailModel = new AccoutantSendMailModel
                    {
                        PurchaseRequestName = data.PurchaseRequestName,
                        Register = data.UserName!,
                        Note = "",
                        Sender = accountantName,
                        Receiver = boardOfDirector,
                        ReviewLink = _frontEndDomain + Constant.DirectorReviewPurchaseRequestPath,
                        PoLineItems = lineItems,
                        CreatedDate = data.CreatedDate.ToString("dd/MM/yyyy"),
                        IsUrgent = data.IsUrgent ? needUrgently : normally,
                        ProjectName = data.ProjectName ?? "",
                        EstimateDate = data.EstimateDate.HasValue ? data.EstimateDate.Value.ToString("dd/MM/yyyy") : "",
                        ReviewStatus = CommonHelper.GetDescription((EPurchaseRequestStatus)data.ReviewStatus)
                    };

                    emails.AddRange(directorEmails);

                    var objSendMail = new ObjSendMail()
                    {
                        FileName = "PurchaseRequestAccountantToDirectorTemplate.html",
                        Mail_To = emails,
                        Title = "[Purchase Order] Yêu cầu duyệt đơn mua hàng",
                        Mail_cc = new List<string>(),
                        JsonObject = JsonConvert.SerializeObject(emailModel)
                    };

                    await _sendMailDynamicTemplateService.SendMailAsync(objSendMail);
                }
            }
            // Từ chối => gửi về lại cho nhân viên
            else
            {
                var emailModel = new AccoutantSendMailModel
                {
                    PurchaseRequestName = data.PurchaseRequestName,
                    Register = data.UserName!,
                    Sender = accountantName,
                    Note = data.RejectReason,
                    PoLineItems = lineItems,
                    CreatedDate = data.CreatedDate.ToString("dd/MM/yyyy"),
                    IsUrgent = data.IsUrgent ? needUrgently : normally,
                    ProjectName = data.ProjectName ?? "",
                    EstimateDate = data.EstimateDate.HasValue ? data.EstimateDate.Value.ToString("dd/MM/yyyy") : "",
                    ReviewStatus = CommonHelper.GetDescription((EPurchaseRequestStatus)data.ReviewStatus)
                };

                emails.Add(data.Email);

                var objSendMail = new ObjSendMail()
                {
                    FileName = "PurchaseRequestAccountantToUserTemplate.html",
                    Mail_To = emails,
                    Title = "[Purchase Request] Yêu cầu cấp/mua hàng bị từ chối",
                    Mail_cc = new List<string>(),
                    JsonObject = JsonConvert.SerializeObject(emailModel)
                };
                await _sendMailDynamicTemplateService.SendMailAsync(objSendMail);
            }
        }

        public async Task<CombineResponseModel<int>> DirectorPrepareReviewAsync(int id, DirectorReviewModel request)
        {
            var res = new CombineResponseModel<int>();
            if (request.ReviewStatus != EPurchaseRequestStatus.DirectorApproved &&
                request.ReviewStatus != EPurchaseRequestStatus.DirectorUpdateRequest &&
                request.ReviewStatus != EPurchaseRequestStatus.DirectorRejected)
            {
                res.ErrorMessage = "Trạng thái duyệt không hợp lệ";
                return res;
            }

            if (string.IsNullOrEmpty(request.ReviewNote))
            {
                if (request.ReviewStatus == EPurchaseRequestStatus.DirectorUpdateRequest)
                {
                    res.ErrorMessage = "Yêu cầu cập nhật không được trống";
                    return res;
                }
                else if (request.ReviewStatus == EPurchaseRequestStatus.DirectorRejected)
                {
                    res.ErrorMessage = "Lý do từ chối không được trống";
                    return res;
                }
            }

            // Director chỉ duyệt/yêu cầu cập nhật đơn có purchase order hoặc export bill
            if (request.ReviewStatus == EPurchaseRequestStatus.DirectorApproved || request.ReviewStatus == EPurchaseRequestStatus.DirectorUpdateRequest)
            {
                if ((request.ReviewPurchaseOrderModels == null || !request.ReviewPurchaseOrderModels.Any()) && (request.ReviewExportBillModels == null || !request.ReviewExportBillModels.Any()))
                {
                    res.ErrorMessage = "Không thể duyệt yêu cầu không có phiếu đặt hàng và phiếu xuất kho";
                    return res;
                }
            }

            var purchaseRequest = await _unitOfWork.PurchaseRequestRepository.GetByIdAsync(id);
            if (purchaseRequest == null)
            {
                res.ErrorMessage = "Đơn đặt hàng không tồn tại";
                return res;
            }

            if (purchaseRequest.ReviewStatus != (int)EPurchaseRequestStatus.AccountantApproved && purchaseRequest.ReviewStatus != (int)EPurchaseRequestStatus.DirectorUpdateRequest)
            {
                res.ErrorMessage = "Trạng thái đơn không cho phép duyệt";
                return res;
            }

            var poIds = new List<int>();
            // Kiểm tra đã đủ số lượng hay chưa?
            if (request.ReviewStatus == EPurchaseRequestStatus.DirectorApproved)
            {
                var quantityFromRequest = purchaseRequest.PurchaseRequestLineItems.Sum(o => o.Quantity);
                var quantityFromPoAndEb = request.ReviewPurchaseOrderModels?.Sum(o => o.PurchaseQuantity) + request.ReviewExportBillModels?.Sum(o => o.ExportQuantity);
                if (quantityFromRequest != quantityFromPoAndEb)
                {
                    res.ErrorMessage = "Số lượng đặt hàng và xuất kho không đầy đủ";
                    return res;
                }

                // Kiểm tra kế hoạch thanh toán của Po
                if (request.ReviewPurchaseOrderModels != null)
                {
                    poIds.AddRange(request.ReviewPurchaseOrderModels.Select(o => o.PurchaseOrderId).Distinct().ToList());
                    var paymentPlans = await _unitOfWork.PaymentPlanRepository.GetByPoIdsAsync(poIds);

                    var poPrices = await _unitOfWork.PurchaseOrderRepository.GetPoPriceByIdsAsync(poIds);
                    foreach (var poPrice in poPrices)
                    {
                        var paymentPlanByPo = paymentPlans.Where(o => o.PurchaseOrderId == poPrice.PurchaseOrderId).Sum(o => o.PaymentAmount);
                        if (paymentPlanByPo != (int)Math.Floor(poPrice.TotalPrice))
                        {
                            res.ErrorMessage = $"Chi phí kế hoạch thanh toán của PO '{CommonHelper.ToIdDisplayString("PO", poPrice.PurchaseOrderId)}' không hợp lệ";
                            return res;
                        }
                    }

                    var vendors = await _unitOfWork.VendorRepository.GetAllAsync();
                    foreach (var poModel in request.ReviewPurchaseOrderModels)
                    {
                        var vendor = vendors.Find(o => o.Id == poModel.VendorId);
                        if (vendor == null)
                        {
                            res.ErrorMessage = $"Nhà cung cấp của PO '{CommonHelper.ToIdDisplayString("PO", poModel.PurchaseOrderId)}' không tồn tại";
                            return res;
                        }
                    }
                }
            }

            // Update Status & ReviewNote
            await _unitOfWork.PurchaseRequestRepository.UpdatePurchaseRequestAsync(purchaseRequest.Id, request.ReviewNote, request.ReviewStatus);

            if (request.ReviewStatus == EPurchaseRequestStatus.DirectorRejected)
            {
                await _unitOfWork.PurchaseRequestRepository.RemovePoByPrIdAsync(id);
                await _unitOfWork.ExportBillRepository.RemoveExportBillByPrIdAsync(id);
            }
            else if (request.ReviewStatus == EPurchaseRequestStatus.DirectorApproved)
            {
                // Kiểm tra PR có trong những PO đó
                // Nếu trong po tồn tại pr có status != DirectorApprove => update status cho po = đang duyệt, ngược lại thì set status = đã duyệt
                await _unitOfWork.PurchaseOrderRepository.UpdateStatusAsync(poIds);
            }

            res.Status = true;
            res.Data = id;
            return res;
        }

        public async Task DirectorSendEmailAsync(string directorName, int purchaseRequestId)
        {
            var purchaseRequest = await _unitOfWork.PurchaseRequestRepository.GetPrDtoByPrIdAsync(purchaseRequestId);
            if (purchaseRequest == null || !purchaseRequest.Any())
            {
                return;
            }

            var lineItemIds = purchaseRequest.Select(o => o.LineItemId).Distinct().ToList();
            var ebInfos = await _unitOfWork.ExportBillRepository.GetDtoByLineItemIdsAsync(lineItemIds);

            var data = purchaseRequest.GroupBy(z => new
            {
                z.Id,
                z.UserName,
                z.PurchaseRequestName,
                z.JobTitle,
                z.DepartmentName,
                z.ReviewUserName,
                z.ProjectName,
                z.IsUrgent,
                z.EstimateDate,
                z.Note,
                z.ReviewStatus,
                z.ManagerComment,
                z.FirstHrComment,
                z.SecondHrComment,
                z.DirectorComment,
                z.RejectReason,
                z.CreatedDate,
                z.Email
            }).Select(o => new AccountantSendEmailModel
            {
                Id = o.Key.Id,
                UserName = o.Key.UserName,
                Email = o.Key.Email,
                PurchaseRequestName = o.Key.PurchaseRequestName,
                JobName = o.Key.JobTitle,
                DepartmentName = o.Key.DepartmentName,
                ReviewUserName = o.Key.ReviewUserName,
                ProjectName = o.Key.ProjectName ?? "",
                IsUrgent = o.Key.IsUrgent,
                CreatedDate = o.Key.CreatedDate,
                EstimateDate = o.Key.EstimateDate,
                Note = o.Key.Note ?? "",
                ReviewStatus = o.Key.ReviewStatus,
                ManagerComment = o.Key.ManagerComment ?? "",
                FirstHrComment = o.Key.FirstHrComment ?? "",
                SecondHrComment = o.Key.SecondHrComment ?? "",
                DirectorComment = o.Key.DirectorComment ?? "",
                RejectReason = o.Key.RejectReason ?? "",
                FileUrls = [],
                IsAllowUpdate = false,
                AccountantLineItems = o.GroupBy(t => new
                {
                    t.LineItemId,
                    t.ProductCategoryId,
                    t.ProductCategoryName,
                    t.ProductId,
                    t.ProductName,
                    t.ProductDescription,
                    t.LineItemQuantity
                }).Select(y => new AccountantLineItemSendEmailModel
                {
                    LineItemId = y.Key.LineItemId,
                    ProductCategoryId = y.Key.ProductCategoryId,
                    ProductCategoryName = y.Key.ProductCategoryName,
                    ProductId = y.Key.ProductId,
                    ProductName = y.Key.ProductName,
                    ProductDescription = y.Key.ProductDescription,
                    LineItemQuantity = y.Key.LineItemQuantity,
                    AccountantSubProductEmailModels = y.GroupBy(v => new
                    {
                        v.SubProductId,
                        v.SubProductName,
                        v.KitQuantity,
                        v.SubProductDescription,
                    }).Select(q => new AccountantSubProductEmailModel
                    {
                        SubProductName = q.Key.SubProductName ?? "",
                        SubProductDescription = q.Key.SubProductDescription ?? "",
                        Quantity = q.Key.KitQuantity ?? 0,
                    }).OrderBy(o => o.SubProductName).ToList(),
                    AccountantPurchaseOrderModels = y.Where(i => i.PurchaseOrderId.HasValue).GroupBy(w => w.PurchaseOrderId).Select(w => new AccountantPurchaseOrderModel
                    {
                        PoIdStr = w.Key.HasValue ? CommonHelper.ToIdDisplayString("PO", w.Key.Value) : "",
                        Price = w.FirstOrDefault()?.Price ?? 0,
                        VendorName = w.FirstOrDefault()?.VendorName,
                        PurchaseOrderQuantity = w.FirstOrDefault()?.PurchaseQuantity ?? 0,
                    }).OrderBy(o => o.PoIdStr).ToList(),
                    AccountantExportBillModels = ebInfos.Where(o => o.LineItemId == y.Key.LineItemId).Select(r => new AccountantExportBillModel
                    {
                        ExportBillIdStr = CommonHelper.ToIdDisplayString("XK", r.ExportBillId),
                        ExportQuantity = r.ExportQuantity
                    }).ToList()
                }).OrderBy(o => o.ProductName).ToList()
            }).FirstOrDefault();

            if (data == null || (data.ReviewStatus != (int)EPurchaseRequestStatus.DirectorApproved &&
                                 data.ReviewStatus != (int)EPurchaseRequestStatus.DirectorUpdateRequest &&
                                 data.ReviewStatus != (int)EPurchaseRequestStatus.DirectorRejected))
            {
                return;
            }

            var lineItems = string.Empty;
            var lineItemsWhenApproved = string.Empty;
            foreach (var lineItem in data.AccountantLineItems)
            {
                var rowSpan = CalculateRowSpan(lineItem);
                if (lineItem.AccountantSubProductEmailModels != null && lineItem.AccountantSubProductEmailModels.Count != 0)
                {
                    var index = 0; // index của AccountantSubProductEmailModels
                    if (data.ReviewStatus == (int)EPurchaseRequestStatus.DirectorApproved || data.ReviewStatus == (int)EPurchaseRequestStatus.DirectorUpdateRequest)
                    {
                        foreach (var item in lineItem.AccountantSubProductEmailModels)
                        {
                            if (item != null)
                            {
                                lineItems += $@"<tr style='border-collapse: collapse;border: 1px solid black'>";
                                if (index == 0)
                                {
                                    lineItems += $@"<td style='border-collapse: collapse;border: 1px solid black' rowSpan='{rowSpan}'>{lineItem.ProductCategoryName}</td>
                                                    <td style='border-collapse: collapse;border: 1px solid black' rowSpan='{rowSpan}'>{lineItem.ProductName}</td>
                                                    <td style='text-align:center; border-collapse: collapse;border: 1px solid black' rowSpan='{rowSpan}'>{lineItem.LineItemQuantity}</td>";
                                }
                                lineItems += $@"<td style='border-collapse: collapse;border: 1px solid black'>{item.SubProductName}</td>
                                                <td style='border-collapse: collapse;border: 1px solid black'>{item.SubProductDescription}</td>";

                                if (index == 0)
                                {
                                    if (lineItem.AccountantPurchaseOrderModels != null && lineItem.AccountantPurchaseOrderModels.Any())
                                    {
                                        lineItems += $@"<td style = 'text-align:center; border-collapse: collapse;border: 1px solid black' rowSpan='{rowSpan}'>";
                                        var poIndex = 0;
                                        foreach (var po in lineItem.AccountantPurchaseOrderModels)
                                        {
                                            lineItems += @$"<div style='border-bottom: {(poIndex < lineItem.AccountantPurchaseOrderModels.Count - 1 ? "1px solid #eceaea" : "none")}'><p>{(po.Price.HasValue ? (int)Math.Floor(po.Price.Value) : po.Price)}</p></div>";
                                            poIndex++;
                                        }
                                        lineItems += "</td>";

                                        poIndex = 0;
                                        lineItems += $@"<td style = 'text-align:center; border-collapse: collapse;border: 1px solid black' rowSpan='{rowSpan}'>";
                                        foreach (var po in lineItem.AccountantPurchaseOrderModels)
                                        {
                                            lineItems += @$"<div style='border-bottom: {(poIndex < lineItem.AccountantPurchaseOrderModels.Count - 1 ? "1px solid #eceaea" : "none")}'><p>{po.VendorName}</p></div>";
                                            poIndex++;
                                        }
                                        lineItems += "</td>";

                                        poIndex = 0;
                                        lineItems += $@"<td style = 'text-align:center; border-collapse: collapse;border: 1px solid black' rowSpan='{rowSpan}'>";
                                        foreach (var po in lineItem.AccountantPurchaseOrderModels)
                                        {
                                            lineItems += @$"<div style='border-bottom: {(poIndex < lineItem.AccountantPurchaseOrderModels.Count - 1 ? "1px solid #eceaea" : "none")}'><p>{po.PoIdStr}</p></div>";
                                            poIndex++;
                                        }
                                        lineItems += "</td>";

                                        poIndex = 0;
                                        lineItems += $@"<td style = 'text-align:center; border-collapse: collapse;border: 1px solid black' rowSpan='{rowSpan}'>";
                                        foreach (var po in lineItem.AccountantPurchaseOrderModels)
                                        {
                                            lineItems += @$"<div style='border-bottom: {(poIndex < lineItem.AccountantPurchaseOrderModels.Count - 1 ? "1px solid #eceaea" : "none")}'><p>{po.PurchaseOrderQuantity}</p></div>";
                                            poIndex++;
                                        }
                                        lineItems += "</td>";

                                        if (lineItem.AccountantExportBillModels != null && lineItem.AccountantExportBillModels.Any())
                                        {
                                            poIndex = 0;
                                            lineItems += $@"<td style = 'text-align:center; border-collapse: collapse;border: 1px solid black' rowSpan='{rowSpan}'>";
                                            foreach (var eb in lineItem.AccountantExportBillModels)
                                            {
                                                lineItems += @$"<div style='border-bottom: {(poIndex < lineItem.AccountantExportBillModels.Count - 1 ? "1px solid #eceaea" : "none")}'><p>{eb.ExportBillIdStr}</p></div>";
                                                poIndex++;
                                            }
                                            lineItems += "</td>";

                                            poIndex = 0;
                                            lineItems += $@"<td style = 'text-align:center; border-collapse: collapse;border: 1px solid black' rowSpan='{rowSpan}'>";
                                            foreach (var eb in lineItem.AccountantExportBillModels)
                                            {
                                                lineItems += @$"<div style='border-bottom: {(poIndex < lineItem.AccountantExportBillModels.Count - 1 ? "1px solid #eceaea" : "none")}'><p>{(eb.ExportQuantity == 0 ? "" : eb.ExportQuantity)}</p></div>";
                                                poIndex++;
                                            }
                                            lineItems += "</td>";
                                        }
                                        else
                                        {
                                            lineItems += $@"<td style = 'text-align:center; border-collapse: collapse;border: 1px solid black' rowSpan='{rowSpan}'>";
                                            lineItems += @$"<div style=''><p>{""}</p></div>";
                                            lineItems += "</td>";
                                            lineItems += $@"<td style = 'text-align:center; border-collapse: collapse;border: 1px solid black' rowSpan='{rowSpan}'>";
                                            lineItems += @$"<div style=''><p>{""}</p></div>";
                                            lineItems += "</td>";
                                        }
                                    }
                                    else
                                    {
                                        var poIndex = 0;
                                        lineItems += $@"<td style = 'text-align:center; border-collapse: collapse;border: 1px solid black' rowSpan='{rowSpan}'>";
                                        lineItems += @$"<div style=''><p>{""}</p></div>";
                                        lineItems += "</td>";
                                        lineItems += $@"<td style = 'text-align:center; border-collapse: collapse;border: 1px solid black' rowSpan='{rowSpan}'>";
                                        lineItems += @$"<div style=''><p>{""}</p></div>";
                                        lineItems += "</td>";
                                        lineItems += $@"<td style = 'text-align:center; border-collapse: collapse;border: 1px solid black' rowSpan='{rowSpan}'>";
                                        lineItems += @$"<div style=''><p>{""}</p></div>";
                                        lineItems += "</td>";
                                        lineItems += $@"<td style = 'text-align:center; border-collapse: collapse;border: 1px solid black' rowSpan='{rowSpan}'>";
                                        lineItems += @$"<div style=''><p>{""}</p></div>";
                                        lineItems += "</td>";

                                        if (lineItem.AccountantExportBillModels != null && lineItem.AccountantExportBillModels.Any())
                                        {
                                            poIndex = 0;
                                            lineItems += $@"<td style = 'text-align:center; border-collapse: collapse;border: 1px solid black' rowSpan='{rowSpan}'>";
                                            foreach (var eb in lineItem.AccountantExportBillModels)
                                            {
                                                lineItems += @$"<div style='border-bottom: {(poIndex < lineItem.AccountantExportBillModels.Count - 1 ? "1px solid #eceaea" : "none")}'><p>{eb.ExportBillIdStr}</p></div>";
                                                poIndex++;
                                            }
                                            lineItems += "</td>";

                                            poIndex = 0;
                                            lineItems += $@"<td style = 'text-align:center; border-collapse: collapse;border: 1px solid black' rowSpan='{rowSpan}'>";
                                            foreach (var eb in lineItem.AccountantExportBillModels)
                                            {
                                                lineItems += @$"<div style='border-bottom: {(poIndex < lineItem.AccountantExportBillModels.Count - 1 ? "1px solid #eceaea" : "none")}'><p>{(eb.ExportQuantity == 0 ? "" : eb.ExportQuantity)}</p></div>";
                                                poIndex++;
                                            }
                                            lineItems += "</td>";
                                        }
                                        else
                                        {
                                            lineItems += $@"<td style = 'text-align:center; border-collapse: collapse;border: 1px solid black' rowSpan='{rowSpan}'>";
                                            lineItems += @$"<div style=''><p>{""}</p></div>";
                                            lineItems += "</td>";
                                            lineItems += $@"<td style = 'text-align:center; border-collapse: collapse;border: 1px solid black' rowSpan='{rowSpan}'>";
                                            lineItems += @$"<div style=''><p>{""}</p></div>";
                                            lineItems += "</td>";
                                        }
                                    }
                                }

                                // LineItem của Email director approve
                                lineItemsWhenApproved += $@"<tr style='border-collapse: collapse;border: 1px solid black'>";
                                if (index == 0)
                                {
                                    lineItemsWhenApproved += $@"<td style='border-collapse: collapse;border: 1px solid black' rowSpan='{lineItem.AccountantSubProductEmailModels.Count}'>{lineItem.ProductCategoryName}</td>
                                                <td style='border-collapse: collapse;border: 1px solid black' rowSpan='{lineItem.AccountantSubProductEmailModels.Count}'>{lineItem.ProductName}</td>
                                                <td style='text-align:center; border-collapse: collapse;border: 1px solid black' rowSpan='{lineItem.AccountantSubProductEmailModels.Count}'>{lineItem.LineItemQuantity}</td>";
                                }
                                lineItemsWhenApproved += $@"<td style='border-collapse: collapse;border: 1px solid black'>{item.SubProductName}</td>
                                            <td style='border-collapse: collapse;border: 1px solid black'>{item.SubProductDescription}</td>
                                        </tr>";
                                index++;
                            }
                        }
                    }
                    else if (data.ReviewStatus == (int)EPurchaseRequestStatus.DirectorRejected)
                    {
                        foreach (var item in lineItem.AccountantSubProductEmailModels)
                        {
                            lineItems += $@"<tr style='border-collapse: collapse;border: 1px solid black'>";
                            if (index == 0)
                            {
                                lineItems += $@"<td style='border-collapse: collapse;border: 1px solid black' rowSpan='{lineItem.AccountantSubProductEmailModels.Count}'>{lineItem.ProductCategoryName}</td>
                                                <td style='border-collapse: collapse;border: 1px solid black' rowSpan='{lineItem.AccountantSubProductEmailModels.Count}'>{lineItem.ProductName}</td>
                                                <td style='text-align:center; border-collapse: collapse;border: 1px solid black' rowSpan='{lineItem.AccountantSubProductEmailModels.Count}'>{lineItem.LineItemQuantity}</td>";
                            }
                            lineItems += $@"<td style='border-collapse: collapse;border: 1px solid black'>{item.SubProductName}</td>
                                            <td style='border-collapse: collapse;border: 1px solid black'>{item.SubProductDescription}</td>
                                            <td style='text-align:center; border-collapse: collapse;border: 1px solid black'>{(item.Quantity == 0 ? "" : item.Quantity)}</td>
                                        </tr>";
                            index++;
                        }
                    }
                }
            }

            var emails = new List<string>();

            // Director yccn => gửi cho hr để update po/eb
            if (data.ReviewStatus == (int)EPurchaseRequestStatus.DirectorUpdateRequest)
            {
                var hrValue = await _unitOfWork.GlobalConfigurationRepository.GetByNameAsync(Constant.FirstHrEmail);
                if (hrValue != null && !string.IsNullOrEmpty(hrValue.Value))
                {
                    var hrEmails = hrValue.Value!.Split(",").Where(email => !string.IsNullOrEmpty(email)).Select(o => o.Trim()).ToList();
                    var emailModel = new AccoutantSendMailModel
                    {
                        PurchaseRequestName = data.PurchaseRequestName,
                        Register = data.UserName,
                        Note = data.DirectorComment ?? "",
                        Sender = boardOfDirector,
                        Receiver = backOffice,
                        ReviewLink = _frontEndDomain + Constant.HrReviewPurchaseRequestPath,
                        PoLineItems = lineItems,
                        CreatedDate = data.CreatedDate.ToString("dd/MM/yyyy"),
                        IsUrgent = data.IsUrgent ? needUrgently : normally,
                        ProjectName = data.ProjectName ?? "",
                        EstimateDate = data.EstimateDate.HasValue ? data.EstimateDate.Value.ToString("dd/MM/yyyy") : "",
                        ReviewStatus = CommonHelper.GetDescription((EPurchaseRequestStatus)data.ReviewStatus)
                    };

                    emails.AddRange(hrEmails);

                    var objSendMail = new ObjSendMail()
                    {
                        FileName = "PurchaseRequestDirectorToHrTemplate.html",
                        Mail_To = emails,
                        Title = "[Purchase Order] Yêu cầu cập nhật đơn mua hàng",
                        Mail_cc = new List<string>(),
                        JsonObject = JsonConvert.SerializeObject(emailModel)
                    };

                    await _sendMailDynamicTemplateService.SendMailAsync(objSendMail);
                }
            }
            // Director approved => gửi cho hr & accountant để đi mua hàng 
            else if (data.ReviewStatus == (int)EPurchaseRequestStatus.DirectorApproved)
            {
                var accountantValue = await _unitOfWork.GlobalConfigurationRepository.GetByNameAsync(Constant.SecondHrEmail);
                if (accountantValue != null && !string.IsNullOrEmpty(accountantValue.Value))
                {
                    var accountantEmails = accountantValue.Value!.Split(",").Where(email => !string.IsNullOrEmpty(email)).Select(o => o.Trim()).ToList();

                    emails.AddRange(accountantEmails);
                }

                var hrValue = await _unitOfWork.GlobalConfigurationRepository.GetByNameAsync(Constant.FirstHrEmail);
                if (hrValue != null && !string.IsNullOrEmpty(hrValue.Value))
                {
                    var hrValueEmails = hrValue.Value!.Split(",").Where(email => !string.IsNullOrEmpty(email)).Select(o => o.Trim()).ToList();

                    emails.AddRange(hrValueEmails);
                }
                var emailModel = new AccoutantSendMailModel
                {
                    PurchaseRequestName = data.PurchaseRequestName,
                    Register = data.UserName!,
                    Note = "",
                    Sender = boardOfDirector,
                    Receiver = backOffice,
                    ReviewLink = "",
                    PoLineItems = lineItems,
                    CreatedDate = data.CreatedDate.ToString("dd/MM/yyyy"),
                    IsUrgent = data.IsUrgent ? needUrgently : normally,
                    ProjectName = data.ProjectName ?? "",
                    EstimateDate = data.EstimateDate.HasValue ? data.EstimateDate.Value.ToString("dd/MM/yyyy") : "",
                    ReviewStatus = CommonHelper.GetDescription((EPurchaseRequestStatus)data.ReviewStatus)
                };

                emails = emails.Distinct().ToList();
                var objSendMail = new ObjSendMail()
                {
                    FileName = "PurchaseRequestDirectorToAccountantTemplate.html",
                    Mail_To = emails,
                    Title = "[Purchase Order] Yêu cầu duyệt đơn mua hàng",
                    Mail_cc = new List<string>(),
                    JsonObject = JsonConvert.SerializeObject(emailModel)
                };

                await _sendMailDynamicTemplateService.SendMailAsync(objSendMail);

                // Gửi mail cho nhân viên
                var emailModelDirectorToUser = new AccoutantSendMailModel
                {
                    PurchaseRequestName = data.PurchaseRequestName,
                    Register = data.UserName!,
                    Sender = boardOfDirector,
                    Note = data.RejectReason,
                    PoLineItems = lineItemsWhenApproved,
                    CreatedDate = data.CreatedDate.ToString("dd/MM/yyyy"),
                    IsUrgent = data.IsUrgent ? needUrgently : normally,
                    ProjectName = data.ProjectName ?? "",
                    EstimateDate = data.EstimateDate.HasValue ? data.EstimateDate.Value.ToString("dd/MM/yyyy") : "",
                    ReviewStatus = CommonHelper.GetDescription((EPurchaseRequestStatus)data.ReviewStatus)
                };

                var objSendMailDirectorToUser = new ObjSendMail()
                {
                    FileName = "PurchaseRequestDirectorToUserWhenApprovedTemplate.html",
                    Mail_To = [data.Email],
                    Title = "[Purchase Request] Kết quả của đơn yêu cầu cấp/mua hàng",
                    Mail_cc = new List<string>(),
                    JsonObject = JsonConvert.SerializeObject(emailModelDirectorToUser)
                };
                await _sendMailDynamicTemplateService.SendMailAsync(objSendMailDirectorToUser);

            }
            // Từ chối => gửi về lại cho nhân viên
            else
            {
                var emailModel = new AccoutantSendMailModel
                {
                    PurchaseRequestName = data.PurchaseRequestName,
                    Register = data.UserName!,
                    Sender = boardOfDirector,
                    Note = data.RejectReason,
                    PoLineItems = lineItems,
                    CreatedDate = data.CreatedDate.ToString("dd/MM/yyyy"),
                    IsUrgent = data.IsUrgent ? needUrgently : normally,
                    ProjectName = data.ProjectName ?? "",
                    EstimateDate = data.EstimateDate.HasValue ? data.EstimateDate.Value.ToString("dd/MM/yyyy") : "",
                    ReviewStatus = CommonHelper.GetDescription((EPurchaseRequestStatus)data.ReviewStatus)
                };

                emails.Add(data.Email);

                var objSendMail = new ObjSendMail()
                {
                    FileName = "PurchaseRequestDirectorToUserTemplate.html",
                    Mail_To = emails,
                    Title = "[Purchase Request] Yêu cầu cấp/mua hàng bị từ chối",
                    Mail_cc = new List<string>(),
                    JsonObject = JsonConvert.SerializeObject(emailModel)
                };
                await _sendMailDynamicTemplateService.SendMailAsync(objSendMail);
            }
        }
        public async Task<PagingResponseModel<HRPurchaseRequestPagingResponse>> HRGetAllWithPagingAsync(HRPurchaseRequestPagingRequest model)
        {
            var responseRaw = await _unitOfWork.PurchaseRequestRepository.HRGetAllWithPagingAsync(model);
            var responses = responseRaw != null ? responseRaw.Select(x =>
            {
                var request = new HRPurchaseRequestPagingResponse
                {
                    StringId = CommonHelper.ToIdDisplayString("PR", x.Id),
                    Id = x.Id,
                    CreatedDate = x.CreatedDate,
                    DepartmentName = x.DepartmentName,
                    Progress = (x.QuantityFromEB + x.QuantityFromPO).ToString() + "/" + x.TotalRequestQuantity,
                    FromEBs = !string.IsNullOrEmpty(x.FromEB) ? x.FromEB.Split(",").Distinct().ToList().Select(t =>
                    {
                        var display = new IdDisplay
                        {
                            Id = int.Parse(t),
                            StringId = CommonHelper.ToIdDisplayString("XK", int.Parse(t))
                        };
                        return display;
                    }).ToList() : new List<IdDisplay>(),
                    FromPOs = !string.IsNullOrEmpty(x.FromPO) ? x.FromPO.Split(",").Distinct().ToList().Select(y =>
                    {
                        var display = new IdDisplay
                        {
                            Id = int.Parse(y),
                            StringId = CommonHelper.ToIdDisplayString("PO", int.Parse(y))
                        };
                        return display;
                    }).ToList() : new List<IdDisplay>(),
                    FullName = x.FullName,
                    IsUrgent = x.IsUrgent,
                    Name = x.Name,
                    ProjectName = x.ProjectName,
                    QuantityFromEB = x.QuantityFromEB,
                    QuantityFromPO = x.QuantityFromPO,
                    ReviewStatus = x.ReviewStatus,
                    ReviewStatusName = CommonHelper.GetDescription((EPurchaseRequestStatus)(x.ReviewStatus)),
                    TotalRequestQuantity = x.TotalRequestQuantity,
                    EstimateDate = x.EstimateDate,
                    TotalRecord = x.TotalRecord,
                    IsFinished = x.QuantityFromEB + x.QuantityFromPO < x.TotalRequestQuantity ? false : true,
                    IsReceived = x.ReviewStatus == (int)EPurchaseRequestStatus.Delivered ? true : false
                };
                return request;
            }).ToList() : new List<HRPurchaseRequestPagingResponse>();

            var totalRecords = responses.Count > 0 ? responses.FirstOrDefault().TotalRecord : 0;
            var res = new PagingResponseModel<HRPurchaseRequestPagingResponse>
            {
                Items = responses,
                TotalRecord = totalRecords
            };
            return res;
        }

        public async Task<CombineResponseModel<bool>> PrepareDeleteAsync(int id, int userId)
        {
            var res = new CombineResponseModel<bool>();
            var purchaseRequest = await _unitOfWork.PurchaseRequestRepository.GetByIdAsync(id);
            if (purchaseRequest == null)
            {
                res.ErrorMessage = "Đơn đặt hàng không tồn tại";
                return res;
            }

            if (purchaseRequest.UserId != userId)
            {
                res.ErrorMessage = "Đơn đặt hàng không thuộc về bạn";
                return res;
            }

            if (purchaseRequest.ReviewStatus != (int)EPurchaseRequestStatus.Pending && purchaseRequest.ReviewStatus != (int)EPurchaseRequestStatus.Cancelled)
            {
                res.ErrorMessage = "Tình trạng đơn hàng không cho phép xóa";
                return res;
            }

            await _unitOfWork.PurchaseRequestAttachmentRepository.DeleteRangeAsync(purchaseRequest.PurchaseRequestAttachments.ToList());
            await _unitOfWork.PurchaseRequestLineItemRepository.DeleteRangeAsync(purchaseRequest.PurchaseRequestLineItems.ToList());
            await _unitOfWork.PurchaseRequestRepository.DeleteAsync(purchaseRequest);
            await _unitOfWork.SaveChangesAsync();

            res.Status = true;
            res.Data = true;
            return res;

        }

        public async Task<CombineResponseModel<List<int>>> AdminPrepareReviewAsync(AdminMultipleRequestReviewModel request)
        {
            // Chức năng này dùng để đồng ý nhiều purchase request
            var res = new CombineResponseModel<List<int>>();
            if (request.IsDirector && request.ReviewStatus != EPurchaseRequestStatus.DirectorApproved)
            {
                res.ErrorMessage = "Trạng thái duyệt không hợp lệ";
                return res;
            }

            if (!request.IsDirector && request.ReviewStatus != EPurchaseRequestStatus.AccountantApproved)
            {
                res.ErrorMessage = "Trạng thái duyệt không đúng";
                return res;
            }

            if (request.AdminRequestReviewModels.Count == 0)
            {
                res.ErrorMessage = "Danh sách đơn cần duyệt không được trống";
                return res;
            }
            var poIds = new List<int>();
            foreach (var purchaseRequestReview in request.AdminRequestReviewModels)
            {
                // Chỉ duyệt đơn có purchase order hoặc export bill
                if ((purchaseRequestReview.ReviewPurchaseOrderModels == null || !purchaseRequestReview.ReviewPurchaseOrderModels.Any())
                    && (purchaseRequestReview.ReviewExportBillModels == null || !purchaseRequestReview.ReviewExportBillModels.Any()))
                {
                    res.ErrorMessage = "Không thể duyệt yêu cầu không có phiếu đặt hàng và phiếu xuất kho";
                    return res;
                }

                if (purchaseRequestReview.ReviewPurchaseOrderModels != null)
                {
                    purchaseRequestReview.ReviewPurchaseOrderModels.ForEach(po =>
                    {
                        if (!poIds.Contains(po.PurchaseOrderId))
                        {
                            poIds.Add(po.PurchaseOrderId);
                        }
                    });
                }
            }

            var purchaseRequestIds = request.AdminRequestReviewModels.Select(o => o.PurchaseRequestId).Distinct().ToList();
            var purchaseRequests = await _unitOfWork.PurchaseRequestRepository.GetByIdsAsync(purchaseRequestIds);
            if (purchaseRequests == null)
            {
                res.ErrorMessage = "Danh sách đơn yêu cầu cấp/mua hàng không tồn tại";
                return res;
            }

            if (purchaseRequests.Count != purchaseRequestIds.Count)
            {
                res.ErrorMessage = "Có đơn yêu cầu cấp/mua hàng không tồn tại";
                return res;
            }

            var paymentPlans = await _unitOfWork.PaymentPlanRepository.GetByPoIdsAsync(poIds);
            var poPrices = await _unitOfWork.PurchaseOrderRepository.GetPoPriceByIdsAsync(poIds);
            var vendors = await _unitOfWork.VendorRepository.GetAllAsync();

            foreach (var purchaseRequest in purchaseRequests)
            {
                if (!IsAllowMultipleReview((EPurchaseRequestStatus)purchaseRequest.ReviewStatus, request.IsDirector))
                {
                    res.ErrorMessage = $"Trạng thái của đơn '{purchaseRequest.Name}' không cho phép duyệt";
                    return res;
                }

                var purchaseRequestReview = request.AdminRequestReviewModels.Find(o => o.PurchaseRequestId == purchaseRequest.Id);
                if (purchaseRequestReview == null)
                {
                    res.ErrorMessage = $"Đơn '{purchaseRequest.Name}' không có phiếu đặt hàng hoặc phiếu xuất kho";
                    return res;
                }

                // Kiểm tra đã đủ số lượng hay chưa?
                var reviewModel = request.AdminRequestReviewModels.Find(o => o.PurchaseRequestId == purchaseRequest.Id);
                var quantityFromPo = reviewModel?.ReviewPurchaseOrderModels?.Sum(z => z.PurchaseQuantity) ?? 0;
                var quantityFromEb = reviewModel?.ReviewExportBillModels?.Sum(z => z.ExportQuantity) ?? 0;
                var quantityFromPoAndEb = quantityFromPo + quantityFromEb;
                if (purchaseRequest.TotalLineItemQuantity != quantityFromPoAndEb)
                {
                    res.ErrorMessage = $"Số lượng đặt hàng và xuất kho của đơn '{purchaseRequest.Name}' không đầy đủ";
                    return res;
                }

                // Kiểm tra kế hoạch thanh toán của Po
                foreach (var poModel in request.AdminRequestReviewModels.Select(o => o.ReviewPurchaseOrderModels))
                {
                    if (poModel != null && poModel.Any())
                    {
                        foreach (var po in poModel)
                        {
                            var poPrice = poPrices.Find(o => o.PurchaseOrderId == po.PurchaseOrderId);
                            if (poPrice != null)
                            {
                                var paymentPlanByPo = paymentPlans.Where(o => o.PurchaseOrderId == poPrice.PurchaseOrderId).Sum(o => o.PaymentAmount);
                                if (paymentPlanByPo != (int)Math.Floor(poPrice.TotalPrice))
                                {
                                    res.ErrorMessage = $"Chi phí kế hoạch thanh toán của PO '{CommonHelper.ToIdDisplayString("PO", poPrice.PurchaseOrderId)}' không hợp lệ";
                                    return res;
                                }
                            }
                            // Kiểm tra nhà cung cấp
                            var vendor = vendors.Find(o => o.Id == po.VendorId);
                            if (vendor == null)
                            {
                                res.ErrorMessage = $"Nhà cung cấp của PO '{CommonHelper.ToIdDisplayString("PO", po.PurchaseOrderId)}' không tồn tại";
                                return res;
                            }
                        }
                    }
                }
            }

            await _unitOfWork.PurchaseRequestRepository.AdminReviewPurchaseRequestsAsync(purchaseRequestIds, request.ReviewStatus);
            await _unitOfWork.PurchaseOrderRepository.UpdateStatusAsync(poIds);
            res.Status = true;
            res.Data = purchaseRequestIds;
            return res;
        }
        public async Task<CombineResponseModel<PurchaseRequest>> ChangeStatusAsync(int id)
        {
            var res = new CombineResponseModel<PurchaseRequest>();
            var purchaseRequest = await _unitOfWork.PurchaseRequestRepository.GetByIdAsync(id);
            if(purchaseRequest == null)
            {
                res.ErrorMessage = "Không tồn tại đơn yêu cầu";
                return res;
            }
            if(purchaseRequest.ReviewStatus != (int)EPurchaseRequestStatus.DirectorApproved)
            {
                if(purchaseRequest.ReviewStatus == (int)EPurchaseRequestStatus.Delivered)
                {
                    res.ErrorMessage = "Yêu cầu đã nhận hàng";
                    return res;
                }
                res.ErrorMessage = "Yêu cầu chưa được giám đốc duyệt";
                return res;
            }
            purchaseRequest.ReviewStatus = (int)EPurchaseRequestStatus.Delivered;
            purchaseRequest.UpdatedDate = DateTime.UtcNow.UTCToIct();
            res.Status = true;
            res.Data = purchaseRequest;
            return res;
        }
    }
}