using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Enums;
using InternalPortal.ApplicationCore.Helpers;
using InternalPortal.ApplicationCore.Interfaces;
using InternalPortal.ApplicationCore.Interfaces.Business;
using InternalPortal.ApplicationCore.Interfaces.Email;
using InternalPortal.ApplicationCore.Interfaces.Utilities.AzureBlob;
using InternalPortal.ApplicationCore.Models;
using InternalPortal.ApplicationCore.Models.PaymentRequest;
using Newtonsoft.Json;
using Org.BouncyCastle.Asn1.Crmf;
using System.Globalization;
using System.Linq;
using static InternalPortal.ApplicationCore.ValueObjects.Global;

namespace InternalPortal.Infrastructure.Services.Business
{
    public class PaymentRequestService : IPaymentRequestService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IBlobService _blobService;
        private readonly ISendMailDynamicTemplateService _sendMailDynamicTemplateService;
        public PaymentRequestService(IUnitOfWork unitOfWork,
            IBlobService blobService,
            ISendMailDynamicTemplateService sendMailDynamicTemplateService)
        {
            _unitOfWork = unitOfWork;
            _blobService = blobService;
            _sendMailDynamicTemplateService = sendMailDynamicTemplateService;
        }
        #region Private Method
        public EPaymentStatus GetPaymentStatus(List<string> paymentStatuses)
        {
            var status = 0;
            if (paymentStatuses.Count == 0)
            {
                status = (int)EPaymentStatus.NotPaid;
                return (EPaymentStatus)status;
            }
            var statuses = paymentStatuses.Select(t => t == "1" ? true : false).ToList();
            if (statuses.All(t => t))
            {
                status = (int)EPaymentStatus.Paid;
                return (EPaymentStatus)status;
            }
            if (statuses.All(t => !t))
            {
                status = (int)EPaymentStatus.NotPaid;
                return (EPaymentStatus)status;
            }
            if (statuses.Any(t => t))
            {
                status = (int)EPaymentStatus.PartialPaid;
                return (EPaymentStatus)status;
            }
            return (EPaymentStatus)status;
        }
        #endregion
        public async Task<CombineResponseModel<PaymentRequest>> CreateAsync(PaymentRequestCreateModel model, int userId, string fullName)
        {
            var res = new CombineResponseModel<PaymentRequest>();
            if (model.TypeId == 0 && string.IsNullOrEmpty(model.OtherType))
            {
                res.ErrorMessage = "Vui lòng ghi rõ loại mục đích thanh toán";
                return res;
            }
            if (model.PaymentMethod == (int)EPaymentMethod.Card)
            {
                if (string.IsNullOrEmpty(model.BankName) || string.IsNullOrEmpty(model.BankNumber))
                {
                    res.ErrorMessage = "Vui lòng nhập đủ thông tin thanh toán";
                    return res;
                }
            }
            if (model.VendorId.HasValue)
            {
                var vendor = await _unitOfWork.VendorRepository.GetByIdAsync(model.VendorId.Value);
                if (vendor == null)
                {
                    res.ErrorMessage = "Không tồn tại nhà cung cấp";
                    return res;
                }
                var userVendors = await _unitOfWork.UserVendorRepository.GetByUserIdAsync(userId);
                var isAllowToUserVendor = userVendors.Count > 0 ? userVendors.Any(t => t.VendorId == vendor.Id) ? true : false : false;
                if (!isAllowToUserVendor)
                {
                    res.ErrorMessage = "Nhà cung cấp không hợp lệ";
                    return res;
                }
            }
            if (model.DepartmentId.HasValue)
            {
                var department = await _unitOfWork.DepartmentRepository.GetByIdAsync(model.DepartmentId.Value);
                if (department == null)
                {
                    res.ErrorMessage = "Bộ phận không tồn tại";
                    return res;
                }
                var user = await _unitOfWork.UserInternalRepository.GetByIdAsync(userId);
                if (department.Id != user.DepartmentId)//nếu gửi bộ phận khác check có phải acc,hr,bod không
                {
                    var allowDepartment = new List<string>();
                    allowDepartment.Add("Accounting");
                    allowDepartment.Add("HR");
                    allowDepartment.Add("BOD");
                    var userDepartment = user.Department != null ? !string.IsNullOrEmpty(user.Department.Name) ? user.Department.Name : string.Empty : string.Empty;
                    var isHasAllowDepartment = allowDepartment.Any(t => t.Equals(userDepartment, StringComparison.OrdinalIgnoreCase));
                    if (!isHasAllowDepartment)
                    {
                        res.ErrorMessage = "Phòng ban không hợp lệ";
                        return res;
                    }
                }
            }
            var totalPriceRequest = model.PaymentRequestDetails.Sum(t => Math.Round((t.Quantity * t.Price) + (t.VatPrice == 0 ? (t.Quantity * t.Price) * t.Vat / 100 : t.VatPrice)));
            var totalPricePlan = model.PaymentRequestPlans.Sum(t => Math.Round(t.PaymentAmount));
            if (totalPriceRequest != totalPricePlan)
            {
                res.ErrorMessage = "Tổng tiền của chi tiết yêu cầu không khớp với tổng tiền các đợt thanh toán";
                return res;
            }
            var reviewUser = await _unitOfWork.UserInternalRepository.GetByIdAsync(model.ReviewUserId);
            if (reviewUser == null)
            {
                res.ErrorMessage = "Người duyệt không tồn tại";
                return res;
            }
            var proposeName = string.Empty;
            if (model.TypeId != 0)
            {
                var paymentRequestPropose = await _unitOfWork.PaymentRequestProposeRepository.GetByIdAsync(model.TypeId);
                if (paymentRequestPropose == null)
                {
                    res.ErrorMessage = "Không tồn tại mục đích thanh toán";
                    return res;
                }
                proposeName = paymentRequestPropose.Name;
            }
            var paymentRequest = new PaymentRequest()
            {
                Name = model.Name,
                CreateUserId = userId,
                ReviewStatus = model.ReviewUserId == userId ? (int)EPaymentRequestStatus.ManagerApproved : (int)EPaymentRequestStatus.Pending,
                ReviewUserId = model.ReviewUserId,
                Type = model.TypeId != 0 ? proposeName : model.OtherType,
                VendorId = model.VendorId,
                PaymentMethod = model.PaymentMethod,
                BankName = model.PaymentMethod == (int)EPaymentMethod.Card ? model.BankName : string.Empty,
                BankNumber = model.PaymentMethod == (int)EPaymentMethod.Card ? model.BankNumber : string.Empty,
                Note = model.Note,
                DepartmentId = model.DepartmentId,
                CreateDate = DateTime.UtcNow.UTCToIct()
            };
            var paymentRequestDetails = new List<PaymentRequestDetail>();
            if (model.PaymentRequestDetails.Count == 0)
            {
                res.ErrorMessage = "Chi tiết đơn yêu cầu không được trống";
                return res;
            }
            foreach (var item in model.PaymentRequestDetails)
            {
                var paymentRequestDetail = new PaymentRequestDetail()
                {
                    Name = item.Name,
                    Quantity = item.Quantity,
                    Price = Math.Round(item.Price),
                    Vat = item.Vat,
                    VatPrice = Math.Round(item.VatPrice),
                    CreateDate = DateTime.UtcNow.UTCToIct()
                };
                paymentRequestDetails.Add(paymentRequestDetail);
            }
            paymentRequest.PaymentRequestDetails = paymentRequestDetails;
            var paymentRequestPlans = new List<PaymentRequestPlan>();
            if (model.PaymentRequestPlans.Count == 0)
            {
                res.ErrorMessage = "Các đợt thanh toán không được trống";
                return res;
            }
            foreach (var plan in model.PaymentRequestPlans)
            {
                var paymentRequestPlan = new PaymentRequestPlan()
                {
                    PaymentType = plan.PaymentType,
                    PaymentStatus = false,
                    IsUrgent = plan.IsUrgent,
                    ProposePaymentDate = plan.ProposePaymentDate,
                    PaymentAmount = plan.PaymentAmount,
                    Note = plan.Note,
                    CreateDate = DateTime.UtcNow.UTCToIct()
                };
                paymentRequestPlans.Add(paymentRequestPlan);
            }
            paymentRequest.PaymentRequestPlans = paymentRequestPlans;
            var paymentRequestFiles = new List<PaymentRequestFile>();
            if (model.Files != null)
            {
                var now = DateTime.UtcNow.UTCToIct();
                foreach (var file in model.Files)
                {
                    using (var ms = new MemoryStream())
                    {
                        await file.CopyToAsync(ms);
                        var fileBytes = ms.ToArray();
                        var filename = $"{now.Year}/{now.Month}/{Guid.NewGuid().ToString()}/{CommonHelper.RemoveDiacritics(file.FileName)}";
                        var imageUrl = await _blobService.UploadAsync(fileBytes, BlobContainerName.PaymentRequest, filename, file.ContentType);
                        if (imageUrl == null)
                        {
                            res.ErrorMessage = "Tải hình lên lỗi";
                            return res;
                        }
                        var paymentRequestFile = new PaymentRequestFile()
                        {
                            Url = imageUrl.RelativeUrl,
                            CreateDate = DateTime.UtcNow.UTCToIct()
                        };
                        paymentRequestFiles.Add(paymentRequestFile);
                    }
                }
            }
            paymentRequest.PaymentRequestFiles = paymentRequestFiles;
            res.Status = true;
            res.Data = paymentRequest;
            await _unitOfWork.PaymentRequestRepository.CreateAsync(paymentRequest);
            await _unitOfWork.SaveChangesAsync();
            if (paymentRequest.CreateUserId != paymentRequest.ReviewUserId) //nếu không phải manager gửi đơn
            {
                var paymentRequestReviewSendmail = new PaymentRequestReviewSendMail()
                {
                    Name = paymentRequest.Name,
                    Reviewer = paymentRequest.ReviewUser.FullName,
                    CreateDate = DateTime.ParseExact(paymentRequest.CreateDate.ToString("dd/MM/yyyy HH:mm:ss.fff"), "dd/MM/yyyy HH:mm:ss.fff", CultureInfo.InvariantCulture),
                    CreateUser = fullName,
                    ReviewResult = CommonHelper.GetDescription((EPaymentRequestStatus)paymentRequest.ReviewStatus)
                };
                ObjSendMail objSendMail = new()
                {
                    FileName = "PaymentRequestReviewTemplate.html",
                    Mail_To = new List<string> { paymentRequest.ReviewUser.Email },
                    Title = "[Thanh toán] Yêu cầu duyệt cho đơn thanh toán: " + paymentRequest.Name,
                    Mail_cc = [],
                    JsonObject = JsonConvert.SerializeObject(paymentRequestReviewSendmail)
                };
                await _sendMailDynamicTemplateService.SendMailAsync(objSendMail);
            }
            else // manager gửi thì gửi mail cho accountant
            {
                var accountEmail = await _unitOfWork.GlobalConfigurationRepository.GetByNameAsync(Constant.PaymentRequestAccountEmails);
                if (accountEmail == null || string.IsNullOrEmpty(accountEmail.Value))
                {
                    res.ErrorMessage = "Người dùng chưa được config";
                    return res;
                }
                var accountEmails = accountEmail.Value.Split(",").ToList();
                var paymentRequestReviewSendmail = new PaymentRequestReviewSendMail()
                {
                    Name = paymentRequest.Name,
                    Reviewer = paymentRequest.ReviewUser.FullName,
                    CreateDate = DateTime.ParseExact(paymentRequest.CreateDate.ToString("dd/MM/yyyy HH:mm:ss.fff"), "dd/MM/yyyy HH:mm:ss.fff", CultureInfo.InvariantCulture),
                    CreateUser = fullName,
                    ReviewResult = CommonHelper.GetDescription((EPaymentRequestStatus)paymentRequest.ReviewStatus)
                };
                ObjSendMail objSendMail = new()
                {
                    FileName = "PaymentRequestReviewTemplate.html",
                    Mail_To = accountEmails,
                    Title = "[Thanh toán] Yêu cầu duyệt cho đơn thanh toán: " + paymentRequest.Name,
                    Mail_cc = [],
                    JsonObject = JsonConvert.SerializeObject(paymentRequestReviewSendmail)
                };
                await _sendMailDynamicTemplateService.SendMailAsync(objSendMail);
            }
            return res;
        }

