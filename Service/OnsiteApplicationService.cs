using FirebaseAdmin.Messaging;
using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Enums;
using InternalPortal.ApplicationCore.Helpers;
using InternalPortal.ApplicationCore.Interfaces;
using InternalPortal.ApplicationCore.Interfaces.Business;
using InternalPortal.ApplicationCore.Interfaces.Email;
using InternalPortal.ApplicationCore.Interfaces.Utilities.AzureBlob;
using InternalPortal.ApplicationCore.Interfaces.Utilities.Firebase;
using InternalPortal.ApplicationCore.Models;
using InternalPortal.ApplicationCore.Models.CriteriaModel;
using InternalPortal.ApplicationCore.Models.Holiday;
using InternalPortal.ApplicationCore.Models.OnsiteApplicationModel;
using InternalPortal.ApplicationCore.Models.PagingModel;
using InternalPortal.ApplicationCore.Models.User;
using InternalPortal.ApplicationCore.Models.UserInternal;
using iTextSharp.text;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using static InternalPortal.ApplicationCore.ValueObjects.Global;

namespace InternalPortal.Infrastructure.Services.Business
{
    public class OnsiteApplicationService : IOnsiteApplicationService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ISendMailDynamicTemplateService _sendMailDynamicTemplateService;
        private readonly string _frontEndDomain;
        private readonly IFirebaseMessageCloudService _firebaseMessageCloudService;
        private readonly IBlobService _blobService;
        private readonly string _blobDomain;
        public OnsiteApplicationService(
            IUnitOfWork unitOfWork,
            ISendMailDynamicTemplateService sendMailDynamicTemplateService,
            IConfiguration configuration,
            IFirebaseMessageCloudService firebaseMessageCloudService,
            IBlobService blobService
            )
        {
            _unitOfWork = unitOfWork;
            _sendMailDynamicTemplateService = sendMailDynamicTemplateService;
            _frontEndDomain = configuration["FrontEndDomain"]!;
            _firebaseMessageCloudService = firebaseMessageCloudService;
            _blobService = blobService;
            _blobDomain = configuration["BlobDomainUrl"]!;
        }

        #region Public Method
        public async Task<PagingResponseModel<OnsiteApplicationPagingModel>> GetAllWithPagingAsync(OnsiteApplicationCriteriaModel searchModel, int userId)
        {
            var recordsRaw = await _unitOfWork.OnsiteApplicationRepository.GetAllWithPagingAsync(searchModel, userId);

            var records = recordsRaw.Select(x => new OnsiteApplicationPagingModel
            {
                Id = x.Id,
                RegisterDate = x.RegisterDate,
                FromDate = x.FromDate,
                ToDate = x.ToDate,
                ReviewNote = x.ReviewNote,
                Status = x.Status,
                UserId = x.UserId,
                ReviewUserId = x.ReviewUserId,
                ReviewUser = x.ReviewUser,
                UserName = x.UserName,
                Location = x.Location,
                OnsiteNote = x.OnsiteNote,
                ProjectName = x.ProjectName,
                ReviewDate = x.ReviewDate,
                PeriodType = x.PeriodType,
                NumberDayOnsite = x.NumberDayOnsite,
                JobTitle = x.JobTitle,
                TotalRecord = x.TotalRecord,
                IsCharge = x.IsCharge,
                FileUrls = !string.IsNullOrEmpty(x.FileUrls) ? x.FileUrls.Split(",").Select(o => $"{_blobDomain}/{o}").ToList() : new List<string>(),
                Avatar = x.Avatar ?? ""
            }).ToList();

            var totalRecords = records.FirstOrDefault();
            records.Remove(totalRecords!);

            var res = new PagingResponseModel<OnsiteApplicationPagingModel>
            {
                Items = records,
                TotalRecord = totalRecords!.TotalRecord ?? 0
            };

            return res;
        }

