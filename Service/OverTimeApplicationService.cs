using FirebaseAdmin.Messaging;
using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Enums;
using InternalPortal.ApplicationCore.Helpers;
using InternalPortal.ApplicationCore.Interfaces;
using InternalPortal.ApplicationCore.Interfaces.Business;
using InternalPortal.ApplicationCore.Interfaces.Email;
using InternalPortal.ApplicationCore.Interfaces.Utilities.Firebase;
using InternalPortal.ApplicationCore.Models;
using InternalPortal.ApplicationCore.Models.CriteriaModel;
using InternalPortal.ApplicationCore.Models.Holiday;
using InternalPortal.ApplicationCore.Models.OverTimeApplicationModel;
using InternalPortal.ApplicationCore.Models.PagingModel;
using InternalPortal.ApplicationCore.Models.User;
using InternalPortal.ApplicationCore.Models.UserInternal;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using static InternalPortal.ApplicationCore.ValueObjects.Global;

namespace InternalPortal.Infrastructure.Services.Business
{
    public class OverTimeApplicationService : IOverTimeApplicationService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ISendMailDynamicTemplateService _sendMailDynamicTemplateService;
        private readonly string _frontEndDomain;
        private readonly IFirebaseMessageCloudService _firebaseMessageCloudService;
        public OverTimeApplicationService(
            IUnitOfWork unitOfWork,
            ISendMailDynamicTemplateService sendMailDynamicTemplateService,
            IConfiguration configuration,
            IFirebaseMessageCloudService firebaseMessageCloudService
            )
        {
            _unitOfWork = unitOfWork;
            _sendMailDynamicTemplateService = sendMailDynamicTemplateService;
            _frontEndDomain = configuration["FrontEndDomain"]!;
            _firebaseMessageCloudService = firebaseMessageCloudService;
        }

        #region Public Method
        public async Task<PagingResponseModel<OverTimePagingModel>> GetAllWithPagingAsync(OverTimeCriteriaModel requestModel, int userId)
        {
            var recordsRaw = await _unitOfWork.OverTimeApplicationRepository.GetAllWithPagingAsync(requestModel, userId);

            var records = recordsRaw.Select(x => new OverTimePagingModel
            {
                Id = x.Id,
                RegisterDate = x.RegisterDate,
                FromDate = x.FromDate,
                ToDate = x.ToDate,
                OverTimeHour = x.OverTimeHour,
                OverTimeNote = x.OverTimeNote,
                ReviewNote = x.ReviewNote,
                Status = x.Status,
                CreatedDate = x.CreatedDate,
                UpdatedDate = x.UpdatedDate,
                UserId = x.UserId,
                ReviewUserId = x.ReviewUserId,
                TotalRecord = x.TotalRecord,
                BreakHour = x.BreakHour,
                ReviewUser = x.ReviewUser,
                UserName = x.UserName,
                JobTitle = x.JobTitle
            }).ToList();

            var totalRecords = records.FirstOrDefault();
            records.Remove(totalRecords!);

            var res = new PagingResponseModel<OverTimePagingModel>
            {
                Items = records,
                TotalRecord = totalRecords!.TotalRecord ?? 0
            };

            return res;
        }