        public async Task<CombineResponseModel<PaymentRequest>> DeleteAsync(int id, int userId)
        {
            var res = new CombineResponseModel<PaymentRequest>();
            var paymentRequest = await _unitOfWork.PaymentRequestRepository.GetByIdAsync(id);
            if (paymentRequest == null)
            {
                res.ErrorMessage = "Không tồn tại đơn yêu cầu";
                return res;
            }
            if (paymentRequest.CreateUserId != userId)
            {
                res.ErrorMessage = "Đây không phải đơn của bạn";
                return res;
            }
            if (paymentRequest.ReviewStatus != (int)EPaymentRequestStatus.Pending)
            {
                res.ErrorMessage = "Không thể xóa đơn đã duyệt";
                return res;
            }
            if (paymentRequest.PaymentRequestDetails.Count > 0)
            {
                await _unitOfWork.PaymentRequestDetailRepository.DeleteRangeAsync(paymentRequest.PaymentRequestDetails.ToList());
            }
            if (paymentRequest.PaymentRequestFiles.Count > 0)
            {
                await _unitOfWork.PaymentRequestFileRepository.DeleteRangeAsync(paymentRequest.PaymentRequestFiles.ToList());
            }
            if (paymentRequest.PaymentRequestPlans.Count > 0)
            {
                await _unitOfWork.PaymentRequestPlanRepository.DeleteRangeAsync(paymentRequest.PaymentRequestPlans.ToList());
            }
            res.Status = true;
            res.Data = paymentRequest;
            return res;
        }