        public async Task<CombineResponseModel<OnsiteApplicationNotificationModel>> PrepareCreateAsync(OnsiteApplicationRequest applicationRequest, UserDtoModel user)
        {
            var response = new CombineResponseModel<OnsiteApplicationNotificationModel>();

            bool isValidToSubmit = CommonHelper.ValidDateToSubmitApplication(applicationRequest.FromDate, EApplicationMessageType.OnsiteApplication, out string error);
            if (!isValidToSubmit)
            {
                response.ErrorMessage = error;
                return response;
            }

            if (string.IsNullOrEmpty(applicationRequest.Location))
            {
                response.ErrorMessage = "Địa điểm công tác không được trống";
                return response;
            }

            if (applicationRequest.Files == null || !applicationRequest.Files.Any())
            {
                response.ErrorMessage = "Bản báo cáo công tác không được trống";
                return response;
            }

            if (string.IsNullOrEmpty(applicationRequest.OnsiteNote))
            {
                applicationRequest.OnsiteNote = "";
            }

            if (applicationRequest.FromDate > applicationRequest.ToDate)
            {
                response.ErrorMessage = "Từ ngày không được lớn hơn đến ngày";
                return response;
            }

            if (string.IsNullOrEmpty(applicationRequest.ProjectName))
            {
                response.ErrorMessage = "Tên dự án liên quan công tác không được trống";
                return response;
            }

            if (applicationRequest.PeriodType != EPeriodType.AllDay && applicationRequest.PeriodType != EPeriodType.FirstHalf && applicationRequest.PeriodType != EPeriodType.SecondHalf)
            {
                response.ErrorMessage = "Thời gian buổi công tác không hợp lệ";
                return response;
            }

            var dayOnsite = CalculateDayOnsite(applicationRequest.PeriodType, applicationRequest.FromDate, applicationRequest.ToDate);
            List<OnsiteApplication> onsiteApplicationByDate = await _unitOfWork.OnsiteApplicationRepository.GetByUserIdAndDateAsync(user.Id, applicationRequest.FromDate.Date, applicationRequest.ToDate.Date);
            if (onsiteApplicationByDate != null && onsiteApplicationByDate.Where(x => x.Status == (int)EReviewStatus.Pending && (x.PeriodType == (int)applicationRequest.PeriodType || x.PeriodType != (int)EPeriodType.AllDay && applicationRequest.PeriodType == (int)EPeriodType.AllDay)).Any())
            {
                response.ErrorMessage = "Trùng ngày công tác";
                return response;
            }

            var reviewer = await _unitOfWork.UserInternalRepository.GetByIdAsync(applicationRequest.ReviewUserId);
            if (reviewer == null)
            {
                response.ErrorMessage = "Người duyệt không tồn tại";
                return response;
            }

            var holidays = await _unitOfWork.HolidayRepository.GetAllAsync();
            var holidayHelpers = holidays.Select(h => new HolidayHelper
            {
                Date = h.HolidayDate,
                IsHolidayByYear = h.IsHolidayByYear,
            }).ToList();

            var errorMessage = DateTimeHelper.ValidateDateRange(applicationRequest.FromDate, applicationRequest.ToDate, holidayHelpers);
            if (!string.IsNullOrEmpty(errorMessage))
            {
                response.ErrorMessage = errorMessage;
                return response;
            }

            var fileUrls = new List<string>();
            
            if (applicationRequest.Files != null)
            {
                foreach (var file in applicationRequest.Files)
                {
                    using (var ms = new MemoryStream())
                    {
                        await file.CopyToAsync(ms);
                        var fileBytes = ms.ToArray();
                        var fileName = $"{DateTime.UtcNow.UTCToIct().Year}/{DateTime.UtcNow.UTCToIct().Month}/{Guid.NewGuid().ToString()}";
                        var url = await _blobService.UploadAsync(fileBytes, BlobContainerName.ScannedOnsite, fileName, file.ContentType);
                        if (url == null || string.IsNullOrEmpty(url.RelativeUrl))
                        {
                            response.ErrorMessage = "Tải hình lên lỗi";
                            return response;
                        }
                        fileUrls.Add(url.RelativeUrl);
                    }
                }
            }

            var application = new OnsiteApplication()
            {
                CreatedDate = DateTime.UtcNow.UTCToIct(),
                RegisterDate = DateTime.UtcNow.UTCToIct(),
                Location = applicationRequest.Location,
                FromDate = applicationRequest.FromDate,
                ToDate = applicationRequest.ToDate,
                OnsiteNote = applicationRequest.OnsiteNote,
                ProjectName = applicationRequest.ProjectName,
                ReviewUserId = applicationRequest.ReviewUserId,
                Status = (int)EReviewStatus.Pending,
                PeriodType = (int)applicationRequest.PeriodType,
                UserId = user.Id,
                NumberDayOnsite = dayOnsite,
                IsCharge = applicationRequest.IsCharge,
                OnsiteApplicationFiles = fileUrls.Select(o => new OnsiteApplicationFile
                {
                    FileUrl = o,
                    CreateDate = DateTime.UtcNow.UTCToIct()
                }).ToList()
            };

            await _unitOfWork.OnsiteApplicationRepository.CreateAsync(application);
            await _unitOfWork.SaveChangesAsync();

            var onsiteApplicationData = new OnsiteApplicationNotificationModel
            {
                Id = application.Id,
                FromDate = application.FromDate,
                ToDate = application.ToDate,
                ReviewNote = application.ReviewNote,
                Status = (EReviewStatus)application.Status,
                ProjectName = application.ProjectName,
                Location = application.Location,
                UserId = application.UserId,
                UserFullName = user.FullName,
                ReviewUserId = application.ReviewUserId,
                IsCharge = application.IsCharge
            };

            response.Status = true;
            response.Data = onsiteApplicationData;

            return response;
        }