        public async Task<CombineResponseModel<OvertimeApplicationNotificationModel>> PrepareCreateAsync(OverTimeApplicationRequest request, UserDtoModel user)
        {
            var response = new CombineResponseModel<OvertimeApplicationNotificationModel>();
            if (string.IsNullOrEmpty(request.OverTimeNote))
            {
                response.ErrorMessage = "Lí do làm thêm không được trống";
                return response;
            }

            // 1. Find Reviewer is existed.
            var reviewer = await _unitOfWork.UserInternalRepository.GetByIdAsync(request.ReviewUserId);

            if (reviewer == null)
            {
                response.ErrorMessage = "Người duyệt không tồn tại";
                return response;
            }

            var application = new OverTimeApplication
            {
                ReviewUserId = reviewer.Id
            };

            if (request.FromDate > request.ToDate)
            {
                response.ErrorMessage = "Từ ngày không thể nhỏ hơn đến ngày";
                return response;
            }

            var holidays = await _unitOfWork.HolidayRepository.GetAllAsync();
            var holidayHelpers = holidays.Select(h => new HolidayHelper
            {
                Date = h.HolidayDate,
                IsHolidayByYear = h.IsHolidayByYear,
            }).ToList();

            if (!request.ToDate.IsValidToDateForOT(holidayHelpers))
            {
                response.ErrorMessage = "Đã quá hạn gửi đơn! Vui lòng liên hệ HR";
                return response;
            }

            application.FromDate = request.FromDate;
            application.ToDate = request.ToDate;

            // 3. Calculate Overtime Hours.      
            application.OverTimeHour = CalculateOverTimeHours(application.ToDate, application.FromDate);

            if (request.BreakHour >= application.OverTimeHour)
            {
                response.ErrorMessage = "Giờ làm thêm nên lớn hơn giờ nghỉ";
                return response;
            }

            application.Status = (int)EReviewStatus.Pending;
            application.OverTimeNote = request.OverTimeNote;
            application.RegisterDate = DateTime.UtcNow.UTCToIct();
            application.CreatedDate = DateTime.UtcNow.UTCToIct();
            application.BreakHour = request.BreakHour.GetValueOrDefault();
            application.UserId = user.Id;

            await _unitOfWork.OverTimeApplicationRepository.CreateAsync(application);
            await _unitOfWork.SaveChangesAsync();

            var overtimeApplicationData = new OvertimeApplicationNotificationModel()
            {
                Id = application.Id,
                FromDate = application.FromDate,
                ToDate = application.ToDate,
                Status = (EReviewStatus)application.Status,
                OverTimeNote = application.OverTimeNote!,
                ReviewNote = application.ReviewNote,
                ReviewUserId = application.ReviewUserId,
                UserId = application.UserId,
                UserFullName = user.FullName,
            };

            response.Status = true;
            response.Data = overtimeApplicationData;

            return response;
        }

        public async Task SendMailAsync(int applicationId)
        {
            var overtimeApplication = await _unitOfWork.OverTimeApplicationRepository.GetByIdAsync(applicationId);
            if (overtimeApplication == null)
                return;

            // Title
            string dateString = overtimeApplication.FromDate.Date == overtimeApplication.ToDate.Date ? overtimeApplication.FromDate.Date.ToString("dd/MM/yyyy") : overtimeApplication.FromDate.Date.ToString("dd/MM/yyyy") + " - " + overtimeApplication.ToDate.Date.ToString("dd/MM/yyyy");
            // Detail DateTime
            string dateData = overtimeApplication.FromDate.ToString("dd/MM/yyyy HH:mm") + " - " + overtimeApplication.ToDate.ToString("dd/MM/yyyy HH:mm");
            if (overtimeApplication.Status == (int)EReviewStatus.Pending)
            {
                OverTimeApplicationSendMail overTimeApplicationSendMail = new()
                {
                    DateData = dateData,
                    Register = overtimeApplication.User.FullName,
                    Reviewer = overtimeApplication.ReviewUser.FullName!,
                    ReviewLink = _frontEndDomain + Constant.ReviewOverTimePath,
                    Reason = overtimeApplication.OverTimeNote!
                };
                ObjSendMail objSendMail = new()
                {
                    FileName = "OverTimeTemplate.html",
                    Mail_To = [overtimeApplication.ReviewUser.Email!],
                    Title = "[Làm Thêm] Yêu cầu duyệt đơn xin làm thêm giờ của " + overtimeApplication.User.FullName + " ngày " + dateString,
                    Mail_cc = [],
                    JsonObject = JsonConvert.SerializeObject(overTimeApplicationSendMail)
                };
                await _sendMailDynamicTemplateService.SendMailAsync(objSendMail);
            }
            else
            {
                OverTimeApplicationReviewSendMail overTimeApplicationReviewSendMail = new()
                {
                    DateData = dateData,
                    IsAccept = overtimeApplication.Status == (int)EReviewStatus.Reviewed,
                    Register = overtimeApplication.User.FullName,
                    RegisterLink = _frontEndDomain + Constant.RegistOverTimePath,
                    ReasonReject = overtimeApplication.ReviewNote,
                    Reviewer = overtimeApplication.ReviewUser.FullName!,
                    Reason = overtimeApplication.OverTimeNote
                };
                ObjSendMail objSendMail = new()
                {
                    FileName = "OverTimeReviewTemplate.html",
                    Mail_To = [overtimeApplication.User.Email!],
                    Title = "[Làm Thêm] Duyệt đơn xin làm thêm giờ của " + overtimeApplication.User.FullName + " ngày " + dateString,
                    Mail_cc = [],
                    JsonObject = JsonConvert.SerializeObject(overTimeApplicationReviewSendMail)
                };
                await _sendMailDynamicTemplateService.SendMailAsync(objSendMail);
            }
        }

