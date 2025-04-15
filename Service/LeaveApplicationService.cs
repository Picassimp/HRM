using FirebaseAdmin.Messaging;
using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Enums;
using InternalPortal.ApplicationCore.Helpers;
using InternalPortal.ApplicationCore.Interfaces;
using InternalPortal.ApplicationCore.Interfaces.Business;
using InternalPortal.ApplicationCore.Interfaces.Email;
using InternalPortal.ApplicationCore.Interfaces.Utilities.Firebase;
using InternalPortal.ApplicationCore.Models;
using InternalPortal.ApplicationCore.Models.Holiday;
using InternalPortal.ApplicationCore.Models.LeaveApplication;
using InternalPortal.ApplicationCore.Models.User;
using InternalPortal.ApplicationCore.Models.UserInternal;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using static InternalPortal.ApplicationCore.ValueObjects.Global;

namespace InternalPortal.Infrastructure.Services.Business
{
    public class LeaveApplicationService : ILeaveApplicationService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IUserInternalService _userInternalService;
        private readonly string _frontEndDomain;
        private readonly ISendMailDynamicTemplateService _sendMailDynamicTemplateService;
        private readonly IFirebaseMessageCloudService _firebaseMessageCloudService;

        public LeaveApplicationService(
            IUnitOfWork unitOfWork,
            IUserInternalService userInternalService,
            IConfiguration configuration,
            ISendMailDynamicTemplateService sendMailDynamicTemplateService,
            IFirebaseMessageCloudService firebaseMessageCloudService
            )
        {
            _unitOfWork = unitOfWork;
            _userInternalService = userInternalService;
            _frontEndDomain = configuration["FrontEndDomain"]!;
            _sendMailDynamicTemplateService = sendMailDynamicTemplateService;
            _firebaseMessageCloudService = firebaseMessageCloudService;
        }

        #region Private Method
        private async Task<decimal> CalculateDayOffAsync(EPeriodType periodType, DateTime fromDate, DateTime toDate)
        {
            var holidays = await _unitOfWork.HolidayRepository.GetAllAsync();
            ShiftInfo shiftInfo = new ShiftInfo();
            if (periodType != EPeriodType.AllDay)
            {
                if (DateTimeHelper.IsHoliday(holidays, fromDate.Date, shiftInfo.IsWorkWeekend))
                    return 0;
                return 0.5m;
            }
            if (fromDate.Date == toDate.Date)
            {
                if (DateTimeHelper.IsHoliday(holidays, fromDate.Date, shiftInfo.IsWorkWeekend))
                    return 0;
                return 1;
            }
            decimal dayOff = 0;
            foreach (DateTime day in DateTimeHelper.EachDay(fromDate.Date, toDate.Date))
            {
                if (DateTimeHelper.IsHoliday(holidays, day, shiftInfo.IsWorkWeekend))
                {
                    continue;
                }
                dayOff += 1;
            }
            return dayOff;
        }

        private static bool CanUseOldYearRemainDayOff(DateTime applicationRequest, decimal dayOff, decimal pendingDayOff, UserInternal user, out string error)
        {
            error = string.Empty;
            var borrowedDayOffAllow = user.BorrowedDayOff - user.UsedBorrowedDayOff > 0 ? user.BorrowedDayOff - user.UsedBorrowedDayOff : 0;
            if (DateTimeHelper.IsGreaterOrEqualAprilFirst(applicationRequest)) //khi qua quý 1 của năm nay sẽ dùng ngày nghỉ của năm nay
            {
                if (dayOff + pendingDayOff > user.YearOffDay + borrowedDayOffAllow)
                {
                    error = $"Sau ngày 01/04 thì không được sử dụng phép năm cũ, số NP năm nay: ({user.YearOffDay - pendingDayOff + borrowedDayOffAllow})";
                }
                return false;
            }
            else //khi chưa qua quý 1 của năm nay sẽ dùng ngày nghỉ của năm nay + năm ngoái
            {
                if (dayOff + pendingDayOff > user.YearOffDay + user.RemainDayOffLastYear + borrowedDayOffAllow)
                {
                    error = "Số ngày nghỉ không thể lớn hơn số ngày còn lại";
                }
                return true;
            }
        }
        #endregion