        public async Task SendMailAsync(int onsiteApplicationId)
        {
            var onsiteApplication = await _unitOfWork.OnsiteApplicationRepository.GetByIdAsync(onsiteApplicationId);
            if (onsiteApplication == null)
            {
                return;
            }

            string dateString = onsiteApplication.FromDate.Date == onsiteApplication.ToDate.Date ?
                onsiteApplication.FromDate.Date.ToString("dd/MM/yyyy") :
                onsiteApplication.FromDate.Date.ToString("dd/MM/yyyy") + " - " + onsiteApplication.ToDate.Date.ToString("dd/MM/yyyy");

            // Chú thích thêm khi thời gian công tác nửa ngày
            if (onsiteApplication.FromDate.Date == onsiteApplication.ToDate.Date)
            {
                dateString += onsiteApplication.PeriodType != (int)EPeriodType.AllDay ? (onsiteApplication.PeriodType == (int)EPeriodType.FirstHalf ? " buổi sáng" : " buổi chiều") : "";
            }

            if (onsiteApplication.Status == (int)EReviewStatus.Pending)
            {
                var onsiteApplicationSendMail = new OnsiteApplicationSendMail()
                {
                    DateData = dateString,
                    Location = onsiteApplication.Location!,
                    Register = onsiteApplication.User.FullName!,
                    Reviewer = onsiteApplication.ReviewUser.FullName!,
                    ReviewLink = _frontEndDomain + Constant.ReviewOnsiteApplicationPath,
                    OnsiteNote = onsiteApplication.OnsiteNote,
                    ProjectName = onsiteApplication.ProjectName!,
                    IsCharge = onsiteApplication.IsCharge ? "Có" : "Không"
                };

                ObjSendMail objSendMail = new()
                {
                    FileName = "OnsiteTemplate.html",
                    Mail_To = [onsiteApplication.ReviewUser.Email!],
                    Title = "[Công Tác] Yêu cầu duyệt phiếu công tác của " + onsiteApplication.User.FullName + " ngày " + dateString,
                    Mail_cc = [],
                    JsonObject = JsonConvert.SerializeObject(onsiteApplicationSendMail)
                };
                await _sendMailDynamicTemplateService.SendMailAsync(objSendMail);
            }
            else
            {
                var onsiteApplicationReviewSendMail = new OnsiteApplicationReviewSendMail()
                {
                    DateData = dateString,
                    IsAccept = onsiteApplication.Status == (int)EReviewStatus.Reviewed,
                    Register = onsiteApplication.User.FullName!,
                    RegisterLink = _frontEndDomain + Constant.RegistOnsiteApplicationPath,
                    ReasonReject = onsiteApplication.ReviewNote,
                    Reviewer = onsiteApplication.ReviewUser.FullName!,
                    Location = onsiteApplication.Location!,
                    ProjectName = onsiteApplication.ProjectName!,
                    IsCharge = onsiteApplication.IsCharge ? "Có" : "Không"
                };

                ObjSendMail objSendMail = new()
                {
                    FileName = "OnsiteReviewTemplate.html",
                    Mail_To = [onsiteApplication.User.Email!],
                    Title = "[Công Tác] Duyệt phiếu công tác của " + onsiteApplication.User.FullName + " ngày " + dateString,
                    Mail_cc = [],
                    JsonObject = JsonConvert.SerializeObject(onsiteApplicationReviewSendMail)
                };
                await _sendMailDynamicTemplateService.SendMailAsync(objSendMail);
            }
        }