        public async Task<CombineResponseModel<PaymentRequest>> UpdateAsync(PaymentRequestUpdateModel model, int userId, int id)
        {
            var res = new CombineResponseModel<PaymentRequest>();
            if (model.TypeId == 0 && string.IsNullOrEmpty(model.OtherType))
            {
                res.ErrorMessage = "Vui lòng ghi rõ loại mục đích thanh toán";
                return res;
            }
            if (model.PaymentMethod == (int)EPaymentMethod.Card)
            {
                if (string.IsNullOrEmpty(model.BankName) || string.IsNullOrEmpty(model.BankNumber))
                {
                    res.ErrorMessage = "Vui lòng nhập đủ thông tin thanh toán";
                    return res;
                }
            }
            if (model.DepartmentId.HasValue)
            {
                var department = await _unitOfWork.DepartmentRepository.GetByIdAsync(model.DepartmentId.Value);
                if (department == null)
                {
                    res.ErrorMessage = "Bộ phận không tồn tại";
                    return res;
                }
                var user = await _unitOfWork.UserInternalRepository.GetByIdAsync(userId);
                if (department.Id != user.DepartmentId)//nếu gửi bộ phận khác check có phải acc,hr,bod không
                {
                    var allowDepartment = new List<string>();
                    allowDepartment.Add("Accounting");
                    allowDepartment.Add("HR");
                    allowDepartment.Add("BOD");
                    var userDepartment = user.Department != null ? !string.IsNullOrEmpty(user.Department.Name) ? user.Department.Name : string.Empty : string.Empty;
                    var isHasAllowDepartment = allowDepartment.Any(t => t.Equals(userDepartment, StringComparison.OrdinalIgnoreCase));
                    if (!isHasAllowDepartment)
                    {
                        res.ErrorMessage = "Phòng ban không hợp lệ";
                        return res;
                    }
                }
            }
            var reviewUser = await _unitOfWork.UserInternalRepository.GetByIdAsync(model.ReviewUserId);
            if (reviewUser == null)
            {
                res.ErrorMessage = "Người duyệt không tồn tại";
                return res;
            }
            var paymentRequest = await _unitOfWork.PaymentRequestRepository.GetByIdAsync(id);
            if (paymentRequest == null)
            {
                res.ErrorMessage = "Không tồn tại đơn yêu cầu";
                return res;
            }
            var isClose = paymentRequest.Isclose ?? false;
            if (isClose)
            {
                res.ErrorMessage = "Đơn đã đóng";
                return res;
            }
            if (paymentRequest.CreateUserId != userId)
            {
                res.ErrorMessage = "Đây không phải đơn của bạn";
                return res;
            }
            var statusAllowUpdate = new List<EPaymentRequestStatus>()
            {
                EPaymentRequestStatus.Pending,
                EPaymentRequestStatus.AccountantUpdateRequest,
                EPaymentRequestStatus.DirectorUpdateRequest
            };
            if (!statusAllowUpdate.Contains((EPaymentRequestStatus)paymentRequest.ReviewStatus) 
                || (paymentRequest.ReviewStatus == (int)EPaymentRequestStatus.ManagerApproved && paymentRequest.CreateUserId != paymentRequest.ReviewUserId))
            {
                res.ErrorMessage = "Không thể cập nhật đơn đã duyệt";
                return res;
            }
            if (model.VendorId.HasValue)
            {
                var vendor = await _unitOfWork.VendorRepository.GetByIdAsync(model.VendorId.Value);
                if (vendor == null)
                {
                    res.ErrorMessage = "Không tồn tại nhà cung cấp";
                    return res;
                }
                var userVendors = await _unitOfWork.UserVendorRepository.GetByUserIdAsync(userId);
                var isAllowToUserVendor = userVendors.Count > 0 ? userVendors.Any(t => t.VendorId == vendor.Id) ? true : false : false;
                if (!isAllowToUserVendor)
                {
                    res.ErrorMessage = "Nhà cung cấp không hợp lệ";
                    return res;
                }
            }
            var proposeName = string.Empty;
            if (model.TypeId != 0)
            {
                var paymentRequestPropose = await _unitOfWork.PaymentRequestProposeRepository.GetByIdAsync(model.TypeId);
                if (paymentRequestPropose == null)
                {
                    res.ErrorMessage = "Không tồn tại mục đích thanh toán";
                    return res;
                }
                proposeName = paymentRequestPropose.Name;
            }
            var ogReviewUserId = paymentRequest.ReviewUserId;
            var ogReviewStatus = paymentRequest.ReviewStatus;
            paymentRequest.Name = model.Name;
            paymentRequest.ReviewUserId = model.ReviewUserId;
            paymentRequest.Type = model.TypeId != 0 ? proposeName : model.OtherType;
            paymentRequest.Note = model.Note;
            paymentRequest.ReviewStatus = paymentRequest.ReviewStatus == (int)EPaymentRequestStatus.AccountantUpdateRequest
                || paymentRequest.ReviewStatus == (int)EPaymentRequestStatus.DirectorUpdateRequest
                ? (int)EPaymentRequestStatus.Pending : (int)EPaymentRequestStatus.Pending;
            paymentRequest.VendorId = model.VendorId;
            paymentRequest.PaymentMethod = model.PaymentMethod;
            paymentRequest.BankName = model.BankName ?? string.Empty;
            paymentRequest.BankNumber = model.BankNumber ?? string.Empty;
            paymentRequest.DepartmentId = model.DepartmentId;
            paymentRequest.UpdateDate = DateTime.UtcNow.UTCToIct();
            if (paymentRequest.ReviewUserId == paymentRequest.CreateUserId)//nếu cập nhật người duyệt là bản thân
            {
                var totalPriceRequest = paymentRequest.PaymentRequestDetails.Sum(t => Math.Round((decimal)((t.Quantity * t.Price) + (t.VatPrice == 0 ? t.Quantity * t.Price * t.Vat / 100 : t.VatPrice))));
                var totalPricePlan = paymentRequest.PaymentRequestPlans.Sum(t => Math.Round(t.PaymentAmount));
                if (totalPriceRequest != totalPricePlan)
                {
                    res.ErrorMessage = "Tổng tiển chi tiết yêu cầu không khớp với tổng tiển các đợt thanh toán";
                    return res;
                }
                paymentRequest.ReviewStatus = (int)EPaymentRequestStatus.ManagerApproved;
            }
            var listFileId = !string.IsNullOrEmpty(model.FileIds) ? model.FileIds.Split(",").Select(t => int.Parse(t)).ToList() : new List<int>();
            // khi trên FE khi xóa 1 file nào đó sẽ lưu id file sẽ xóa vào FileIds và sẽ xóa ở db những file có id nằm trong FileIds
            if (listFileId.Count > 0)
            {
                var listFileDelete = paymentRequest.PaymentRequestFiles.Where(t => listFileId.Contains(t.Id)).ToList();
                if (listFileDelete.Count() > 0)
                {
                    await _unitOfWork.PaymentRequestFileRepository.DeleteRangeAsync(listFileDelete);
                }
            }
            var paymentRequestFiles = new List<PaymentRequestFile>();
            if (model.Files != null)
            {
                var now = DateTime.UtcNow.UTCToIct();
                foreach (var file in model.Files)
                {
                    using (var ms = new MemoryStream())
                    {
                        await file.CopyToAsync(ms);
                        var fileBytes = ms.ToArray();
                        var filename = $"{now.Year}/{now.Month}/{Guid.NewGuid().ToString()}/{CommonHelper.RemoveDiacritics(file.FileName)}";
                        var imageUrl = await _blobService.UploadAsync(fileBytes, BlobContainerName.PaymentRequest, filename, file.ContentType);
                        if (imageUrl == null)
                        {
                            res.ErrorMessage = "Tải hình lên lỗi";
                            return res;
                        }
                        var paymentRequestFile = new PaymentRequestFile()
                        {
                            PaymentRequestId = paymentRequest.Id,
                            Url = imageUrl.RelativeUrl,
                            CreateDate = DateTime.UtcNow.UTCToIct()
                        };
                        paymentRequestFiles.Add(paymentRequestFile);
                    }
                }
            }
            if (paymentRequestFiles.Count > 0)
            {
                await _unitOfWork.PaymentRequestFileRepository.CreateRangeAsync(paymentRequestFiles);
            }
            res.Status = true;
            res.Data = paymentRequest;
            await _unitOfWork.PaymentRequestRepository.UpdateAsync(paymentRequest);
            await _unitOfWork.SaveChangesAsync();
            if (ogReviewUserId != model.ReviewUserId || ogReviewStatus == (int)EPaymentRequestStatus.AccountantUpdateRequest
                || ogReviewStatus == (int)EPaymentRequestStatus.DirectorUpdateRequest)//nếu update lại người duyệt là người khác hoặc có yêu cầu cập nhật thì gửi mail 
            {
                //nếu người gửi là manager thì gửi mail cho Accountant
                if (paymentRequest.CreateUserId == paymentRequest.ReviewUserId)
                {
                    var accountEmail = await _unitOfWork.GlobalConfigurationRepository.GetByNameAsync(Constant.PaymentRequestAccountEmails);
                    if (accountEmail == null || string.IsNullOrEmpty(accountEmail.Value))
                    {
                        res.ErrorMessage = "Người dùng chưa được config";
                        return res;
                    }
                    var accountEmails = accountEmail.Value.Split(',').ToList();
                    var paymentRequestReviewSendmail = new PaymentRequestReviewSendMail()
                    {
                        Name = paymentRequest.Name,
                        Reviewer = paymentRequest.ReviewUser.FullName,
                        CreateDate = DateTime.ParseExact(paymentRequest.CreateDate.ToString("dd/MM/yyyy HH:mm:ss.fff"), "dd/MM/yyyy HH:mm:ss.fff", CultureInfo.InvariantCulture),
                        CreateUser = paymentRequest.CreateUser.FullName,
                        ReviewResult = CommonHelper.GetDescription((EPaymentRequestStatus)paymentRequest.ReviewStatus)
                    };
                    ObjSendMail objSendMail = new()
                    {
                        FileName = "PaymentRequestReviewTemplate.html",
                        Mail_To = accountEmails,
                        Title = "[Thanh toán] Yêu cầu duyệt cho đơn thanh toán: " + paymentRequest.Name,
                        Mail_cc = [],
                        JsonObject = JsonConvert.SerializeObject(paymentRequestReviewSendmail)
                    };
                    await _sendMailDynamicTemplateService.SendMailAsync(objSendMail);
                }
                else //ngược lại gửi cho manager
                {
                    var paymentRequestReviewSendmail = new PaymentRequestReviewSendMail()
                    {
                        Name = paymentRequest.Name,
                        Reviewer = paymentRequest.ReviewUser.FullName,
                        CreateDate = DateTime.ParseExact(paymentRequest.CreateDate.ToString("dd/MM/yyyy HH:mm:ss.fff"), "dd/MM/yyyy HH:mm:ss.fff", CultureInfo.InvariantCulture),
                        CreateUser = paymentRequest.CreateUser.FullName,
                        ReviewResult = CommonHelper.GetDescription((EPaymentRequestStatus)paymentRequest.ReviewStatus)
                    };
                    ObjSendMail objSendMail = new()
                    {
                        FileName = "PaymentRequestReviewTemplate.html",
                        Mail_To = new List<string> { paymentRequest.ReviewUser.Email },
                        Title = "[Thanh toán] Yêu cầu duyệt cho đơn thanh toán: " + paymentRequest.Name,
                        Mail_cc = [],
                        JsonObject = JsonConvert.SerializeObject(paymentRequestReviewSendmail)
                    };
                    await _sendMailDynamicTemplateService.SendMailAsync(objSendMail);
                }
            }
            return res;
        }
        public async Task<CombineResponseModel<List<PaymentRequestFile>>> AddFileAsync(int id, int userId, PaymentRequestFileRequest request)
        {
            var res = new CombineResponseModel<List<PaymentRequestFile>>();
            var paymentRequest = await _unitOfWork.PaymentRequestRepository.GetByIdAsync(id);
            if (paymentRequest == null)
            {
                res.ErrorMessage = "Không tồn tại đơn yêu cầu";
                return res;
            }
            if (paymentRequest.CreateUserId != userId)
            {
                res.ErrorMessage = "Đây không phải đơn của bạn";
                return res;
            }
            if (paymentRequest.ReviewStatus != (int)EPaymentRequestStatus.Pending)
            {
                res.ErrorMessage = "Không thể cập nhật đơn đã duyệt";
                return res;
            }
            // khi trên FE khi xóa 1 file nào đó sẽ lưu id file sẽ xóa vào FileUrlIds và sẽ xóa ở db những file có id nằm trong FileUrlIds
            var paymentRequestFileIds = !string.IsNullOrEmpty(request.FileUrlIds) ? request.FileUrlIds.Split(",").ToList() : new List<string>();
            if (paymentRequestFileIds.Count > 0)
            {
                //foreach (var item in paymentRequestFileIds)
                //{
                //    var paymentRequestFileExist = paymentRequest.PaymentRequestFiles.FirstOrDefault(t => t.Id == int.Parse(item));
                //    if (paymentRequestFileExist != null)
                //    {
                //        await _unitOfWork.PaymentRequestFileRepository.DeleteAsync(paymentRequestFileExist);
                //    }
                //}
                var listFileDelete = paymentRequest.PaymentRequestFiles.Where(t => paymentRequestFileIds.Contains(t.Id.ToString())).ToList();
                if (listFileDelete.Count() > 0)
                {
                    await _unitOfWork.PaymentRequestFileRepository.DeleteRangeAsync(listFileDelete);
                }
            }
            var paymentRequestFiles = new List<PaymentRequestFile>();
            if (request.Files != null)
            {
                var now = DateTime.UtcNow.UTCToIct();
                foreach (var file in request.Files)
                {
                    using (var ms = new MemoryStream())
                    {
                        await file.CopyToAsync(ms);
                        var fileBytes = ms.ToArray();
                        var filename = $"{now.Year}/{now.Month}/{Guid.NewGuid().ToString()}/{CommonHelper.RemoveDiacritics(file.FileName)}";
                        var imageUrl = await _blobService.UploadAsync(fileBytes, BlobContainerName.PaymentRequest, filename, file.ContentType);
                        if (imageUrl == null)
                        {
                            res.ErrorMessage = "Tải hình lên lỗi";
                            return res;
                        }
                        var paymentRequestFile = new PaymentRequestFile()
                        {
                            PaymentRequestId = id,
                            Url = imageUrl.RelativeUrl,
                            CreateDate = DateTime.UtcNow.UTCToIct()
                        };
                        paymentRequestFiles.Add(paymentRequestFile);
                    }
                }
            }
            res.Status = true;
            res.Data = paymentRequestFiles;
            return res;
        }

        public async Task<PagingResponseModel<PaymentRequestPagingResponse>> GetUserPagingAsync(PaymentRequestPagingModel model, int userId)
        {
            var responseRaw = await _unitOfWork.PaymentRequestRepository.GetUserPagingAsync(model, userId);
            var response = responseRaw.Count > 0 ? responseRaw.GroupBy(y => new
            {
                y.Id,
                y.CreateDate,
                y.Name,
                y.FullName,
                y.ReviewStatus,
                y.ReviewUserName,
                y.Type,
                y.TotalRecord
            }).Select(t =>
            {
                var paymentStatus = GetPaymentStatus(t.Select(y => y.PaymentStatus.ToString()).ToList());
                var item = new PaymentRequestPagingResponse()
                {
                    Id = t.Key.Id,
                    CreateDate = t.Key.CreateDate,
                    Name = t.Key.Name,
                    FullName = t.Key.FullName,
                    ReviewUserName = t.Key.ReviewUserName,
                    ReviewStatus = t.Key.ReviewStatus,
                    ReviewStatusName = CommonHelper.GetDescription((EPaymentRequestStatus)t.Key.ReviewStatus),
                    Type = t.Key.Type,
                    Status = paymentStatus,
                    PaymentStatusName = CommonHelper.GetDescription(paymentStatus),
                    TotalRecord = t.Key.TotalRecord
                };
                return item;
            }).ToList() : new List<PaymentRequestPagingResponse>();
            var totalRecord = response.Count > 0 ? response.FirstOrDefault().TotalRecord : 0;
            var res = new PagingResponseModel<PaymentRequestPagingResponse>()
            {
                Items = response,
                TotalRecord = totalRecord
            };
            return res;
        }