        public async Task<CombineResponseModel<LeaveApplicationNotificationModel>> PrepareCreateAsync(LeaveApplicationRequest request, UserDtoModel register)
        {
            var res = new CombineResponseModel<LeaveApplicationNotificationModel>();

            var error = string.Empty;
            bool isValidToSubmit = CommonHelper.ValidDateToSubmitApplication(request.FromDate, EApplicationMessageType.LeaveApplication, out error);
            if (!isValidToSubmit)
            {
                res.ErrorMessage = error;
                return res;
            }

            if (request.FromDate.Date > request.ToDate.Date)
            {
                res.ErrorMessage = "Từ ngày không thể nhỏ hơn đến ngày";
                return res;
            }

            var leaveType = await _unitOfWork.LeaveApplicationTypeRepository.GetByIdAsync(request.LeaveApplicationTypeId);
            if (leaveType == null)
            {
                res.ErrorMessage = "Loại nghỉ phép không tồn tại";
                return res;
            }

            if (!_userInternalService.IsOfficialStaff(register.GroupUserName) && leaveType.IsSubTractCumulation)
            {
                res.ErrorMessage = "Loại ngày nghỉ không phù hợp";
                return res;
            }

            var leaveApplicationByDate = await _unitOfWork.LeaveApplicationRepository.GetByUserIdAndDateAsync(register.Id, request.FromDate.Date, request.ToDate.Date);
            if (leaveApplicationByDate != null && leaveApplicationByDate.Where(x => x.ReviewStatus != (int)EReviewStatus.Rejected 
                                                    && (x.PeriodType == (int)request.PeriodType || x.PeriodType != (int)EPeriodType.AllDay 
                                                    && request.PeriodType == (int)EPeriodType.AllDay)).Any())
            {
                res.ErrorMessage = "Trùng ngày nghỉ phép";
                return res;
            }

            var reviewer = await _unitOfWork.UserInternalRepository.GetByIdAsync(request.ReviewUserId);
            if (reviewer == null)
            {
                res.ErrorMessage = "Người duyệt không tồn tại";
                return res;
            }

            if (request.RelatedUserIds.Any())
            {
                foreach (var relatedUserId in request.RelatedUserIds)
                {
                    var relatedUser = await _unitOfWork.UserInternalRepository.GetByIdAsync(relatedUserId);
                    if (relatedUser == null)
                    {
                        res.ErrorMessage = "Người liên quan không tồn tại";
                        return res;
                    }
                }
            }

            if (request.HandoverUserId.HasValue)
            {
                var handoverUser = await _unitOfWork.UserInternalRepository.GetByIdAsync(request.HandoverUserId.Value);
                if (handoverUser == null)
                {
                    res.ErrorMessage = "Người bàn giao không tồn tại";
                    return res;
                }
            }

            var holidays = await _unitOfWork.HolidayRepository.GetAllAsync();
            var holidayHelpers = holidays.Select(h => new HolidayHelper
            {
                Date = h.HolidayDate,
                IsHolidayByYear = h.IsHolidayByYear,
            }).ToList();

            var errorMessage = DateTimeHelper.ValidateDateRange(request.FromDate, request.ToDate, holidayHelpers);
            if (!string.IsNullOrEmpty(errorMessage))
            {
                res.ErrorMessage = errorMessage;
                return res;
            }

            var dayOff = await CalculateDayOffAsync(request.PeriodType, request.FromDate, request.ToDate);
            // Số ngày phép sử dụng từ ngày nghỉ năm trước
            decimal numberDayOffLastYear = 0;
            decimal borrowedDayOff = 0;

            // Note: chổ này get user lên lại dùng để sử dụng YearOffDay và RemainDayOffLastYear 
            // Không mapping vào user dto model trong middleware vì đang sử dụng cache => có thể xảy ra tình huống sử dụng data cũ => bug
            var user = await _unitOfWork.UserInternalRepository.GetByIdAsync(register.Id);
            if (user == null)
            {
                res.ErrorMessage = "Người đăng ký không tồn tại";
                return res;
            }

            if (leaveType.IsSubTractCumulation && leaveType.Name != Constant.SICK_LEAVE_APPLICATION_TYPE)
            {
                var pendings = await _unitOfWork.LeaveApplicationRepository.GetPendingByUserIdAsync(user.Id);
                var pendingDayOff = pendings.Sum(x => x.NumberDayOff);

                if (CanUseOldYearRemainDayOff(request.FromDate, dayOff, pendingDayOff, user, out error))
                {
                    #region Lưu lại số ngày phép của các cột ngày phép người dùng sử dụng
                    if (user.RemainDayOffLastYear > 0)
                    {
                        // 434: Tính số ngày còn lại của năm ngoái khi đã trừ đi số ngày phép quy đổi đang chờ duyệt
                        var now = DateTime.UtcNow.UTCToIct();
                        var exchangeDayOff = await _unitOfWork.ExchangeDayOffRepository.GetPendingByUserIdAndYearAsync(user.Id, now.Year);

                        var allowDayOffLastYear = user.RemainDayOffLastYear - pendings.Sum(o => o.NumberDayOffLastYear) - (exchangeDayOff?.DayOffExchange ?? 0);
                        numberDayOffLastYear = allowDayOffLastYear > dayOff ? dayOff : allowDayOffLastYear;
                    }
                    #endregion
                }

                if (!string.IsNullOrEmpty(error))
                {
                    res.ErrorMessage = error;
                    return res;
                }

                var pendingLeaveDay = pendings.Sum(o => o.NumberDayOff - o.BorrowedDayOff - o.NumberDayOffLastYear);
                var pendingBorrowedDay = pendings.Sum(o => o.BorrowedDayOff);

                var borrowedDayOffAllow = user.BorrowedDayOff - user.UsedBorrowedDayOff > 0 ? user.BorrowedDayOff - user.UsedBorrowedDayOff : 0;
                if (dayOff > user.YearOffDay + numberDayOffLastYear + borrowedDayOffAllow - pendingLeaveDay - pendingBorrowedDay)
                {
                    res.ErrorMessage = "Số ngày nghỉ không thế lớn hơn số ngày cho phép";
                    return res;
                }

                // Tính số ngày phép sử dụng phép năm nay
                var dayOffCurrentYear = dayOff - numberDayOffLastYear;
                if (dayOffCurrentYear > user.YearOffDay - pendingLeaveDay)
                {
                    // Số ngày được phép ứng còn lại
                    var borrowedDayOffAllowRemaining = borrowedDayOffAllow - pendingBorrowedDay;

                    // Tính số ngày nghỉ đã trừ phép năm
                    var dayOffWithOutYearOffDay = dayOffCurrentYear - (user.YearOffDay - pendingLeaveDay);

                    borrowedDayOff = borrowedDayOffAllowRemaining > dayOffWithOutYearOffDay ? dayOffWithOutYearOffDay : borrowedDayOffAllowRemaining;
                }
            }

            if (leaveType.Name == Constant.SICK_LEAVE_APPLICATION_TYPE)
            {
                var pendingSick = await _unitOfWork.LeaveApplicationRepository.GetPendingSickByUserIdAsync(user.Id);
                if (dayOff + pendingSick.Sum(x => x.NumberDayOff) > user.SickDayOff)
                {
                    res.ErrorMessage = "Số ngày nghỉ không thể lớn hơn số ngày còn lại";
                    return res;
                }
            }

            var leaveApplication = new LeaveApplication()
            {
                CreatedDate = DateTime.UtcNow.UTCToIct(),
                FromDate = request.FromDate.Date,
                ToDate = request.ToDate.Date,
                LeaveApplicationNote = request.LeaveApplicationNote,
                LeaveApplicationTypeId = request.LeaveApplicationTypeId,
                RegisterDate = DateTime.UtcNow.UTCToIct(),
                ReviewStatus = (int)EReviewStatus.Pending,
                ReviewUserId = request.ReviewUserId,
                UserId = user.Id,
                NumberDayOff = dayOff,
                PeriodType = (int)request.PeriodType,
                RelatedUserId = request.RelatedUserId,
                NumberDayOffLastYear = numberDayOffLastYear,
                RelatedUserIds = request.RelatedUserIds.JoinComma(),
                HandoverUserId = request.HandoverUserId,
                IsAlertCustomer = request.IsAlertCustomer ?? false,
                BorrowedDayOff = borrowedDayOff
            };

            await _unitOfWork.LeaveApplicationRepository.CreateAsync(leaveApplication);
            await _unitOfWork.SaveChangesAsync();

            var response = new LeaveApplicationNotificationModel
            {
                Id = leaveApplication.Id,
                FromDate = leaveApplication.FromDate,
                ToDate = leaveApplication.ToDate,
                ReviewStatus = leaveApplication.ReviewStatus,
                LeaveApplicationNote = leaveApplication.LeaveApplicationNote,
                ReviewNote = leaveApplication.ReviewNote,
                ReviewUserId = leaveApplication.ReviewUserId,
                UserId = leaveApplication.UserId,
                UserFullName = leaveApplication.User.FullName!
            };
            res.Status = true;
            res.Data = response;
            return res;
        }