        public async Task SendNotificationAsync(OnsiteApplicationNotificationModel onSiteApplication)
        {
            string dateString = (onSiteApplication.FromDate.Date == onSiteApplication.ToDate.Date) ?
                $"ngày {onSiteApplication.FromDate.Date:dd/MM/yyyy}" :
                $"từ ngày {onSiteApplication.FromDate.Date:dd/MM/yyyy} đến ngày {onSiteApplication.ToDate.Date:dd/MM/yyyy}";

            Dictionary<string, string> data = new()
            {
                { "Event", "Onsite" },
                { "Id", onSiteApplication.Id.ToString() }
            };

            if (onSiteApplication.Status == (int)EReviewStatus.Pending)
            {
                string title = "Yêu cầu duyệt phiếu công tác";
                string body = $"{onSiteApplication.UserFullName} xin đi công tác {dateString}. Dự án: {onSiteApplication.ProjectName}, địa điểm: {onSiteApplication.Location}, tính phí: {(onSiteApplication.IsCharge ? "có" : "không")}";

                data.Add("EventType", "Request");

                Notification notification = new()
                {
                    Title = title,
                    Body = body
                };

                var reviewerUserDevices = await _unitOfWork.DeviceRepository.GetByUserIdAsync(onSiteApplication.ReviewUserId);

                if (reviewerUserDevices.Count > 1)
                {
                    List<string> registrationTokens = reviewerUserDevices.Select(x => x.RegistrationToken).ToList()!;
                    var task = Task.Run(async () => await _firebaseMessageCloudService.SendMultiCastAsync(registrationTokens, notification, data));
                    task.Wait();
                }
                else
                {
                    var reviewerUserDevice = reviewerUserDevices.FirstOrDefault();
                    if (reviewerUserDevice == null) return;
                    string registrationToken = reviewerUserDevice.RegistrationToken!;
                    var task = Task.Run(async () => await _firebaseMessageCloudService.SendAsync(registrationToken, notification, data));
                    task.Wait();
                }
            }
            else
            {
                string title = "Duyệt phiếu công tác";
                string body = $"{(onSiteApplication.Status == EReviewStatus.Reviewed ? "Đồng ý" : "Từ chối")} phiếu công tác {dateString}.{(onSiteApplication.Status == EReviewStatus.Reviewed ? "" : $"Lý do: {onSiteApplication.ReviewNote}")}";

                data.Add("EventType", "Confirmed");

                Notification notification = new()
                {
                    Title = title,
                    Body = body
                };

                var userDevices = await _unitOfWork.DeviceRepository.GetByUserIdAsync(onSiteApplication.UserId);

                if (userDevices.Count > 1)
                {
                    List<string> registrationTokens = userDevices.Select(x => x.RegistrationToken).ToList()!;
                    var task = Task.Run(async () => await _firebaseMessageCloudService.SendMultiCastAsync(registrationTokens, notification, data));
                    task.Wait();
                }
                else
                {
                    var userDevice = userDevices.FirstOrDefault();
                    if (userDevice == null) return;
                    string registrationToken = userDevice.RegistrationToken!;
                    var task = Task.Run(async () => await _firebaseMessageCloudService.SendAsync(registrationToken, notification, data));
                    task.Wait();
                }
            }
        }