        public async Task SendNotificationAsync(OvertimeApplicationNotificationModel overtimeApplication)
        {
            string dateString = (overtimeApplication.FromDate.Date == overtimeApplication.ToDate.Date) ?
                $"vào ngày {overtimeApplication.FromDate:dd/MM/yyyy} từ {overtimeApplication.FromDate:HH:mm} đến {overtimeApplication.ToDate:HH:mm}" :
                $"từ ngày {overtimeApplication.FromDate:dd/MM/yyyy HH:mm} đến ngày {overtimeApplication.ToDate:dd/MM/yyyy HH:mm}";

            Dictionary<string, string> data = new()
            {
                { "Event", "Overtime" },
                { "Id", overtimeApplication.Id.ToString() }
            };

            if (overtimeApplication.Status == (int)EReviewStatus.Pending)
            {
                string title = "Yêu cầu duyệt đơn làm thêm";
                string body = $"{overtimeApplication.UserFullName} xin làm thêm {dateString}. Lý do: {overtimeApplication.OverTimeNote}";

                data.Add("EventType", "Request");

                Notification notification = new()
                {
                    Title = title,
                    Body = body
                };

                var reviewerUserDevices = await _unitOfWork.DeviceRepository.GetByUserIdAsync(overtimeApplication.ReviewUserId);

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
                string title = "Duyệt đơn làm thêm";
                string body = $"{(overtimeApplication.Status == EReviewStatus.Reviewed ? "Đồng ý" : "Từ chối")} đơn làm thêm {dateString}.{(overtimeApplication.Status == EReviewStatus.Reviewed ? "" : $"Lý do: {overtimeApplication.ReviewNote}")}";

                data.Add("EventType", "Confirmed");

                Notification notification = new()
                {
                    Title = title,
                    Body = body
                };

                var userDevices = await _unitOfWork.DeviceRepository.GetByUserIdAsync(overtimeApplication.ReviewUserId);

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

        public async Task<CombineResponseModel<OvertimeApplicationNotificationModel>> PrepareUpdateAsync(int applicationId, OverTimeApplicationRequest applicationRequest, int userId)
        {
            var response = new CombineResponseModel<OvertimeApplicationNotificationModel>();
            if (string.IsNullOrEmpty(applicationRequest.OverTimeNote))
            {
                response.ErrorMessage = "Lí do làm thêm không được trống";
                return response;
            }

            var application = await _unitOfWork.OverTimeApplicationRepository.GetByIdAsync(applicationId);
            if (application == null)
            {
                response.ErrorMessage = "Đơn xin nghỉ phép không tồn tại";
                return response;
            }

            DateTime fromDate = applicationRequest.FromDate;
            DateTime toDate = applicationRequest.ToDate;
            if (fromDate > toDate)
            {
                response.ErrorMessage = "Từ ngày không thể nhỏ hơn đến ngày";
                return response;
            }

            var holidays = await _unitOfWork.HolidayRepository.GetAllAsync();
            var holidayHelpers = holidays.Select(h => new HolidayHelper
            {
                Date = h.HolidayDate,
                IsHolidayByYear = h.IsHolidayByYear,
            }).ToList();

            if (!toDate.IsValidToDateForOT(holidayHelpers))
            {
                response.ErrorMessage = "Ngày kết thúc quá hạn! Vui lòng liên hệ HR";
                return response;
            }

            var userReview = await _unitOfWork.UserInternalRepository.GetByIdAsync(applicationRequest.ReviewUserId);
            if (userReview == null)
            {
                response.ErrorMessage = "Người duyệt không tồn tại";
                return response;
            }

            if (application.Status != (int)EReviewStatus.Pending)
            {
                response.ErrorMessage = "Đơn xin làm thêm giờ đã được duyệt";
                return response;
            }
            if (application.UserId != userId)
            {
                response.ErrorMessage = "Không thể sửa đơn nghỉ phép của người khác";
                return response;
            }

            if (application.BreakHour >= application.OverTimeHour)
            {
                response.ErrorMessage = "Giờ làm thêm nên lớn hơn giờ nghỉ";
                return response;
            }

            application.FromDate = fromDate;
            application.ToDate = toDate;
            application.OverTimeHour = CalculateOverTimeHours(application.ToDate, application.FromDate);
            application.UpdatedDate = DateTime.UtcNow.UTCToIct();
            application.OverTimeNote = applicationRequest.OverTimeNote;
            application.ReviewUserId = applicationRequest.ReviewUserId;
            application.BreakHour = applicationRequest.BreakHour.GetValueOrDefault();

            await _unitOfWork.OverTimeApplicationRepository.UpdateAsync(application);
            await _unitOfWork.SaveChangesAsync();

            var overtimeApplicationData = new OvertimeApplicationNotificationModel()
            {
                Id = application.Id,
                FromDate = application.FromDate,
                ToDate = application.ToDate,
                Status = (EReviewStatus)application.Status,
                OverTimeNote = application.OverTimeNote!,
                ReviewNote = application.ReviewNote,
                ReviewUserId = application.ReviewUserId,
                UserId = application.UserId,
                UserFullName = application.User.FullName,
            };

            response.Status = true;
            response.Data = overtimeApplicationData;

            return response;

        }
        public async Task<List<OverTimePagingMobileModel>> GetAllWithPagingForMobileAsync(OverTimeCriteriaModel requestModel, int userId)
        {
            var recordsRaw = await _unitOfWork.OverTimeApplicationRepository.GetAllWithPagingForMobileAsync(requestModel, userId);

            var records = recordsRaw.Select(x => new OverTimePagingMobileModel
            {
                Id = x.Id,
                FromDate = x.FromDate,
                ToDate = x.ToDate,
                Note = x.Note,
                Status = x.Status,
                BreakTime = x.BreakTime,
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
                }
            }).ToList();

            var totalRecords = records.FirstOrDefault();
            records.Remove(totalRecords);

            return records;
        }
        #endregion

        #region Private Method
        private static decimal CalculateOverTimeHours(DateTime toDate, DateTime fromDate)
        {
            if (toDate < fromDate)
                return 0;

            // Mapping date not include seconds and miliseconds.
            DateTime toDateMapping = new(toDate.Year, toDate.Month, toDate.Day, toDate.Hour, toDate.Minute, 0);
            DateTime fromDateMapping = new(fromDate.Year, fromDate.Month, fromDate.Day, fromDate.Hour, fromDate.Minute, 0);

            return (decimal)toDateMapping.Subtract(fromDateMapping).TotalHours;
        }
        #endregion
    }
}