        public async Task<PagingResponseModel<LeaveApplicationPagingModel>> GetAllWithPagingAsync(LeaveApplicationPagingRequest request, int userId)
        {
            var recordsRaw = await _unitOfWork.LeaveApplicationRepository.GetAllWithPagingAsync(request, userId);

            var totalRecords = recordsRaw.FirstOrDefault();
            if (totalRecords != null)
            {
                recordsRaw.Remove(totalRecords);
            }

            var records = recordsRaw.Select(x => new LeaveApplicationPagingModel
            {
                Id = x.Id,
                RegisterDate = x.RegisterDate,
                FromDate = x.FromDate,
                ToDate = x.ToDate,
                LeaveApplicationNote = x.LeaveApplicationNote,
                LeaveApplicationType = x.LeaveApplicationType,
                NumberDayOff = x.NumberDayOff,
                ReviewNote = x.ReviewNote,
                ReviewStatus = x.ReviewStatus,
                ReviewUser = x.ReviewUser,
                UserId = x.UserId,
                ReviewUserId = x.ReviewUserId,
                TotalRecord = x.TotalRecord,
                ReviewDate = x.ReviewDate,
                UserName = x.UserName,
                PeriodType = x.PeriodType,
                JobTitle = x.JobTitle,
                RelatedUserIds = !string.IsNullOrEmpty(x.RelatedUserIds) ? x.RelatedUserIds.Split(',').Select(int.Parse).ToList() : new List<int>(),
                IsAlertCustomer = x.IsAlertCustomer,
                HandoverUserId = x.HandoverUserId,
                HandoverUserName = x.HandoverUserName ?? "",
                BorrowedDayOff = x.BorrowedDayOff,
                Avatar = x.Avatar ?? ""
            }).ToList();

            var res = new PagingResponseModel<LeaveApplicationPagingModel>
            {
                Items = records,
                TotalRecord = totalRecords?.TotalRecord ?? 0
            };

            return res;
        }