        public async Task<CombineResponseModel<OnsiteApplicationNotificationModel>> PrepareUpdateAsync(int id, OnsiteApplicationUpdateRequest applicationRequest, int userId)
        {
            var response = new CombineResponseModel<OnsiteApplicationNotificationModel>();

            bool isValidToSubmit = CommonHelper.ValidDateToSubmitApplication(applicationRequest.FromDate, EApplicationMessageType.OnsiteApplication, out string error);
            if (!isValidToSubmit)
            {
                response.ErrorMessage = error;
                return response;
            }

            if (string.IsNullOrEmpty(applicationRequest.FileUrls) && (applicationRequest.Files == null || !applicationRequest.Files.Any()))
            {
                response.ErrorMessage = "Bản báo cáo công tác không được trống";
                return response;
            }

            // Find application
            var application = await _unitOfWork.OnsiteApplicationRepository.GetByIdAsync(id);

            if (application == null)
            {
                response.ErrorMessage = "Phiếu công tác không tồn tại";
                return response;
            }

            // 0. Validation
            if (string.IsNullOrEmpty(applicationRequest.Location))
            {
                response.ErrorMessage = "Địa điểm công tác không được trống";
                return response;
            }

            if (string.IsNullOrEmpty(applicationRequest.OnsiteNote))
            {
                // Prevent null
                applicationRequest.OnsiteNote = "";
            }

            if (applicationRequest.FromDate > applicationRequest.ToDate)
            {
                response.ErrorMessage = "Từ ngày không được lớn hơn đến ngày";
                return response;
            }

            if (string.IsNullOrEmpty(applicationRequest.ProjectName))
            {
                response.ErrorMessage = "Tên dự án liên quan công tác không được trống";
                return response;
            }

            if (applicationRequest.PeriodType != EPeriodType.AllDay && applicationRequest.PeriodType != EPeriodType.FirstHalf && applicationRequest.PeriodType != EPeriodType.SecondHalf)
            {
                response.ErrorMessage = "Thời gian buổi công tác không hợp lệ";
                return response;
            }

            if (application.Status == (int)EReviewStatus.Rejected)
            {
                response.ErrorMessage = "Phiếu công tác đã bị từ chối";
                return response;
            }
            if (application.Status != (int)EReviewStatus.Pending)
            {
                response.ErrorMessage = "Phiếu công tác đã được duyệt";
                return response;
            }
            if (application.UserId != userId)
            {
                response.ErrorMessage = "Không thể sửa phiếu công tác của người khác";
                return response;
            }

            var dayOnsite = CalculateDayOnsite(applicationRequest.PeriodType, applicationRequest.FromDate, applicationRequest.ToDate);

            List<OnsiteApplication> onsiteApplicationByDate = await _unitOfWork.OnsiteApplicationRepository.GetByUserIdAndDateAsync(userId, applicationRequest.FromDate.Date, applicationRequest.ToDate.Date);
            if (onsiteApplicationByDate != null && onsiteApplicationByDate.Where(x => x.Id != id && x.Status == (int)EReviewStatus.Pending && (x.PeriodType == (int)applicationRequest.PeriodType || x.PeriodType != (int)EPeriodType.AllDay && applicationRequest.PeriodType == EPeriodType.AllDay)).Any())
            {
                response.ErrorMessage = "Trùng ngày công tác";
                return response;
            }

            // 1. Find Reviewer is existed.
            var reviewer = await _unitOfWork.UserInternalRepository.GetByIdAsync(applicationRequest.ReviewUserId);

            if (reviewer == null)
            {
                response.ErrorMessage = "Người duyệt không tồn tại";
                return response;
            }

            var holidays = await _unitOfWork.HolidayRepository.GetAllAsync();
            var holidayHelpers = holidays.Select(h => new HolidayHelper
            {
                Date = h.HolidayDate,
                IsHolidayByYear = h.IsHolidayByYear,
            }).ToList();

            var errorMessage = DateTimeHelper.ValidateDateRange(applicationRequest.FromDate, applicationRequest.ToDate, holidayHelpers);
            if (!string.IsNullOrEmpty(errorMessage))
            {
                response.ErrorMessage = errorMessage;
                return response;
            }

            var newAttachments = new List<string>();
            if (applicationRequest.Files != null)
            {
                foreach (var file in applicationRequest.Files)
                {
                    using (var ms = new MemoryStream())
                    {
                        await file.CopyToAsync(ms);
                        var fileBytes = ms.ToArray();
                        var filename = $"{DateTime.UtcNow.UTCToIct().Year}/{DateTime.UtcNow.UTCToIct().Month}/{Guid.NewGuid().ToString()}";
                        var imageUrl = await _blobService.UploadAsync(fileBytes, BlobContainerName.ScannedOnsite, filename, file.ContentType);
                        if (imageUrl == null)
                        {
                            response.ErrorMessage = "Tải hình lên lỗi";
                            return response;
                        }
                        newAttachments.Add(imageUrl.RelativeUrl!);
                    }
                }
            }

            var removeAttachments = new List<OnsiteApplicationFile>();
            if (string.IsNullOrEmpty(applicationRequest.FileUrls))
            {
                removeAttachments.AddRange(application.OnsiteApplicationFiles);
            }
            else
            {
                var stringFileUrls = applicationRequest.FileUrls.Split(",").ToList();
                if (stringFileUrls != null && stringFileUrls.Any())
                {
                    removeAttachments.AddRange(application.OnsiteApplicationFiles.Where(o => !stringFileUrls.Any(y => y.EndsWith(o.FileUrl))).ToList());
                }
            }

            application.UpdatedDate = DateTime.UtcNow.UTCToIct();
            application.FromDate = applicationRequest.FromDate;
            application.ToDate = applicationRequest.ToDate;
            application.OnsiteNote = applicationRequest.OnsiteNote;
            application.Location = applicationRequest.Location;
            application.ProjectName = applicationRequest.ProjectName;
            application.ReviewUserId = applicationRequest.ReviewUserId;
            application.PeriodType = (int)applicationRequest.PeriodType;
            application.NumberDayOnsite = dayOnsite;
            application.IsCharge = applicationRequest.IsCharge;

            if (removeAttachments.Any())
            {
                await _unitOfWork.OnsiteApplicationFileRepository.DeleteRangeAsync(removeAttachments);
            }
            if (newAttachments.Any())
            {
                await _unitOfWork.OnsiteApplicationFileRepository.CreateRangeAsync(newAttachments.Select(o => new OnsiteApplicationFile
                {
                    OnsiteApplicationId = application.Id,
                    FileUrl = o,
                    CreateDate = DateTime.UtcNow.UTCToIct()
                }).ToList());
            }

            await _unitOfWork.OnsiteApplicationRepository.UpdateAsync(application);
            await _unitOfWork.SaveChangesAsync();

            var onsiteApplicationData = new OnsiteApplicationNotificationModel
            {
                Id = application.Id,
                FromDate = application.FromDate,
                ToDate = application.ToDate,
                ReviewNote = application.ReviewNote,
                Status = (EReviewStatus)application.Status,
                ProjectName = application.ProjectName,
                Location = application.ReviewNote!,
                UserId = application.UserId,
                UserFullName = application.User.FullName!,
                ReviewUserId = application.ReviewUserId,
                IsCharge = application.IsCharge
            };

            response.Status = true;
            response.Data = onsiteApplicationData;

            return response;
        }
        #endregion