        public async Task<PagingResponseModel<PaymentRequestPagingResponse>> GetManagerPagingAsync(PaymentRequestPagingModel model, int userId)
        {
            var responseRaw = await _unitOfWork.PaymentRequestRepository.GetManagerPagingAsync(model, userId);
            var response = responseRaw.Count > 0 ? responseRaw.GroupBy(y => new
            {
                y.Id,
                y.CreateDate,
                y.Name,
                y.FullName,
                y.ReviewStatus,
                y.ReviewUserName,
                y.Type,
                y.TotalRecord
            }).Select(t =>
            {
                var item = new PaymentRequestPagingResponse()
                {
                    Id = t.Key.Id,
                    CreateDate = t.Key.CreateDate,
                    Name = t.Key.Name,
                    FullName = t.Key.FullName,
                    ReviewUserName = t.Key.ReviewUserName,
                    ReviewStatus = t.Key.ReviewStatus,
                    ReviewStatusName = CommonHelper.GetDescription((EPaymentRequestStatus)t.Key.ReviewStatus),
                    Type = t.Key.Type,
                    TotalRecord = t.Key.TotalRecord,
                    PaymentRequestPlanDetails = t.Select(x => new PaymentRequestPlanDetail()
                    {
                        PaymentAmount = x.PaymentAmount,
                        ProposePaymentDate = x.ProposePaymentDate,
                        Note = x.Note
                    }).ToList()
                };
                return item;
            }).ToList() : new List<PaymentRequestPagingResponse>();
            var totalRecord = response.Count > 0 ? response.FirstOrDefault().TotalRecord : 0;
            var res = new PagingResponseModel<PaymentRequestPagingResponse>()
            {
                Items = response,
                TotalRecord = totalRecord
            };
            return res;
        }

        public async Task<CombineResponseModel<PaymentRequest>> ManagerReviewAsync(int id, int userId, PaymentRequestReviewModel model)
        {
            var res = new CombineResponseModel<PaymentRequest>();
            var paymentRequest = await _unitOfWork.PaymentRequestRepository.GetByIdAsync(id);
            if (paymentRequest == null)
            {
                res.ErrorMessage = "Không tồn tại đơn yêu cầu";
                return res;
            }
            if (paymentRequest.ReviewStatus != (int)EPaymentRequestStatus.Pending)
            {
                res.ErrorMessage = "Trạng thái đơn không cho phép duyệt";
                return res;
            }
            if (paymentRequest.ReviewUserId != userId)
            {
                res.ErrorMessage = "Bạn không phải manager của yêu cầu này";
                return res;
            }
            if (!model.IsAccept && string.IsNullOrEmpty(model.Reason))
            {
                res.ErrorMessage = "Lý do từ chối không được trống";
                return res;
            }
            if (paymentRequest.PaymentRequestDetails.Count == 0)
            {
                res.ErrorMessage = "Chi tiết yêu cầu trống";
                return res;
            }
            if (model.IsAccept)
            {
                var totalPriceRequest = paymentRequest.PaymentRequestDetails.Sum(t => Math.Round((decimal)((t.Quantity * t.Price) + (t.VatPrice == 0 ? t.Quantity * t.Price * t.Vat / 100 : t.VatPrice))));
                var totalPricePaymentPlan = paymentRequest.PaymentRequestPlans.Sum(t => Math.Round(t.PaymentAmount));
                if (totalPriceRequest != totalPricePaymentPlan)
                {
                    res.ErrorMessage = "Tổng tiển của yêu cầu không trùng khớp với tổng tiền các đợt thanh toán";
                    return res;
                }
            }
            paymentRequest.ReviewStatus = model.IsAccept ? (int)EPaymentRequestStatus.ManagerApproved : (int)EPaymentRequestStatus.ManagerRejected;
            paymentRequest.RejectReason = !model.IsAccept ? model.Reason : string.Empty;
            paymentRequest.UpdateDate = DateTime.UtcNow.UTCToIct();
            res.Status = true;
            res.Data = paymentRequest;
            //gửi mail cho người tạo đơn
            if (paymentRequest.CreateUserId != paymentRequest.ReviewUserId)//nếu người gửi không phải là manager 
            {
                var paymentRequestReviewedSendMail = new PaymentRequestReviewSendMail()
                {
                    CreateUser = paymentRequest.CreateUser.FullName,
                    Name = paymentRequest.Name,
                    CreateDate = DateTime.ParseExact(paymentRequest.CreateDate.ToString("dd/MM/yyyy HH:mm:ss.fff"), "dd/MM/yyyy HH:mm:ss.fff", CultureInfo.InvariantCulture),
                    ReviewResult = CommonHelper.GetDescription((EPaymentRequestStatus)paymentRequest.ReviewStatus),
                    Reviewer = paymentRequest.ReviewUser.FullName,
                    ReasonReject = paymentRequest.RejectReason ?? string.Empty
                };

                ObjSendMail objSendMail = new()
                {
                    FileName = "PaymentRequestReviewedTemplate.html",
                    Mail_To = new List<string>() { paymentRequest.CreateUser.Email },
                    Title = "[Thanh toán] Kết quả duyệt cho đơn thanh toán: " + paymentRequest.Name,
                    Mail_cc = [],
                    JsonObject = JsonConvert.SerializeObject(paymentRequestReviewedSendMail)
                };
                await _sendMailDynamicTemplateService.SendMailAsync(objSendMail);
            }
            //nếu đồng ý thì gửi mail cho accountant
            if (model.IsAccept)
            {
                var accountEmail = await _unitOfWork.GlobalConfigurationRepository.GetByNameAsync(Constant.SecondHrEmail);
                if (accountEmail == null || string.IsNullOrEmpty(accountEmail.Value))
                {
                    res.ErrorMessage = "Người dùng chưa được config";
                    return res;
                }
                var accountEmails = accountEmail.Value.Split(',').ToList();
                var paymentRequestReviewSendmail = new PaymentRequestReviewSendMail()
                {
                    Name = paymentRequest.Name,
                    Reviewer = paymentRequest.ReviewUser.FullName,
                    CreateDate = DateTime.ParseExact(paymentRequest.CreateDate.ToString("dd/MM/yyyy HH:mm:ss.fff"), "dd/MM/yyyy HH:mm:ss.fff", CultureInfo.InvariantCulture),
                    CreateUser = paymentRequest.CreateUser.FullName,
                    ReviewResult = CommonHelper.GetDescription((EPaymentRequestStatus)paymentRequest.ReviewStatus)
                };
                ObjSendMail objAccountantSendMail = new()
                {
                    FileName = "PaymentRequestReviewTemplate.html",
                    Mail_To = accountEmails,
                    Title = "[Thanh toán] Yêu cầu duyệt cho đơn thanh toán: " + paymentRequest.Name,
                    Mail_cc = [],
                    JsonObject = JsonConvert.SerializeObject(paymentRequestReviewSendmail)
                };
                await _sendMailDynamicTemplateService.SendMailAsync(objAccountantSendMail);
            }
            return res;
        }