        public async Task SendEmailAsync(int leaveApplicationId)
        {
            var leaveApplication = await _unitOfWork.LeaveApplicationRepository.GetByIdAsync(leaveApplicationId);
            if (leaveApplication == null)
            {
                return;
            }
            string dateString = leaveApplication.FromDate.Date == leaveApplication.ToDate.Date ? leaveApplication.FromDate.Date.ToString("dd/MM/yyyy") : leaveApplication.FromDate.Date.ToString("dd/MM/yyyy") + " - " + leaveApplication.ToDate.Date.ToString("dd/MM/yyyy");
            var periodType = leaveApplication.PeriodType != (int)EPeriodType.AllDay ? (leaveApplication.PeriodType == (int)EPeriodType.FirstHalf ? " (Nghỉ buổi sáng)" : " (Nghỉ buổi chiều)") : string.Empty;
            var dateDate = dateString + periodType;
            if (leaveApplication.ReviewStatus == (int)EReviewStatus.Pending)
            {
                #region Gửi email cho người duyệt
                var leaveApplicationSendMail = new LeaveApplicationSendMail()
                {
                    DateData = dateDate,
                    LeaveApplicationTypeName = leaveApplication.LeaveApplicationType.Name + (leaveApplication.PeriodType != (int)EPeriodType.AllDay ? (leaveApplication.PeriodType == (int)EPeriodType.FirstHalf ? " - Nghỉ buổi sáng" : " - Nghỉ buổi chiều") : ""),
                    Register = leaveApplication.User.FullName!,
                    Reviewer = leaveApplication.ReviewUser.FullName!,
                    ReviewLink = _frontEndDomain + Constant.ReviewLeaveApplicationPath,
                    Reason = leaveApplication.LeaveApplicationNote!,
                    HandoverUser = leaveApplication.HandoverUser?.FullName,
                    IsAlertCustomer = leaveApplication.IsAlertCustomer ? "Đã thông báo" : "Chưa thông báo"
                };
                var objSendMail = new ObjSendMail()
                {
                    FileName = "TimeOffTemplate.html",
                    Mail_To = new List<string>() { leaveApplication.ReviewUser.Email! },
                    Title = "[Nghỉ Phép] Yêu cầu duyệt đơn xin nghỉ phép của " + leaveApplication.User.FullName + " ngày " + dateString,
                    Mail_cc = new List<string>(),
                    JsonObject = JsonConvert.SerializeObject(leaveApplicationSendMail)
                };
                await _sendMailDynamicTemplateService.SendMailAsync(objSendMail);
                #endregion

                #region Gửi email cho người bàn giao
                if (leaveApplication.HandoverUserId.HasValue && leaveApplication.HandoverUser!.Email != leaveApplication.User.Email)
                {
                    leaveApplicationSendMail = new LeaveApplicationSendMail()
                    {
                        DateData = dateDate,
                        LeaveApplicationTypeName = leaveApplication.LeaveApplicationType.Name + (leaveApplication.PeriodType != (int)EPeriodType.AllDay ? (leaveApplication.PeriodType == (int)EPeriodType.FirstHalf ? " - Nghỉ buổi sáng" : " - Nghỉ buổi chiều") : ""),
                        Register = leaveApplication.User.FullName!,
                        HandoverUser = leaveApplication.HandoverUser.FullName,
                        ReviewLink = _frontEndDomain + Constant.ReviewLeaveApplicationPath,
                        Reason = leaveApplication.LeaveApplicationNote!
                    };
                    objSendMail = new ObjSendMail()
                    {
                        FileName = "TimeOffHandoverUserTemplate.html",
                        Mail_To = new List<string>() { leaveApplication.HandoverUser.Email! },
                        Title = "[Nghỉ Phép] Đơn bàn giao công việc của " + leaveApplication.User.FullName + " ngày " + dateDate,
                        Mail_cc = new List<string>(),
                        JsonObject = JsonConvert.SerializeObject(leaveApplicationSendMail)
                    };
                    await _sendMailDynamicTemplateService.SendMailAsync(objSendMail);
                }
                #endregion

                #region Gửi email cho người liên quan
                // Check Related user exists
                if (!string.IsNullOrEmpty(leaveApplication.RelatedUserIds))
                {
                    var relatedUserIds = leaveApplication.RelatedUserIds.Split(',').Select(int.Parse).ToList();
                    foreach (var relatedUserId in relatedUserIds)
                    {
                        var relatedUser = await _unitOfWork.UserInternalRepository.GetByIdAsync(relatedUserId);
                        if (relatedUser != null && !relatedUser.HasLeft && !relatedUser.IsDeleted)
                        {
                            LeaveApplicationSendMail relatedLeaveApplicationSendMail = new LeaveApplicationSendMail()
                            {
                                DateData = dateDate,
                                LeaveApplicationTypeName = leaveApplication.LeaveApplicationType.Name + (leaveApplication.PeriodType != (int)EPeriodType.AllDay ? (leaveApplication.PeriodType == (int)EPeriodType.FirstHalf ? " - Nghỉ buổi sáng" : " - Nghỉ buổi chiều") : ""),
                                Register = leaveApplication.User.FullName!,
                                Reviewer = relatedUser.FullName!,
                                Reason = leaveApplication.LeaveApplicationNote!
                            };

                            var objRelatedSendMail = new ObjSendMail()
                            {
                                FileName = "TimeOffRelatedUserTemplate.html",
                                Mail_To = new List<string>() { relatedUser.Email! },
                                Title = "[Nghỉ Phép] Đơn xin nghỉ phép của " + leaveApplication.User.FullName + " ngày " + dateDate,
                                Mail_cc = new List<string>(),
                                JsonObject = JsonConvert.SerializeObject(relatedLeaveApplicationSendMail)
                            };

                            await _sendMailDynamicTemplateService.SendMailAsync(objRelatedSendMail);
                        }
                    }
                }
                #endregion

                #region Gửi mail cập nhật status trên microsoft team cho người đăng ký
                // Note: gửi ngay lập tức nếu là ngày bắt đầu nghỉ cùng ngày với ngày gửi đơn hoặc ngày nghỉ sau ngày gửi đơn 1d
                var registerDate = leaveApplication.RegisterDate.Date;

                // Nếu submit ngày phép trong quá khứ thì không gửi mail 
                var isLeaveInPast = registerDate > leaveApplication.FromDate.Date;
                var hasWorkDayInRange = DateTimeHelper.HasWorkingDayInRange(registerDate, leaveApplication.FromDate.Date);
                if (!isLeaveInPast && !hasWorkDayInRange)
                {
                    var emailModel = new UpdateStatusSendMailModel()
                    {
                        DateData = dateDate,
                        Type = "Nghỉ phép",
                        Register = leaveApplication.User.FullName!,
                        Status = "Chờ duyệt"
                    };
                    objSendMail = new ObjSendMail()
                    {
                        FileName = "UpdateStatusOnMicrosoftTeamTemplate.html",
                        Mail_To = new List<string>() { leaveApplication.User.Email! },
                        Title = $"[Nghỉ Phép] Yêu cầu cập nhật trạng thái - {dateDate}",
                        Mail_cc = new List<string>(),
                        JsonObject = JsonConvert.SerializeObject(emailModel)
                    };
                    await _sendMailDynamicTemplateService.SendMailAsync(objSendMail);
                }
                #endregion
            }
            else
            {
                LeaveApplicationReviewSendMail leaveApplicationReviewSendMail = new LeaveApplicationReviewSendMail()
                {
                    DateData = dateDate,
                    IsAccept = leaveApplication.ReviewStatus == (int)EReviewStatus.Reviewed,
                    LeaveApplicationTypeName = leaveApplication.LeaveApplicationType.Name + (leaveApplication.PeriodType != (int)EPeriodType.AllDay ? (leaveApplication.PeriodType == (int)EPeriodType.FirstHalf ? "Nghỉ buổi sáng" : "Nghỉ buổi chiều") : ""),
                    Register = leaveApplication.User.FullName!,
                    RegisterLink = _frontEndDomain + Constant.RegistLeaveApplicationPath,
                    ReasonReject = leaveApplication.ReviewNote,
                    Reviewer = leaveApplication.ReviewUser.FullName!
                };
                ObjSendMail objSendMail = new ObjSendMail()
                {
                    FileName = "TimeOffReviewTemplate.html",
                    Mail_To = new List<string>() { leaveApplication.User.Email! },
                    Title = "[Nghỉ Phép] Duyệt đơn xin nghỉ phép của " + leaveApplication.User.FullName + " ngày " + dateDate,
                    Mail_cc = new List<string>(),
                    JsonObject = JsonConvert.SerializeObject(leaveApplicationReviewSendMail)
                };
                await _sendMailDynamicTemplateService.SendMailAsync(objSendMail);
            }
        }