        #region Private Method
        private static decimal CalculateDayOnsite(EPeriodType periodType, DateTime fromDate, DateTime toDate)
        {
            // Đi công tác không tính ngày lễ.
            if (periodType != EPeriodType.AllDay)
            {
                return 0.5m;
            }

            if (fromDate.Date == toDate.Date)
            {
                return 1;
            }

            decimal dayOnsite = 0;
            foreach (DateTime day in DateTimeHelper.EachDay(fromDate.Date, toDate.Date))
            {
                dayOnsite += 1;
            }
            return dayOnsite;
        }

        public async Task<List<OnsiteApplicationMobilePagingModel>> GetAllWithPagingMobileAsync(OnsiteApplicationMobileCriteriaModel searchModel, int userId)
        {
            var recordsRaw = await _unitOfWork.OnsiteApplicationRepository.GetAllWithPagingMobileAsync(searchModel, userId);

            var records = recordsRaw.Select(x => new OnsiteApplicationMobilePagingModel
            {
                Id = x.Id,
                RegisterDate = x.RegisterDate,
                FromDate = x.FromDate,
                ToDate = x.ToDate,
                ReviewDate = x.ReviewDate,
                NumberDayOnsite = x.NumberDayOnsite,
                Note = x.Note,
                Status = x.Status,
                ProjectName = x.ProjectName,
                OnsiteNote = x.OnsiteNote,
                Location = x.Location,
                PeriodType = x.PeriodType,
                TotalRecord = x.TotalRecord,
                User = new EmployeeProfile
                {
                    Id = x.UserId,
                    Name = x.UserName,
                    Avatar = x.UserAvatar,
                    Gender = x.UserGender,
                    JobTitle = x.UserJobTitle
                },
                ReviewUser = new EmployeeProfile
                {
                    Id = x.ReviewUserId,
                    Name = x.ReviewUserName,
                    Avatar = x.ReviewUserAvatar,
                    Gender = x.ReviewUserGender,
                    JobTitle = x.ReviewUserJobTitle
                },
                Ischarge = x.IsCharge
            }).ToList();

            var totalRecords = records.FirstOrDefault();
            records.Remove(totalRecords);

            return records;
        }
        #endregion
    }
}