        public async Task<CombineResponseModel<PaymentRequest>> AccountantReviewAsync(int id, PaymentRequestReviewModel model, string fullName, string email)
        {
            var res = new CombineResponseModel<PaymentRequest>();
            var accountantEmails = await _unitOfWork.GlobalConfigurationRepository.GetByNameAsync(Constant.PaymentRequestAccountEmails);
            if (accountantEmails == null || string.IsNullOrEmpty(accountantEmails.Value))
            {
                res.ErrorMessage = "Người dùng chưa được config";
                return res;
            }
            var accountantEmail = accountantEmails.Value.Split(",").Select(t => t.Trim()).FirstOrDefault(y => y.Equals(email, StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrEmpty(accountantEmail))
            {
                res.ErrorMessage = "Người dùng chưa được config";
                return res;
            }
            else
            {
                if (!accountantEmail.Equals(email, StringComparison.OrdinalIgnoreCase))
                {
                    res.ErrorMessage = "Bạn không có quyền duyệt";
                    return res;
                }
            }
            var paymentRequest = await _unitOfWork.PaymentRequestRepository.GetByIdAsync(id);
            if (paymentRequest == null)
            {
                res.ErrorMessage = "Không tồn tại đơn yêu cầu";
                return res;
            }
            if (paymentRequest.ReviewStatus != (int)EPaymentRequestStatus.ManagerApproved)
            {
                res.ErrorMessage = "Trạng thái đơn không cho phép duyệt";
                return res;
            }
            if (!model.IsAccept && string.IsNullOrEmpty(model.Reason))
            {
                res.ErrorMessage = "Lý do từ chối không được trống";
                return res;
            }
            if (paymentRequest.PaymentRequestDetails.Count == 0)
            {
                res.ErrorMessage = "Chi tiết yêu cầu trống";
                return res;
            }
            if (model.IsAccept)
            {
                var totalPriceRequest = paymentRequest.PaymentRequestDetails.Sum(t => Math.Round((decimal)((t.Quantity * t.Price) + (t.VatPrice == 0 ? t.Quantity * t.Price * t.Vat / 100 : t.VatPrice))));
                var totalPricePaymentPlan = paymentRequest.PaymentRequestPlans.Sum(t => Math.Round(t.PaymentAmount));
                if (totalPriceRequest != totalPricePaymentPlan)
                {
                    res.ErrorMessage = "Tổng tiển của yêu cầu không trùng khớp với tổng tiền các đợt thanh toán";
                    return res;
                }
            }
            paymentRequest.ReviewStatus = model.IsAccept ? (int)EPaymentRequestStatus.AccountantApproved : (int)EPaymentRequestStatus.AccountantRejected;
            paymentRequest.RejectReason = !model.IsAccept ? model.Reason : string.Empty;
            paymentRequest.UpdateDate = DateTime.UtcNow.UTCToIct();
            //gửi mail cho người tạo đơn
            var paymentRequestReviewedSendMail = new PaymentRequestReviewSendMail()
            {
                CreateUser = paymentRequest.CreateUser.FullName,
                Name = paymentRequest.Name,
                CreateDate = DateTime.ParseExact(paymentRequest.CreateDate.ToString("dd/MM/yyyy HH:mm:ss.fff"), "dd/MM/yyyy HH:mm:ss.fff", CultureInfo.InvariantCulture),
                ReviewResult = CommonHelper.GetDescription((EPaymentRequestStatus)paymentRequest.ReviewStatus),
                Reviewer = fullName,
                ReasonReject = paymentRequest.RejectReason ?? string.Empty
            };

            ObjSendMail objSendMail = new()
            {
                FileName = "PaymentRequestReviewedTemplate.html",
                Mail_To = new List<string>() { paymentRequest.CreateUser.Email },
                Title = "[Thanh toán] Kết quả duyệt cho đơn thanh toán: " + paymentRequest.Name,
                Mail_cc = [],
                JsonObject = JsonConvert.SerializeObject(paymentRequestReviewedSendMail)
            };
            await _sendMailDynamicTemplateService.SendMailAsync(objSendMail);
            //nếu đồng ý thì gửi mail cho giám đốc
            if (model.IsAccept)
            {
                var directorEmail = await _unitOfWork.GlobalConfigurationRepository.GetByNameAsync(Constant.PaymentRequestDirectorEmails);
                if (directorEmail == null || string.IsNullOrEmpty(directorEmail.Value))
                {
                    res.ErrorMessage = "Người dùng chưa được config";
                    return res;
                }
                var directorEmails = directorEmail.Value.Split(',').ToList();
                var paymentRequestReviewSendmail = new PaymentRequestReviewSendMail()
                {
                    Name = paymentRequest.Name,
                    Reviewer = fullName,
                    CreateDate = DateTime.ParseExact(paymentRequest.CreateDate.ToString("dd/MM/yyyy HH:mm:ss.fff"), "dd/MM/yyyy HH:mm:ss.fff", CultureInfo.InvariantCulture),
                    CreateUser = paymentRequest.CreateUser.FullName,
                    ReviewResult = CommonHelper.GetDescription((EPaymentRequestStatus)paymentRequest.ReviewStatus)
                };
                ObjSendMail objAccountantSendMail = new()
                {
                    FileName = "PaymentRequestReviewTemplate.html",
                    Mail_To = directorEmails,
                    Title = "[Thanh toán] Yêu cầu duyệt cho đơn thanh toán: " + paymentRequest.Name,
                    Mail_cc = [],
                    JsonObject = JsonConvert.SerializeObject(paymentRequestReviewSendmail)
                };
                await _sendMailDynamicTemplateService.SendMailAsync(objAccountantSendMail);
            }
            res.Status = true;
            res.Data = paymentRequest;
            return res;
        }

        public async Task<CombineResponseModel<PaymentRequest>> DirectorReviewAsync(int id, PaymentRequestReviewModel model, string fullName, string email)
        {
            var res = new CombineResponseModel<PaymentRequest>();
            var directorEmail = await _unitOfWork.GlobalConfigurationRepository.GetByNameAsync(Constant.PaymentRequestDirectorEmails);
            if (directorEmail == null || string.IsNullOrEmpty(directorEmail.Value))
            {
                res.ErrorMessage = "Người dùng chưa được config";
            }
            var directorEmails = directorEmail.Value.Split(",").Select(t => t.Trim()).FirstOrDefault(y => y.Equals(email, StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrEmpty(directorEmails))
            {
                res.ErrorMessage = "Người dùng chưa được config";
            }
            else
            {
                if (!directorEmails.Equals(email, StringComparison.OrdinalIgnoreCase))
                {
                    res.ErrorMessage = "Bạn không có quyền duyệt";
                }
            }
            var paymentRequest = await _unitOfWork.PaymentRequestRepository.GetByIdAsync(id);
            if (paymentRequest == null)
            {
                res.ErrorMessage = "Không tồn tại đơn yêu cầu";
                return res;
            }
            if (paymentRequest.ReviewStatus != (int)EPaymentRequestStatus.AccountantApproved)
            {
                res.ErrorMessage = "Trạng thái đơn không cho phép duyệt";
                return res;
            }
            if (!model.IsAccept && string.IsNullOrEmpty(model.Reason))
            {
                res.ErrorMessage = "Lý do từ chối không được trống";
                return res;
            }
            if (paymentRequest.PaymentRequestDetails.Count == 0)
            {
                res.ErrorMessage = "Chi tiết yêu cầu trống";
                return res;
            }
            if (model.IsAccept)
            {
                var totalPriceRequest = paymentRequest.PaymentRequestDetails.Sum(t => Math.Round((decimal)((t.Quantity * t.Price) + (t.VatPrice == 0 ? t.Quantity * t.Price * t.Vat / 100 : t.VatPrice))));
                var totalPricePaymentPlan = paymentRequest.PaymentRequestPlans.Sum(t => Math.Round(t.PaymentAmount));
                if (totalPriceRequest != totalPricePaymentPlan)
                {
                    res.ErrorMessage = "Tổng tiển của yêu cầu không trùng khớp với tổng tiền các đợt thanh toán";
                    return res;
                }
            }
            paymentRequest.ReviewStatus = model.IsAccept ? (int)EPaymentRequestStatus.DirectorApproved : (int)EPaymentRequestStatus.DirectorRejected;
            paymentRequest.RejectReason = !model.IsAccept ? model.Reason : string.Empty;
            paymentRequest.UpdateDate = DateTime.UtcNow.UTCToIct();
            if (model.IsAccept)
            {
                foreach (var item in paymentRequest.PaymentRequestPlans)
                {
                    var now = DateTime.UtcNow.UTCToIct();//lấy ngày duyệt đơn
                    var lastDay = DateTime.DaysInMonth(now.Year, now.Month);//lấy số ngày trong tháng hiện tại;
                    if (!item.ProposePaymentDate.HasValue)//nếu chưa đề nghị ngày thanh toán
                    {
                        if (now.Day < 15)//nếu duyệt trước ngày 15
                        {
                            var newDate = new DateTime(now.Year, now.Month, 15);
                            item.ProposePaymentDate = newDate;
                        }
                        else if (now.Day == lastDay) //nếu duyệt vào cuối tháng thì sẽ dời qua ngày 15 tháng sau 
                        {
                            var nextMonthDate = now.AddMonths(1);
                            item.ProposePaymentDate = new DateTime(nextMonthDate.Year, nextMonthDate.Month, 15);
                        }
                        else //duyệt sau ngày 15 thì sẽ dời về cuối tháng
                        {
                            var lastMonthDate = new DateTime(now.Year, now.Month, lastDay);
                            item.ProposePaymentDate = lastMonthDate;
                        }
                    }
                }
                await _unitOfWork.PaymentRequestPlanRepository.UpdateRangeAsync(paymentRequest.PaymentRequestPlans.ToList());
            }
            //gửi mail cho người tạo đơn
            var paymentRequestReviewedSendMail = new PaymentRequestReviewSendMail()
            {
                CreateUser = paymentRequest.CreateUser.FullName,
                Name = paymentRequest.Name,
                CreateDate = DateTime.ParseExact(paymentRequest.CreateDate.ToString("dd/MM/yyyy HH:mm:ss.fff"), "dd/MM/yyyy HH:mm:ss.fff", CultureInfo.InvariantCulture),
                ReviewResult = CommonHelper.GetDescription((EPaymentRequestStatus)paymentRequest.ReviewStatus),
                Reviewer = fullName,
                ReasonReject = paymentRequest.RejectReason ?? string.Empty
            };

            ObjSendMail objSendMail = new()
            {
                FileName = "PaymentRequestReviewedTemplate.html",
                Mail_To = new List<string>() { paymentRequest.CreateUser.Email },
                Title = "[Thanh toán] Kết quả duyệt cho đơn thanh toán: " + paymentRequest.Name,
                Mail_cc = [],
                JsonObject = JsonConvert.SerializeObject(paymentRequestReviewedSendMail)
            };
            await _sendMailDynamicTemplateService.SendMailAsync(objSendMail);
            res.Status = true;
            res.Data = paymentRequest;
            return res;
        }
        public async Task<PagingResponseModel<PaymentRequestPagingResponse>> GetAccountantPagingAsync(PaymentRequestPagingModel model)
        {
            var responseRaw = await _unitOfWork.PaymentRequestRepository.GetAccountantPagingAsync(model);
            var response = responseRaw.Count > 0 ? responseRaw.GroupBy(y => new
            {
                y.Id,
                y.CreateDate,
                y.Name,
                y.FullName,
                y.ReviewStatus,
                y.ReviewUserName,
                y.Type,
                y.TotalRecord
            }).Select(t =>
            {
                var paymentStatus = GetPaymentStatus(t.Select(y => y.PaymentStatus.ToString()).ToList());
                var item = new PaymentRequestPagingResponse()
                {
                    Id = t.Key.Id,
                    CreateDate = t.Key.CreateDate,
                    Name = t.Key.Name,
                    FullName = t.Key.FullName,
                    ReviewUserName = t.Key.ReviewUserName,
                    ReviewStatus = t.Key.ReviewStatus,
                    ReviewStatusName = CommonHelper.GetDescription((EPaymentRequestStatus)t.Key.ReviewStatus),
                    Type = t.Key.Type,
                    TotalRecord = t.Key.TotalRecord,
                    Status = paymentStatus,
                    PaymentStatusName = CommonHelper.GetDescription(paymentStatus),
                    PaymentRequestPlanDetails = t.Select(x => new PaymentRequestPlanDetail()
                    {
                        PaymentAmount = x.PaymentAmount,
                        ProposePaymentDate = x.ProposePaymentDate,
                        Note = x.Note
                    }).ToList()
                };
                return item;
            }).ToList() : new List<PaymentRequestPagingResponse>();
            var totalRecord = response.Count > 0 ? response.FirstOrDefault().TotalRecord : 0;
            var res = new PagingResponseModel<PaymentRequestPagingResponse>()
            {
                Items = response,
                TotalRecord = totalRecord
            };
            return res;
        }
        public async Task<PagingResponseModel<PaymentRequestPagingResponse>> GetDirectorPagingAsync(PaymentRequestPagingModel model)
        {
            var responseRaw = await _unitOfWork.PaymentRequestRepository.GetDirectorPagingAsync(model);
            var response = responseRaw.Count > 0 ? responseRaw.GroupBy(y => new
            {
                y.Id,
                y.CreateDate,
                y.Name,
                y.FullName,
                y.ReviewStatus,
                y.ReviewUserName,
                y.Type,
                y.TotalRecord
            }).Select(t =>
            {
                var item = new PaymentRequestPagingResponse()
                {
                    Id = t.Key.Id,
                    CreateDate = t.Key.CreateDate,
                    Name = t.Key.Name,
                    FullName = t.Key.FullName,
                    ReviewUserName = t.Key.ReviewUserName,
                    ReviewStatus = t.Key.ReviewStatus,
                    ReviewStatusName = CommonHelper.GetDescription((EPaymentRequestStatus)t.Key.ReviewStatus),
                    Type = t.Key.Type,
                    TotalRecord = t.Key.TotalRecord,
                    PaymentRequestPlanDetails = t.Select(x => new PaymentRequestPlanDetail()
                    {
                        PaymentAmount = x.PaymentAmount,
                        ProposePaymentDate = x.ProposePaymentDate,
                        Note = x.Note
                    }).ToList()
                };
                return item;
            }).ToList() : new List<PaymentRequestPagingResponse>();
            var totalRecord = response.Count > 0 ? response.FirstOrDefault().TotalRecord : 0;
            var res = new PagingResponseModel<PaymentRequestPagingResponse>()
            {
                Items = response,
                TotalRecord = totalRecord
            };
            return res;
        }

        public async Task<CombineResponseModel<List<PaymentRequest>>> ManagerMultiReviewAsync(PaymentRequestMultiReviewModel model, int userId)
        {
            var res = new CombineResponseModel<List<PaymentRequest>>();
            if (string.IsNullOrEmpty(model.PaymentRequestIds))
            {
                res.ErrorMessage = "Chưa chọn đơn cần duyệt";
                return res;
            }
            var listIds = model.PaymentRequestIds.Split(",").Select(t => int.Parse(t)).ToList();
            var requests = await _unitOfWork.PaymentRequestRepository.GetMultiRequestAsync(listIds);
            if (requests == null || requests.Count == 0)
            {
                res.ErrorMessage = "Đơn đã chọn không tồn tại";
                return res;
            }
            var isHasReviewerDiff = requests.Any(t => t.ReviewUserId != userId);
            if (isHasReviewerDiff)
            {
                res.ErrorMessage = "Đơn đã chọn có đơn bạn người duyệt không phải là bạn";
                return res;
            }
            var isHasNotAllowStatus = requests.Any(t => t.ReviewStatus != (int)EPaymentRequestStatus.Pending);
            if (isHasNotAllowStatus)
            {
                res.ErrorMessage = "Có đơn có trạng thái không cho phép duyệt";
                return res;
            }
            var isHasPriceRequestNotSamePlan = requests.Any(t => t.PaymentRequestPlans.Sum(x => Math.Round(x.PaymentAmount)) !=
            t.PaymentRequestDetails.Sum(y => Math.Round((decimal)((y.Quantity * y.Price) + (y.VatPrice == 0 ? (y.Quantity * y.Price) * y.Vat / 100 : y.VatPrice)))));
            if (isHasPriceRequestNotSamePlan)
            {
                res.ErrorMessage = "Có đơn tổng tiền chi tiết yêu cầu không khớp với tổng tiển đợt thanh toán";
                return res;
            }
            requests.ForEach(t =>
            {
                t.ReviewStatus = (int)EPaymentRequestStatus.ManagerApproved;
                t.UpdateDate = DateTime.UtcNow.UTCToIct();
            });
            res.Status = true;
            res.Data = requests;
            return res;
        }
        public async Task<CombineResponseModel<List<PaymentRequest>>> AccountantMultiReviewAsync(PaymentRequestMultiReviewModel model, string email)
        {
            var res = new CombineResponseModel<List<PaymentRequest>>();
            if (string.IsNullOrEmpty(model.PaymentRequestIds))
            {
                res.ErrorMessage = "Chưa chọn đơn cần duyệt";
                return res;
            }
            var accountantEmails = await _unitOfWork.GlobalConfigurationRepository.GetByNameAsync(Constant.PaymentRequestAccountEmails);
            if (accountantEmails == null || string.IsNullOrEmpty(accountantEmails.Value))
            {
                res.ErrorMessage = "Người dùng chưa được config";
                return res;
            }
            var accountantEmail = accountantEmails.Value.Split(",").Select(t => t.Trim()).FirstOrDefault(y => y.Equals(email, StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrEmpty(accountantEmail))
            {
                res.ErrorMessage = "Người dùng chưa được config";
                return res;
            }
            else
            {
                if (!accountantEmail.Equals(email, StringComparison.OrdinalIgnoreCase))
                {
                    res.ErrorMessage = "Bạn không có quyền duyệt";
                    return res;
                }
            }
            var listIds = model.PaymentRequestIds.Split(",").Select(t => int.Parse(t)).ToList();
            var requests = await _unitOfWork.PaymentRequestRepository.GetMultiRequestAsync(listIds);
            if (requests == null || requests.Count == 0)
            {
                res.ErrorMessage = "Đơn đã chọn không tồn tại";
                return res;
            }
            var isHasNotAllowStatus = requests.Any(t => t.ReviewStatus != (int)EPaymentRequestStatus.ManagerApproved);
            if (isHasNotAllowStatus)
            {
                res.ErrorMessage = "Có đơn có trạng thái không cho phép duyệt";
                return res;
            }
            var isHasPriceRequestNotSamePlan = requests.Any(t => t.PaymentRequestPlans.Sum(x => Math.Round(x.PaymentAmount)) !=
            t.PaymentRequestDetails.Sum(y => Math.Round((decimal)((y.Quantity * y.Price) + (y.VatPrice == 0 ? (y.Quantity * y.Price) * y.Vat / 100 : y.VatPrice)))));
            if (isHasPriceRequestNotSamePlan)
            {
                res.ErrorMessage = "Có đơn tổng tiền chi tiết yêu cầu không khớp với tổng tiển đợt thanh toán";
                return res;
            }
            requests.ForEach(t =>
            {
                t.ReviewStatus = (int)EPaymentRequestStatus.AccountantApproved;
                t.UpdateDate = DateTime.UtcNow.UTCToIct();
            });
            res.Status = true;
            res.Data = requests;
            return res;
        }
        public async Task<CombineResponseModel<List<PaymentRequest>>> DirectorMultiReviewAsync(PaymentRequestMultiReviewModel model, string email)
        {
            var res = new CombineResponseModel<List<PaymentRequest>>();
            if (string.IsNullOrEmpty(model.PaymentRequestIds))
            {
                res.ErrorMessage = "Chưa chọn đơn cần duyệt";
                return res;
            }
            var directorEmail = await _unitOfWork.GlobalConfigurationRepository.GetByNameAsync(Constant.PaymentRequestDirectorEmails);
            if (directorEmail == null || string.IsNullOrEmpty(directorEmail.Value))
            {
                res.ErrorMessage = "Người dùng chưa được config";
            }
            var directorEmails = directorEmail.Value.Split(",").Select(t => t.Trim()).FirstOrDefault(y => y.Equals(email, StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrEmpty(directorEmails))
            {
                res.ErrorMessage = "Người dùng chưa được config";
            }
            else
            {
                if (!directorEmails.Equals(email, StringComparison.OrdinalIgnoreCase))
                {
                    res.ErrorMessage = "Bạn không có quyền duyệt";
                }
            }
            var listIds = model.PaymentRequestIds.Split(",").Select(t => int.Parse(t)).ToList();
            var requests = await _unitOfWork.PaymentRequestRepository.GetMultiRequestAsync(listIds);
            if (requests == null || requests.Count == 0)
            {
                res.ErrorMessage = "Đơn đã chọn không tồn tại";
                return res;
            }
            var isHasNotAllowStatus = requests.Any(t => t.ReviewStatus != (int)EPaymentRequestStatus.AccountantApproved);
            if (isHasNotAllowStatus)
            {
                res.ErrorMessage = "Có đơn có trạng thái không cho phép duyệt";
                return res;
            }
            var isHasPriceRequestNotSamePlan = requests.Any(t => t.PaymentRequestPlans.Sum(x => Math.Round(x.PaymentAmount)) !=
            t.PaymentRequestDetails.Sum(y => Math.Round((decimal)((y.Quantity * y.Price) + (y.VatPrice == 0 ? (y.Quantity * y.Price) * y.Vat / 100 : y.VatPrice)))));
            if (isHasPriceRequestNotSamePlan)
            {
                res.ErrorMessage = "Có đơn tổng tiền chi tiết yêu cầu không khớp với tổng tiển đợt thanh toán";
                return res;
            }
            requests.ForEach(async t =>
            {
                t.ReviewStatus = (int)EPaymentRequestStatus.DirectorApproved;
                t.UpdateDate = DateTime.UtcNow.UTCToIct();
                foreach (var item in t.PaymentRequestPlans)
                {
                    var now = DateTime.UtcNow.UTCToIct();//lấy ngày duyệt đơn
                    var lastDay = DateTime.DaysInMonth(now.Year, now.Month);//lấy số ngày trong tháng hiện tại;
                    if (!(bool)item.IsUrgent)//nếu chưa cần gấp
                    {
                        if (now.Day < 15)//nếu duyệt trước ngày 15
                        {
                            var newDate = new DateTime(now.Year, now.Month, 15);
                            item.ProposePaymentDate = newDate;
                        }
                        else if (now.Day == lastDay) //nếu duyệt vào cuối tháng thì sẽ dời qua ngày 15 tháng sau 
                        {
                            var nextMonthDate = now.AddMonths(1);
                            item.ProposePaymentDate = new DateTime(nextMonthDate.Year, nextMonthDate.Month, 15);
                        }
                        else //duyệt sau ngày 15 thì sẽ dời về cuối tháng
                        {
                            var lastMonthDate = new DateTime(now.Year, now.Month, lastDay);
                            item.ProposePaymentDate = lastMonthDate;
                        }
                    }
                }
            });
            res.Status = true;
            res.Data = requests;
            return res;
        }

        public async Task SendMail(int id, string fullName)
        {
            var paymentRequest = await _unitOfWork.PaymentRequestRepository.GetByIdAsync(id);
            //gửi mail cho người tạo đơn
            if (paymentRequest.ReviewStatus == (int)EPaymentRequestStatus.AccountantUpdateRequest || paymentRequest.ReviewStatus == (int)EPaymentRequestStatus.DirectorUpdateRequest)
            {
                var paymentRequestUpdateSendMail = new PaymentRequestUpdateSendMail()
                {
                    CreateUser = paymentRequest.CreateUser.FullName,
                    Name = paymentRequest.Name,
                    CreateDate = DateTime.ParseExact(paymentRequest.CreateDate.ToString("dd/MM/yyyy HH:mm:ss.fff"), "dd/MM/yyyy HH:mm:ss.fff", CultureInfo.InvariantCulture),
                    ReviewResult = CommonHelper.GetDescription((EPaymentRequestStatus)paymentRequest.ReviewStatus),
                    Reviewer = fullName,
                    Comment = paymentRequest.ReviewStatus == (int)EPaymentRequestStatus.AccountantUpdateRequest ? paymentRequest.AccountantComment
                    : (paymentRequest.ReviewStatus == (int)EPaymentRequestStatus.DirectorUpdateRequest ? paymentRequest.DirectorComment : string.Empty)
                };
                ObjSendMail objUpdateSendMail = new()
                {
                    FileName = "PaymentRequestUpdateTemplate.html",
                    Mail_To = new List<string>() { paymentRequest.CreateUser.Email },
                    Title = "[Thanh toán] Yêu cầu cập nhật cho đơn thanh toán: " + paymentRequest.Name,
                    Mail_cc = [],
                    JsonObject = JsonConvert.SerializeObject(paymentRequestUpdateSendMail)
                };
                await _sendMailDynamicTemplateService.SendMailAsync(objUpdateSendMail);
            }
            else
            {
                var paymentRequestReviewedSendMail = new PaymentRequestReviewSendMail()
                {
                    CreateUser = paymentRequest.CreateUser.FullName,
                    Name = paymentRequest.Name,
                    CreateDate = DateTime.ParseExact(paymentRequest.CreateDate.ToString("dd/MM/yyyy HH:mm:ss.fff"), "dd/MM/yyyy HH:mm:ss.fff", CultureInfo.InvariantCulture),
                    ReviewResult = CommonHelper.GetDescription((EPaymentRequestStatus)paymentRequest.ReviewStatus),
                    Reviewer = paymentRequest.ReviewUser.FullName,
                    ReasonReject = paymentRequest.RejectReason ?? string.Empty
                };

                ObjSendMail objSendMail = new()
                {
                    FileName = "PaymentRequestReviewedTemplate.html",
                    Mail_To = new List<string>() { paymentRequest.CreateUser.Email },
                    Title = "[Thanh toán] Kết quả duyệt cho đơn thanh toán: " + paymentRequest.Name,
                    Mail_cc = [],
                    JsonObject = JsonConvert.SerializeObject(paymentRequestReviewedSendMail)
                };
                await _sendMailDynamicTemplateService.SendMailAsync(objSendMail);
            }

            //nếu manager duyệt thì gửi mail cho accountant
            if (paymentRequest.ReviewStatus == (int)EPaymentRequestStatus.ManagerApproved)
            {
                var accountEmail = await _unitOfWork.GlobalConfigurationRepository.GetByNameAsync(Constant.PaymentRequestAccountEmails);
                var accountEmails = !string.IsNullOrEmpty(accountEmail.Value) ? accountEmail.Value.Split(',').ToList() : new List<string>();
                var paymentRequestReviewSendmail = new PaymentRequestReviewSendMail()
                {
                    Name = paymentRequest.Name,
                    Reviewer = paymentRequest.ReviewUser.FullName,
                    CreateDate = DateTime.ParseExact(paymentRequest.CreateDate.ToString("dd/MM/yyyy HH:mm:ss.fff"), "dd/MM/yyyy HH:mm:ss.fff", CultureInfo.InvariantCulture),
                    CreateUser = paymentRequest.CreateUser.FullName,
                    ReviewResult = CommonHelper.GetDescription((EPaymentRequestStatus)paymentRequest.ReviewStatus)
                };
                ObjSendMail objAccountantSendMail = new()
                {
                    FileName = "PaymentRequestReviewTemplate.html",
                    Mail_To = accountEmails,
                    Title = "[Thanh toán] Yêu cầu duyệt cho đơn thanh toán: " + paymentRequest.Name,
                    Mail_cc = [],
                    JsonObject = JsonConvert.SerializeObject(paymentRequestReviewSendmail)
                };
                await _sendMailDynamicTemplateService.SendMailAsync(objAccountantSendMail);
            }
            // nếu accountant duyệt thì gửi mail cho giám đốc
            if (paymentRequest.ReviewStatus == (int)EPaymentRequestStatus.AccountantApproved)
            {
                var directorEmail = await _unitOfWork.GlobalConfigurationRepository.GetByNameAsync(Constant.PaymentRequestDirectorEmails);
                var directorEmails = !string.IsNullOrEmpty(directorEmail.Value) ? directorEmail.Value.Split(',').ToList() : new List<string>();
                var paymentRequestReviewSendmail = new PaymentRequestReviewSendMail()
                {
                    Name = paymentRequest.Name,
                    Reviewer = fullName,
                    CreateDate = DateTime.ParseExact(paymentRequest.CreateDate.ToString("dd/MM/yyyy HH:mm:ss.fff"), "dd/MM/yyyy HH:mm:ss.fff", CultureInfo.InvariantCulture),
                    CreateUser = paymentRequest.CreateUser.FullName,
                    ReviewResult = CommonHelper.GetDescription((EPaymentRequestStatus)paymentRequest.ReviewStatus)
                };
                ObjSendMail objAccountantSendMail = new()
                {
                    FileName = "PaymentRequestReviewTemplate.html",
                    Mail_To = directorEmails,
                    Title = "[Thanh toán] Yêu cầu duyệt cho đơn thanh toán: " + paymentRequest.Name,
                    Mail_cc = [],
                    JsonObject = JsonConvert.SerializeObject(paymentRequestReviewSendmail)
                };
                await _sendMailDynamicTemplateService.SendMailAsync(objAccountantSendMail);
            }
        }

        public async Task<CombineResponseModel<PaymentRequest>> AccountantUpdateRequestAsync(int id, string email, UpdateRequestModel model)
        {
            var res = new CombineResponseModel<PaymentRequest>();
            var paymentRequest = await _unitOfWork.PaymentRequestRepository.GetByIdAsync(id);
            if (paymentRequest == null)
            {
                res.ErrorMessage = "Không tồn tại đơn yêu cầu";
                return res;
            }
            var accountantEmails = await _unitOfWork.GlobalConfigurationRepository.GetByNameAsync(Constant.PaymentRequestAccountEmails);
            if (accountantEmails == null || string.IsNullOrEmpty(accountantEmails.Value))
            {
                res.ErrorMessage = "Người dùng chưa được config";
                return res;
            }
            var accountantEmail = accountantEmails.Value.Split(",").Select(t => t.Trim()).FirstOrDefault(y => y.Equals(email, StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrEmpty(accountantEmail))
            {
                res.ErrorMessage = "Người dùng chưa được config";
                return res;
            }
            else
            {
                if (!accountantEmail.Equals(email, StringComparison.OrdinalIgnoreCase))
                {
                    res.ErrorMessage = "Bạn không có quyền thực hiện yêu cầu này";
                    return res;
                }
            }
            if (paymentRequest.ReviewStatus != (int)EPaymentRequestStatus.ManagerApproved)
            {
                res.ErrorMessage = "Trạng thái đơn không cho phép duyệt";
                return res;
            }
            paymentRequest.AccountantComment = model.Comment;
            paymentRequest.ReviewStatus = (int)EPaymentRequestStatus.AccountantUpdateRequest;
            paymentRequest.UpdateDate = DateTime.UtcNow.UTCToIct();
            res.Status = true;
            res.Data = paymentRequest;
            return res;
        }

        public async Task<CombineResponseModel<PaymentRequest>> DirectorUpdateRequestAsync(int id, string email, UpdateRequestModel model)
        {
            var res = new CombineResponseModel<PaymentRequest>();
            var paymentRequest = await _unitOfWork.PaymentRequestRepository.GetByIdAsync(id);
            if (paymentRequest == null)
            {
                res.ErrorMessage = "Không tồn tại đơn yêu cầu";
                return res;
            }
            var accountantEmails = await _unitOfWork.GlobalConfigurationRepository.GetByNameAsync(Constant.PaymentRequestDirectorEmails);
            if (accountantEmails == null || string.IsNullOrEmpty(accountantEmails.Value))
            {
                res.ErrorMessage = "Người dùng chưa được config";
                return res;
            }
            var accountantEmail = accountantEmails.Value.Split(",").Select(t => t.Trim()).FirstOrDefault(y => y.Equals(email, StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrEmpty(accountantEmail))
            {
                res.ErrorMessage = "Người dùng chưa được config";
                return res;
            }
            else
            {
                if (!accountantEmail.Equals(email, StringComparison.OrdinalIgnoreCase))
                {
                    res.ErrorMessage = "Bạn không có quyền thực hiện yêu cầu này";
                    return res;
                }
            }
            if (paymentRequest.ReviewStatus != (int)EPaymentRequestStatus.AccountantApproved)
            {
                res.ErrorMessage = "Trạng thái đơn không cho phép duyệt";
                return res;
            }
            paymentRequest.DirectorComment = model.Comment;
            paymentRequest.ReviewStatus = (int)EPaymentRequestStatus.DirectorUpdateRequest;
            paymentRequest.UpdateDate = DateTime.UtcNow.UTCToIct();
            res.Status = true;
            res.Data = paymentRequest;
            return res;
        }

        public async Task<CombineResponseModel<PaymentRequest>> AccountantResetAsync(int id, string email)
        {
            var res = new CombineResponseModel<PaymentRequest>();
            var paymentRequest = await _unitOfWork.PaymentRequestRepository.GetByIdAsync(id);
            if (paymentRequest == null)
            {
                res.ErrorMessage = "Không tồn tại đơn yêu cầu";
                return res;
            }
            if (paymentRequest.ReviewStatus != (int)EPaymentRequestStatus.DirectorApproved)
            {
                res.ErrorMessage = "Trạng thái đơn không cho phép chuyển đổi";
                return res;
            }
            var isClose = paymentRequest.Isclose ?? false;
            if (isClose)
            {
                res.ErrorMessage = "Đơn đã đóng";
                return res;
            }
            var accountantEmails = await _unitOfWork.GlobalConfigurationRepository.GetByNameAsync(Constant.PaymentRequestAccountEmails);
            if (accountantEmails == null || string.IsNullOrEmpty(accountantEmails.Value))
            {
                res.ErrorMessage = "Người dùng chưa được config";
                return res;
            }
            var accountantEmail = accountantEmails.Value.Split(",").Select(t => t.Trim()).FirstOrDefault(y => y.Equals(email, StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrEmpty(accountantEmail))
            {
                res.ErrorMessage = "Người dùng chưa được config";
                return res;
            }
            else
            {
                if (!accountantEmail.Equals(email, StringComparison.OrdinalIgnoreCase))
                {
                    res.ErrorMessage = "Bạn không có quyền thực hiện yêu cầu này";
                    return res;
                }
            }
            paymentRequest.ReviewStatus = (int)EPaymentRequestStatus.ManagerApproved;
            paymentRequest.UpdateDate = DateTime.UtcNow.UTCToIct();
            res.Status = true;
            res.Data = paymentRequest;
            return res;
        }
        public async Task<CombineResponseModel<PaymentRequest>> AccountantCancelAsync(int id, string email)
        {
            var res = new CombineResponseModel<PaymentRequest>();
            var paymentRequest = await _unitOfWork.PaymentRequestRepository.GetByIdAsync(id);
            if (paymentRequest == null)
            {
                res.ErrorMessage = "Không tồn tại đơn yêu cầu";
                return res;
            }
            if (paymentRequest.ReviewStatus == (int)EPaymentRequestStatus.Cancel)
            {
                res.ErrorMessage = "Trạng thái đơn không cho phép hủy";
                return res;
            }
            var isClose = paymentRequest.Isclose ?? false;
            if (isClose)
            {
                res.ErrorMessage = "Đơn đã đóng";
                return res;
            }
            var accountantEmails = await _unitOfWork.GlobalConfigurationRepository.GetByNameAsync(Constant.PaymentRequestAccountEmails);
            if (accountantEmails == null || string.IsNullOrEmpty(accountantEmails.Value))
            {
                res.ErrorMessage = "Người dùng chưa được config";
                return res;
            }
            var accountantEmail = accountantEmails.Value.Split(",").Select(t => t.Trim()).FirstOrDefault(y => y.Equals(email, StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrEmpty(accountantEmail))
            {
                res.ErrorMessage = "Người dùng chưa được config";
                return res;
            }
            else
            {
                if (!accountantEmail.Equals(email, StringComparison.OrdinalIgnoreCase))
                {
                    res.ErrorMessage = "Bạn không có quyền thực hiện yêu cầu này";
                    return res;
                }
            }
            paymentRequest.ReviewStatus = (int)EPaymentRequestStatus.Cancel;
            paymentRequest.UpdateDate = DateTime.UtcNow.UTCToIct();
            res.Status = true;
            res.Data = paymentRequest;
            return res;
        }

        public async Task<CombineResponseModel<PaymentRequest>> AccountantUpdateAsync(int id, string email, PaymentRequestAccountantUpdateModel model)
        {
            var res = new CombineResponseModel<PaymentRequest>();
            var accountantEmails = await _unitOfWork.GlobalConfigurationRepository.GetByNameAsync(Constant.PaymentRequestAccountEmails);
            if (accountantEmails == null || string.IsNullOrEmpty(accountantEmails.Value))
            {
                res.ErrorMessage = "Người dùng chưa được config";
                return res;
            }
            var accountantEmail = accountantEmails.Value.Split(",").Select(t => t.Trim()).FirstOrDefault(y => y.Equals(email, StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrEmpty(accountantEmail))
            {
                res.ErrorMessage = "Người dùng chưa được config";
                return res;
            }
            else
            {
                if (!accountantEmail.Equals(email, StringComparison.OrdinalIgnoreCase))
                {
                    res.ErrorMessage = "Bạn không có quyền thực hiện yêu cầu này";
                    return res;
                }
            }
            var paymentRequest = await _unitOfWork.PaymentRequestRepository.GetByIdAsync(id);
            if (paymentRequest == null)
            {
                res.ErrorMessage = "Không tồn tại đơn yêu cầu";
                return res;
            }
            var isClose = paymentRequest.Isclose ?? false;
            if (isClose)
            {
                res.ErrorMessage = "Đơn đã đóng";
                return res;
            }
            if (model.VendorId.HasValue)
            {
                var isHasAllowVendor = paymentRequest.CreateUser.UserVendors.Any(t => t.VendorId == model.VendorId);
                if (!isHasAllowVendor)
                {
                    res.ErrorMessage = "Nhà cung cấp không hợp lệ";
                    return res;
                }
            }
            if (paymentRequest.ReviewStatus != (int)EPaymentRequestStatus.ManagerApproved)
            {
                res.ErrorMessage = "Trạng thái đơn không cho phép thực hiện yêu cầu này";
                return res;
            }
            var totalPriceRequest = paymentRequest.PaymentRequestDetails.Sum(t => Math.Round((decimal)((t.Quantity * t.Price) + (t.VatPrice == 0 ? t.Quantity * t.Price * t.Vat / 100 : t.VatPrice))));
            var totalPricePlan = paymentRequest.PaymentRequestPlans.Sum(t => Math.Round(t.PaymentAmount));
            if (totalPriceRequest != totalPricePlan)
            {
                res.ErrorMessage = "Tổng tiển của yêu cầu không khớp với tổng tiền các đợt thanh toán";
                return res;
            }
            paymentRequest.VendorId = model.VendorId;
            paymentRequest.ReviewStatus = (int)EPaymentRequestStatus.AccountantApproved;
            paymentRequest.UpdateDate = DateTime.UtcNow.UTCToIct();
            res.Status = true;
            res.Data = paymentRequest;
            return res;
        }

        public async Task<CombineResponseModel<PaymentRequest>> AccountantCloseAsync(int id, string email)
        {
            var res = new CombineResponseModel<PaymentRequest>();
            var accountantEmails = await _unitOfWork.GlobalConfigurationRepository.GetByNameAsync(Constant.PaymentRequestAccountEmails);
            if (accountantEmails == null || string.IsNullOrEmpty(accountantEmails.Value))
            {
                res.ErrorMessage = "Người dùng chưa được config";
                return res;
            }
            var accountantEmail = accountantEmails.Value.Split(",").Select(t => t.Trim()).FirstOrDefault(y => y.Equals(email, StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrEmpty(accountantEmail))
            {
                res.ErrorMessage = "Người dùng chưa được config";
                return res;
            }
            else
            {
                if (!accountantEmail.Equals(email, StringComparison.OrdinalIgnoreCase))
                {
                    res.ErrorMessage = "Bạn không có quyền thực hiện yêu cầu này";
                    return res;
                }
            }
            var paymentRequest = await _unitOfWork.PaymentRequestRepository.GetByIdAsync(id);
            if (paymentRequest == null)
            {
                res.ErrorMessage = "Không tồn tại đơn yêu cầu";
                return res;
            }
            var isClose = paymentRequest.Isclose ?? false;
            if (isClose)
            {
                res.ErrorMessage = "Đơn đã đóng trước đó";
                return res;
            }
            var isHasUnpaid = paymentRequest.PaymentRequestPlans.Any(t => (bool)!t.PaymentStatus);
            if (isHasUnpaid)
            {
                res.ErrorMessage = "Có đợt chưa thanh toán";
                return res;
            }
            paymentRequest.Isclose = true;
            paymentRequest.UpdateDate = DateTime.UtcNow.UTCToIct();
            res.Status = true;
            res.Data = paymentRequest;
            return res;
        }

        public async Task<CombineResponseModel<PaymentRequest>> CopyAsync(int id, int userId)
        {
            var res = new CombineResponseModel<PaymentRequest>();
            var request = await _unitOfWork.PaymentRequestRepository.GetByIdAsync(id);
            if (request == null)
            {
                res.ErrorMessage = "Không tồn tại đơn yêu cầu";
                return res;
            }
            if (request.CreateUserId != userId)
            {
                res.ErrorMessage = "Đơn này không phải của bạn";
                return res;
            }
            var paymentRequestDetails = await _unitOfWork.PaymentRequestDetailRepository.GetByPaymentRequestIdAsync(request.Id);
            var paymentRequestPlans = await _unitOfWork.PaymentRequestPlanRepository.GetByPaymentRequestIdAsync(request.Id);
            var paymentRequestFiles = await _unitOfWork.PaymentRequestFileRepository.GetByPaymentRequestIdAsync(request.Id);
            if (request.ReviewUserId == request.CreateUserId)//nếu người duyệt là bản thân
            {
                var totalPriceRequest = paymentRequestDetails.Sum(t => Math.Round((decimal)((t.Quantity * t.Price) + (t.VatPrice == 0 ? t.Quantity * t.Price * t.Vat / 100 : t.VatPrice))));
                var totalPricePlan = paymentRequestPlans.Sum(t => Math.Round(t.PaymentAmount));
                if (totalPriceRequest != totalPricePlan)
                {
                    res.ErrorMessage = "Tổng tiển chi tiết yêu cầu không khớp với tổng tiển các đợt thanh toán";
                    return res;
                }
            }
            var paymentRequest = new PaymentRequest();
            paymentRequest.Name = request.Name;
            paymentRequest.CreateUserId = request.CreateUserId;
            paymentRequest.ReviewUserId = request.ReviewUserId;
            paymentRequest.Type = request.Type;
            paymentRequest.Note = request.Note;
            paymentRequest.VendorId = request.VendorId;
            paymentRequest.PaymentMethod = request.PaymentMethod;
            paymentRequest.BankName = request.BankName;
            paymentRequest.BankNumber = request.BankNumber;
            paymentRequest.DepartmentId = request.DepartmentId;
            paymentRequest.CreateDate = DateTime.UtcNow.UTCToIct();
            paymentRequest.ReviewStatus = request.ReviewUserId == request.CreateUserId ? (int)EPaymentRequestStatus.ManagerApproved : (int)EPaymentRequestStatus.Pending;
            paymentRequest.RejectReason = string.Empty;
            paymentRequest.AccountantComment = string.Empty;
            paymentRequest.DirectorComment = string.Empty;
            paymentRequest.Isclose = false;
            paymentRequest.PaymentRequestDetails = paymentRequestDetails.Count > 0 ? paymentRequestDetails.Select(t =>
            {
                var detail = new PaymentRequestDetail();
                detail.Name = t.Name;
                detail.Quantity = t.Quantity;
                detail.Price = t.Price;
                detail.Vat = t.Vat;
                detail.CreateDate = DateTime.UtcNow.UTCToIct();
                detail.VatPrice = t.VatPrice;
                return detail;
            }).ToList() : new List<PaymentRequestDetail>();
            paymentRequest.PaymentRequestPlans = paymentRequestPlans.Count > 0 ? paymentRequestPlans.Select(t =>
            {
                var plan = new PaymentRequestPlan();
                plan.PaymentType = t.PaymentType;
                plan.PaymentStatus = t.PaymentStatus;
                plan.IsUrgent = false;
                plan.PaymentAmount = t.PaymentAmount;
                plan.CreateDate = DateTime.UtcNow.UTCToIct();
                plan.Note = t.Note;
                return plan;
            }).ToList() : new List<PaymentRequestPlan>();
            paymentRequest.PaymentRequestFiles = paymentRequestFiles.Count > 0 ? paymentRequestFiles.Select(t =>
            {
                var file = new PaymentRequestFile()
                {
                    Url = t.Url,
                    CreateDate = DateTime.UtcNow.UTCToIct()
                };
                return t;
            }).ToList() : new List<PaymentRequestFile>();

            res.Status = true;
            res.Data = paymentRequest;
            return res;
        }
    }
}