        public async Task SendNotificationAsync(LeaveApplicationNotificationModel leaveApplication)
        {
            string dateString = (leaveApplication.FromDate.Date == leaveApplication.ToDate.Date) ? $"ngày {leaveApplication.FromDate.Date.ToString("dd/MM/yyyy")}" : $"từ ngày {leaveApplication.FromDate.Date.ToString("dd/MM/yyyy")} đến ngày {leaveApplication.ToDate.Date.ToString("dd/MM/yyyy")}";

            Dictionary<string, string> data = new Dictionary<string, string>();
            data.Add("Event", "Leave");
            data.Add("Id", leaveApplication.Id.ToString());

            if (leaveApplication.ReviewStatus == (int)EReviewStatus.Pending)
            {
                string title = "Yêu cầu duyệt đơn nghỉ phép";
                string body = $"{leaveApplication.UserFullName} xin nghỉ phép {dateString}. Lý do: {leaveApplication.LeaveApplicationNote}";

                data.Add("EventType", "Request");

                Notification notification = new Notification
                {
                    Title = title,
                    Body = body
                };

                var reviewerUserDevices = await _unitOfWork.DeviceRepository.GetByUserIdAsync(leaveApplication.ReviewUserId);

                if (reviewerUserDevices.Any())
                {
                    if (reviewerUserDevices.Count > 1)
                    {
                        List<string> registrationTokens = reviewerUserDevices.Select(x => x.RegistrationToken!).ToList();
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
            }
            else
            {
                string title = "Duyệt đơn nghỉ phép";
                string body = $"{(leaveApplication.ReviewStatus == (int)EReviewStatus.Reviewed ? "Đồng ý" : "Từ chối")} đơn nghỉ phép {dateString}.{(leaveApplication.ReviewStatus == (int)EReviewStatus.Reviewed ? "" : $"Lý do: {leaveApplication.ReviewNote}")}";

                data.Add("EventType", "Confirmed");

                Notification notification = new Notification
                {
                    Title = title,
                    Body = body
                };

                var userDevices = await _unitOfWork.DeviceRepository.GetByUserIdAsync(leaveApplication.UserId);
                if (userDevices.Any())
                {
                    if (userDevices.Count > 1)
                    {
                        List<string> registrationTokens = userDevices.Select(x => x.RegistrationToken!).ToList();
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
        }

        public async Task<CombineResponseModel<LeaveApplicationNotificationModel>> PrepareUpdateAsync(int leaveApplicationId, LeaveApplicationRequest request, UserDtoModel register)
        {
            var res = new CombineResponseModel<LeaveApplicationNotificationModel>();

            if (request.FromDate > request.ToDate)
            {
                res.ErrorMessage = "Ngày bắt đầu không thể nhỏ hơn ngày kết thúc";
                return res;
            }

            var error = string.Empty;
            bool isValidToSubmit = CommonHelper.ValidDateToSubmitApplication(request.FromDate, EApplicationMessageType.LeaveApplication, out error);
            if (!isValidToSubmit)
            {
                res.ErrorMessage = error;
                return res;
            }

            var leaveApplication = await _unitOfWork.LeaveApplicationRepository.GetByIdAsync(leaveApplicationId);
            if (leaveApplication == null)
            {
                res.ErrorMessage = "Đơn xin nghỉ phép không tồn tại";
                return res;
            }

            if (leaveApplication.ReviewStatus != (int)EReviewStatus.Pending)
            {
                res.ErrorMessage = "Đơn xin nghỉ phép đã được duyệt";
                return res;
            }

            if (leaveApplication.UserId != register.Id)
            {
                res.ErrorMessage = "Không thể sửa đơn nghỉ phép của người khác";
                return res;
            }

            var leaveApplicationType = await _unitOfWork.LeaveApplicationTypeRepository.GetByIdAsync(request.LeaveApplicationTypeId);
            if (leaveApplicationType == null)
            {
                res.ErrorMessage = "Loại ngày nghỉ không tồn tại";
                return res;
            }

            var leaveRegister = await _unitOfWork.UserInternalRepository.GetByIdAsync(leaveApplication.UserId);
            if (leaveRegister == null)
            {
                res.ErrorMessage = "Người đăng ký không tồn tại";
                return res;
            }

            var leaveApplicationByDate = await _unitOfWork.LeaveApplicationRepository.GetByUserIdAndDateAsync(register.Id, request.FromDate.Date, request.ToDate.Date);
            if (leaveApplicationByDate != null && leaveApplicationByDate.Where(x => x.Id != leaveApplicationId && x.ReviewStatus != (int)EReviewStatus.Rejected
                                                                                    && (x.PeriodType == (int)request.PeriodType || x.PeriodType != (int)EPeriodType.AllDay 
                                                                                    && request.PeriodType == (int)EPeriodType.AllDay)).Any())
            {
                res.ErrorMessage = "Trùng ngày nghỉ phép";
                return res;
            }

            if (!_userInternalService.IsOfficialStaff(leaveRegister.GroupUser?.Name) && leaveApplicationType.IsSubTractCumulation)
            {
                res.ErrorMessage = "Loại ngày nghỉ không phù hợp";
                return res;
            }

            var reviewer = await _unitOfWork.UserInternalRepository.GetByIdAsync(request.ReviewUserId);
            if (reviewer == null)
            {
                res.ErrorMessage = "Người duyệt không tồn tại";
                return res;
            }

            if (request.RelatedUserIds.Any())
            {
                foreach (var relatedUserId in request.RelatedUserIds)
                {
                    var relatedUser = await _unitOfWork.UserInternalRepository.GetByIdAsync(relatedUserId);
                    if (relatedUser == null)
                    {
                        res.ErrorMessage = "Người liên quan không tồn tại";
                        return res;
                    }
                }
            }

            if (request.HandoverUserId.HasValue)
            {
                var handoverUser = await _unitOfWork.UserInternalRepository.GetByIdAsync(request.HandoverUserId.Value);
                if (handoverUser == null)
                {
                    res.ErrorMessage = "Người bàn giao không tồn tại";
                    return res;
                }
            }

            var oldLeaveApplicationType = await _unitOfWork.LeaveApplicationTypeRepository.GetByIdAsync(leaveApplication.LeaveApplicationTypeId);
            if (oldLeaveApplicationType == null)
            {
                res.ErrorMessage = "Loại ngày nghỉ đăng ký trước đó không tồn tại";
                return res;
            }

            var holidays = await _unitOfWork.HolidayRepository.GetAllAsync();
            var holidayHelpers = holidays.Select(h => new HolidayHelper
            {
                Date = h.HolidayDate,
                IsHolidayByYear = h.IsHolidayByYear,
            }).ToList();

            var errorMessage = DateTimeHelper.ValidateDateRange(request.FromDate, request.ToDate, holidayHelpers);
            if (!string.IsNullOrEmpty(errorMessage))
            {
                res.ErrorMessage = errorMessage;
                return res;
            }

            decimal dayOff = await CalculateDayOffAsync(request.PeriodType, request.FromDate, request.ToDate);
            decimal diff = dayOff - leaveApplication.NumberDayOff;
            decimal borrowedDayOff = 0;
            decimal numberDayOffLastYear = 0;

            // không phải loại nghỉ ốm
            if (leaveApplicationType.IsSubTractCumulation && leaveApplicationType.Name != Constant.SICK_LEAVE_APPLICATION_TYPE)
            {
                var pendings = await _unitOfWork.LeaveApplicationRepository.GetPendingByUserIdAsync(register.Id);
                pendings = pendings.Where(o => o.Id != leaveApplicationId).ToList();
                var pendingDayOff = pendings.Sum(x => x.NumberDayOff);

                if (CanUseOldYearRemainDayOff(request.FromDate, dayOff, pendingDayOff, leaveRegister, out error))
                {
                    if (leaveRegister.RemainDayOffLastYear > 0)
                    {
                        // 434: Tính số ngày còn lại của năm ngoái khi đã trừ đi số ngày phép quy đổi đang chờ duyệt
                        var now = DateTime.UtcNow.UTCToIct();
                        var exchangeDayOff = await _unitOfWork.ExchangeDayOffRepository.GetPendingByUserIdAndYearAsync(leaveRegister.Id, now.Year);

                        var allowDayOffLastYear = leaveRegister.RemainDayOffLastYear - pendings.Sum(o => o.NumberDayOffLastYear) - (exchangeDayOff?.DayOffExchange ?? 0);
                        numberDayOffLastYear = allowDayOffLastYear > dayOff ? dayOff : allowDayOffLastYear;
                    }
                }

                if (!string.IsNullOrEmpty(error))
                {
                    res.ErrorMessage = error;
                    return res;
                }


                var pendingLeaveDay = pendings.Sum(o => o.NumberDayOff - o.BorrowedDayOff - o.NumberDayOffLastYear);
                var pendingBorrowedDay = pendings.Sum(o => o.BorrowedDayOff);

                var borrowedDayOffAllow = leaveRegister.BorrowedDayOff - leaveRegister.UsedBorrowedDayOff > 0 ? leaveRegister.BorrowedDayOff - leaveRegister.UsedBorrowedDayOff : 0;

                if (dayOff > leaveRegister.YearOffDay + numberDayOffLastYear + borrowedDayOffAllow - pendingLeaveDay - pendingBorrowedDay)
                {
                    res.ErrorMessage = "Số ngày nghỉ không thế lớn hơn số ngày cho phép";
                    return res;
                }

                // Tính số ngày phép sử dụng phép năm nay
                var dayOffCurrentYear = dayOff - numberDayOffLastYear;
                if (dayOffCurrentYear > leaveRegister.YearOffDay - pendingLeaveDay)
                {
                    // Số ngày được phép ứng còn lại
                    var borrowedDayOffAllowRemaining = leaveRegister.BorrowedDayOff - leaveRegister.UsedBorrowedDayOff - pendingBorrowedDay;

                    // Tính số ngày nghỉ đã trừ phép năm
                    var dayOffWithOutYearOffDay = dayOffCurrentYear - (leaveRegister.YearOffDay - pendingLeaveDay);

                    borrowedDayOff = borrowedDayOffAllowRemaining > dayOffWithOutYearOffDay ? dayOffWithOutYearOffDay : borrowedDayOffAllowRemaining;
                }
            }

            // loại mới là nghỉ ốm
            if (leaveApplicationType.Name == Constant.SICK_LEAVE_APPLICATION_TYPE)
            {
                var pendingSick = await _unitOfWork.LeaveApplicationRepository.GetPendingSickByUserIdAsync(register.Id);
                if (dayOff + pendingSick.Where(x => x.Id != leaveApplicationId).Sum(x => x.NumberDayOff) > leaveRegister.SickDayOff)
                {
                    res.ErrorMessage = "Số ngày nghỉ ốm không thể lớn hơn số ngày còn lại";
                    return res;
                }
            }

            leaveApplication.UpdatedDate = DateTime.UtcNow.UTCToIct();
            leaveApplication.NumberDayOffLastYear = numberDayOffLastYear;
            leaveApplication.FromDate = request.FromDate.Date;
            leaveApplication.ToDate = request.ToDate.Date;
            leaveApplication.LeaveApplicationNote = request.LeaveApplicationNote;
            leaveApplication.LeaveApplicationTypeId = leaveApplicationType.Id;
            leaveApplication.ReviewUserId = request.ReviewUserId;
            leaveApplication.NumberDayOff = dayOff;
            leaveApplication.RelatedUserId = request.RelatedUserId;
            leaveApplication.PeriodType = (int)request.PeriodType;
            leaveApplication.RelatedUserIds = request.RelatedUserIds.JoinComma();
            leaveApplication.HandoverUserId = request.HandoverUserId;
            leaveApplication.IsAlertCustomer = request.IsAlertCustomer ?? false;
            leaveApplication.BorrowedDayOff = borrowedDayOff;

            await _unitOfWork.LeaveApplicationRepository.UpdateAsync(leaveApplication);
            await _unitOfWork.SaveChangesAsync();

            var response = new LeaveApplicationNotificationModel
            {
                Id = leaveApplication.Id,
                FromDate = leaveApplication.FromDate,
                ToDate = leaveApplication.ToDate,
                ReviewStatus = leaveApplication.ReviewStatus,
                LeaveApplicationNote = leaveApplication.LeaveApplicationNote,
                ReviewNote = leaveApplication.ReviewNote,
                ReviewUserId = leaveApplication.ReviewUserId,
                UserId = leaveApplication.UserId,
                UserFullName = leaveApplication.User.FullName!
            };

            res.Status = true;
            res.Data = response;
            return res;
        }

        public async Task<CombineResponseModel<LeaveApplication>> PrepareReviewAsync(int leaveApplicationId, ReviewModel request, UserDtoModel reviewer)
        {
            var res = new CombineResponseModel<LeaveApplication>();

            if (request.ReviewStatus != EReviewStatus.Rejected && request.ReviewStatus != EReviewStatus.Reviewed)
            {
                res.ErrorMessage = "Trạng thái duyệt không hợp lệ";
                return res;
            }

            var leaveApplication = await _unitOfWork.LeaveApplicationRepository.GetByIdAsync(leaveApplicationId);
            if (leaveApplication == null)
            {
                res.ErrorMessage = "Đơn xin nghỉ phép không tồn tại";
                return res;
            }

            var leaveApplicationType = await _unitOfWork.LeaveApplicationTypeRepository.GetByIdAsync(leaveApplication.LeaveApplicationTypeId);
            if (leaveApplicationType == null)
            {
                res.ErrorMessage = "Loại ngày nghỉ không tồn tại";
                return res;
            }

            if (leaveApplication.ReviewStatus != (int)EReviewStatus.Pending)
            {
                res.ErrorMessage = "Đơn xin nghỉ phép đã được duyệt";
                return res;
            }

            if (reviewer.Id != leaveApplication.ReviewUserId)
            {
                res.ErrorMessage = "Người duyệt không hợp lệ";
                return res;
            }

            var register = await _unitOfWork.UserInternalRepository.GetByIdAsync(leaveApplication.UserId);
            if (register == null)
            {
                res.ErrorMessage = "Người đăng ký không tồn tại";
                return res;
            }

            decimal dayOff = leaveApplication.NumberDayOff;
            if (leaveApplicationType.IsSubTractCumulation && leaveApplicationType.Name != Constant.SICK_LEAVE_APPLICATION_TYPE)
            {
                var pendings = await _unitOfWork.LeaveApplicationRepository.GetPendingByUserIdAsync(register.Id);
                var pendingDayOff = pendings.Where(x => x.Id != leaveApplicationId).Sum(x => x.NumberDayOff);

                var error = string.Empty;
                CanUseOldYearRemainDayOff(leaveApplication.FromDate, dayOff, pendingDayOff, register, out error);
                if (!string.IsNullOrEmpty(error))
                {
                    res.ErrorMessage = error;
                    return res;
                }
            }

            if (leaveApplicationType.Name == Constant.SICK_LEAVE_APPLICATION_TYPE)
            {
                var pendingSick = await _unitOfWork.LeaveApplicationRepository.GetPendingSickByUserIdAsync(register.Id);
                if (dayOff + pendingSick.Where(x => x.Id != leaveApplicationId).Sum(x => x.NumberDayOff) > register.SickDayOff)
                {
                    res.ErrorMessage = "Số ngày nghỉ không thể lớn hơn số ngày còn lại";
                    return res;
                }
            }

            leaveApplication.ReviewStatus = (int)request.ReviewStatus;
            leaveApplication.UpdatedDate = DateTime.UtcNow.UTCToIct();
            leaveApplication.ReviewNote = request.ReviewNote;
            leaveApplication.ReviewDate = DateTime.UtcNow.UTCToIct();
            await _unitOfWork.LeaveApplicationRepository.UpdateAsync(leaveApplication);
            await _unitOfWork.SaveChangesAsync();

            res.Status = true;
            res.Data = leaveApplication;
            return res;
        }

        public async Task<List<LeaveApplicationPagingMobileModel>> GetAllWithPagingMobileAsync(LeaveApplicationSearchMobileModel searchModel, int userId)
        {
            var recordsRaw = await _unitOfWork.LeaveApplicationRepository.GetAllWithPagingMobileAsync(searchModel, userId);
            var users = await _unitOfWork.UserInternalRepository.GetUsersAsync();
            var records = recordsRaw.Select(x => new LeaveApplicationPagingMobileModel
            {
                Id = x.Id,
                RegisterDate = x.RegisterDate,
                FromDate = x.FromDate,
                ToDate = x.ToDate,
                ReviewDate = x.ReviewDate,
                Note = x.Note,
                TypeId = x.TypeId,
                TypeName = x.TypeName,
                NumberDayOff = x.NumberDayOff,
                ReviewNote = x.ReviewNote,
                Status = x.Status,
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
                RelatedUsers = !string.IsNullOrEmpty(x.RelatedUserIds) ? x.RelatedUserIds.Split(',').Select(int.Parse).ToList().Select(o =>
                {
                    var user = users.FirstOrDefault(y => y.Id == o);
                    if(user == null)
                    {
                        return null;
                    }
                    var relateUser = new EmployeeProfile()
                    {
                        Id = user.Id,
                        Name = !string.IsNullOrEmpty(user.FullName) ? user.FullName : user.Name,
                        Avatar = user.Avatar,
                        Gender = (EGender?)user.Gender,
                        JobTitle = user.JobTitle
                    };
                    return relateUser;
                }).Where(t=>t != null).ToList() : null,
                IsAlertCustomer = x.IsAlertCustomer,
                HandoverUserId = x.HandoverUserId,
                HandoverUserName = users.FirstOrDefault(t => t.Id == x.HandoverUserId)?.FullName ?? string.Empty
            }).ToList();

            var totalRecords = records.FirstOrDefault();
            records.Remove(totalRecords);

            return records;
        }
    }
}
