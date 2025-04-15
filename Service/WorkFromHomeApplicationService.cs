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
using InternalPortal.ApplicationCore.Models.PagingModel;
using InternalPortal.ApplicationCore.Models.User;
using InternalPortal.ApplicationCore.Models.UserInternal;
using InternalPortal.ApplicationCore.Models.WorkFromHomeApplicationModel;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using static InternalPortal.ApplicationCore.ValueObjects.Global;

namespace InternalPortal.Infrastructure.Services.Business
{
    public class WorkFromHomeApplicationService : IWorkFromHomeApplicationService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ISendMailDynamicTemplateService _sendMailDynamicTemplateService;
        private readonly string _frontEndDomain;
        private readonly IFirebaseMessageCloudService _firebaseMessageCloudService;

        public WorkFromHomeApplicationService(
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

        public async Task<PagingResponseModel<WorkFromHomeApplicationPagingModel>> GetAllWithPagingAsync(WorkFromHomeApplicationCriteriaModel model, int userId)
        {
            var recordsRaw = await _unitOfWork.WorkFromHomeApplicationRepository.GetAllWithPagingAsync(model, userId);

            var records = recordsRaw.Select(x => new WorkFromHomeApplicationPagingModel
            {
                Id = x.Id,
                RegisterDate = x.RegisterDate,
                FromDate = x.FromDate,
                ToDate = x.ToDate,
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
                Note = x.Note,
                RelatedUserIds = !string.IsNullOrEmpty(x.RelatedUserIds) ? x.RelatedUserIds.Split(',').Select(int.Parse).ToList() : [],
                TotalBusinessDays = x.TotalBusinessDays,
                Avatar = x.Avatar
            }).ToList();

            var totalRecords = records.FirstOrDefault();
            records.Remove(totalRecords!);

            var response = new PagingResponseModel<WorkFromHomeApplicationPagingModel>
            {
                Items = records,
                TotalRecord = totalRecords!.TotalRecord
            };

            return response;
        }

        public async Task<CombineResponseModel<WorkFromHomeApplicationNotificationModel>> PrepareCreateAsync(WorkFromHomeApllicationRequest model, UserDtoModel user)
        {
            var response = new CombineResponseModel<WorkFromHomeApplicationNotificationModel>();

            var groupUser = await _unitOfWork.GroupUserRepository.GetAllAsync();
            if (!CanSubmitWFH(user, groupUser))
            {
                response.ErrorMessage = "Bạn không phải là nhân viên có thể sử dụng chức năng này";
                return response;
            }

            bool isValidToSubmit = CommonHelper.ValidDateToSubmitApplication(model.FromDate, EApplicationMessageType.WorkFromHomeApplication, out string error);
            if (!isValidToSubmit)
            {
                response.ErrorMessage = error;
                return response;
            }

            if (model.FromDate.Date > model.ToDate.Date)
            {
                response.ErrorMessage = "Từ ngày không thể nhỏ hơn đến ngày";
                return response;
            }

            var byUserAndDate = await _unitOfWork.WorkFromHomeApplicationRepository.GetByUserIdAndDateAsync(user.Id, model.FromDate.Date, model.ToDate.Date);
            if (byUserAndDate != null && byUserAndDate.Any(x => x.ReviewStatus != (int)EReviewStatus.Rejected && (x.PeriodType == (int)model.PeriodType || x.PeriodType == (int)EPeriodType.AllDay || model.PeriodType == (int)EPeriodType.AllDay)))
            {
                response.ErrorMessage = "Trùng ngày làm việc ở nhà";
                return response;
            }

            var userReview = await _unitOfWork.UserInternalRepository.GetByIdAsync(model.ReviewUserId);
            if (userReview == null)
            {
                response.ErrorMessage = "Người duyệt không tồn tại";
                return response;
            }

            if (model.RelatedUserIds.Any())
            {
                foreach (var relatedUserId in model.RelatedUserIds)
                {
                    var relatedUser = await _unitOfWork.UserInternalRepository.GetByIdAsync(relatedUserId);
                    if (relatedUser == null)
                    {
                        response.ErrorMessage = "Người liên quan không tồn tại";
                        return response;
                    }
                }
            }

            if (user.LevelId == null)
            {
                response.ErrorMessage = "Đăng ký không thành công. Vui lòng liên hệ phòng HR để biết thêm chi tiết";
                return response;
            }

            var holidays = await _unitOfWork.HolidayRepository.GetAllAsync();
            var holidayHelpers = holidays.Select(h => new HolidayHelper
            {
                Date = h.HolidayDate,
                IsHolidayByYear = h.IsHolidayByYear,
            }).ToList();

            var errorMessage = DateTimeHelper.ValidateDateRange(model.FromDate, model.ToDate, holidayHelpers);
            if (!string.IsNullOrEmpty(errorMessage))
            {
                response.ErrorMessage = errorMessage;
                return response;
            }

            var maxDayWfhPerMonth = GetMaxWorkFromHomeDayPerMonth(user.LevelName!);

            // 202: Fr / Jr / Middle được submit 3d trong tháng 2 / 2024
            var allowLevel = new List<string>()
            {
                "Fresher", "Junior", "Middle"
            };

            if (model.FromDate.Month != model.ToDate.Month)
            {
                #region Tính số ngày dc submit trong tháng của FromDate
                var fromDateEndOfMonthDate = model.FromDate.LastDayOfMonth();
                var fromDateToEndOfMonthTotalDay = CommonHelper.CalcTotalBusinessDays(model.FromDate.Date, fromDateEndOfMonthDate, false, holidayHelpers);
                var totalWfhDayOfFromDateMonth = await _unitOfWork.WorkFromHomeApplicationRepository.GetTotalWFHDayInMonthByUserIdAndDateAsync(user.Id, model.FromDate.Date);

                // Lấy số ngày phép được submit của tháng FromDate (Chỉ cheat cho năm 2024)
                if (allowLevel.Contains(user.LevelName!) && model.FromDate.Year == 2024)
                {
                    maxDayWfhPerMonth = GetMaxDayForWFHPerMonth(model.FromDate.Month);
                }
                if (maxDayWfhPerMonth < fromDateToEndOfMonthTotalDay + totalWfhDayOfFromDateMonth)
                {
                    error = $"Số ngày WFH còn lại trong tháng {model.FromDate.Month} không đủ. Bạn đang đăng ký {fromDateToEndOfMonthTotalDay} ngày. Số ngày bạn có thể đăng ký là {maxDayWfhPerMonth - totalWfhDayOfFromDateMonth} ngày";
                    return response;
                }
                #endregion

                #region Tính số ngày dc submit trong tháng của ToDate
                var toDateStartOfMonthDate = model.ToDate.FirstDayOfMonth();
                var toDateFromStartOfMonthTotalDay = CommonHelper.CalcTotalBusinessDays(toDateStartOfMonthDate, model.ToDate.Date, false, holidayHelpers);
                var totalWfhDayOfToDateMonth = await _unitOfWork.WorkFromHomeApplicationRepository.GetTotalWFHDayInMonthByUserIdAndDateAsync(user.Id, model.ToDate.Date);

                // Lấy số ngày phép được submit của tháng ToDate (Chỉ cheat cho năm 2024)
                if (allowLevel.Contains(user.LevelName!) && model.ToDate.Year == 2024)
                {
                    maxDayWfhPerMonth = GetMaxDayForWFHPerMonth(model.ToDate.Month);
                }
                if (maxDayWfhPerMonth < toDateFromStartOfMonthTotalDay + totalWfhDayOfToDateMonth)
                {
                    error = $"Số ngày WFH còn lại trong tháng {model.ToDate.Month} không đủ. Bạn đang đăng ký {toDateFromStartOfMonthTotalDay} ngày. Số ngày bạn có thể đăng ký là {maxDayWfhPerMonth - totalWfhDayOfToDateMonth} ngày";
                    return response;
                }
                #endregion
            }
            else
            {
                #region Tính số ngày được submit
                // Lấy số ngày phép được submit của tháng ToDate (Chỉ cheat cho năm 2024)
                if (allowLevel.Contains(user.LevelName!) && model.ToDate.Year == 2024)
                {
                    maxDayWfhPerMonth = GetMaxDayForWFHPerMonth(model.ToDate.Month);
                }
                var registerTotalDay = CommonHelper.CalcTotalBusinessDays(model.FromDate.Date, model.ToDate.Date, model.PeriodType != (int)EPeriodType.AllDay, holidayHelpers);
                var totalWfhDayUsed = await _unitOfWork.WorkFromHomeApplicationRepository.GetTotalWFHDayInMonthByUserIdAndDateAsync(user.Id, model.FromDate.Date);
                if (maxDayWfhPerMonth < totalWfhDayUsed + registerTotalDay)
                {
                    response.ErrorMessage = $"Số ngày WFH còn lại trong tháng {model.FromDate.Month} không đủ. Bạn đang đăng ký {registerTotalDay} ngày. Số ngày bạn có thể đăng ký là {maxDayWfhPerMonth - totalWfhDayUsed} ngày";
                    return response;
                }
                #endregion
            }

            var application = new WorkFromHomeApplication()
            {
                CreatedDate = DateTime.UtcNow.UTCToIct(),
                FromDate = model.FromDate.Date,
                ToDate = model.ToDate.Date,
                Note = model.Note,
                RegisterDate = DateTime.UtcNow.UTCToIct(),
                ReviewStatus = (int)EReviewStatus.Pending,
                ReviewUserId = model.ReviewUserId,
                UserId = user.Id,
                PeriodType = (int)model.PeriodType,
                RelatedUserId = model.RelatedUserId,
                RelatedUserIds = model.RelatedUserIds.JoinComma()
            };

            await _unitOfWork.WorkFromHomeApplicationRepository.CreateAsync(application);
            await _unitOfWork.SaveChangesAsync();

            var wfhApplicationData = new WorkFromHomeApplicationNotificationModel()
            {
                Id = application.Id,
                FromDate = application.FromDate,
                ToDate = application.ToDate,
                ReviewStatus = (EReviewStatus)application.ReviewStatus,
                Note = application.Note,
                ReviewNote = application.ReviewNote,
                ReviewUserId = application.ReviewUserId,
                UserId = application.UserId,
                UserFullName = user.FullName,
            };

            response.Status = true;
            response.Data = wfhApplicationData;

            return response;
        }

        public async Task<CombineResponseModel<WorkFromHomeApplicationNotificationModel>> PrepareUpdateAsync(int applicationId, WorkFromHomeApllicationRequest model, UserDtoModel user)
        {
            var response = new CombineResponseModel<WorkFromHomeApplicationNotificationModel>();

            var groupUser = await _unitOfWork.GroupUserRepository.GetAllAsync();
            if (!CanSubmitWFH(user, groupUser))
            {
                response.ErrorMessage = "Bạn không phải là nhân viên có thể sử dụng chức năng này";
                return response;
            }

            bool isValidToSubmit = CommonHelper.ValidDateToSubmitApplication(model.FromDate, EApplicationMessageType.WorkFromHomeApplication, out string error);
            if (!isValidToSubmit)
            {
                response.ErrorMessage = error;
                return response;
            }

            var application = await _unitOfWork.WorkFromHomeApplicationRepository.GetByIdAsync(applicationId);
            if (application == null)
            {
                response.ErrorMessage = "Đơn WFH không tồn tại";
                return response;
            }

            if (application.ReviewStatus == (int)EReviewStatus.Rejected)
            {
                response.ErrorMessage = "Đơn WFH đã bị từ chối";
                return response;
            }

            if (application.ReviewStatus != (int)EReviewStatus.Pending)
            {
                response.ErrorMessage = "Đơn WFH đã được duyệt";
                return response;
            }

            if (application.UserId != user.Id)
            {
                response.ErrorMessage = "Không thể sửa đơn WFH của người khác";
                return response;
            }

            if (model.FromDate > model.ToDate)
            {
                response.ErrorMessage = "Từ ngày không thể nhỏ hơn đến ngày";
                return response;
            }

            var userRegist = await _unitOfWork.UserInternalRepository.GetByIdAsync(application.UserId);
            List<WorkFromHomeApplication> applicationByDate = await _unitOfWork.WorkFromHomeApplicationRepository.GetByUserIdAndDateAsync(userRegist!.Id, model.FromDate.Date, model.ToDate.Date);
            if (applicationByDate != null && applicationByDate.Where(x => x.Id != applicationId && x.ReviewStatus != (int)EReviewStatus.Rejected && (x.PeriodType == (int)model.PeriodType || x.PeriodType == (int)EPeriodType.AllDay && model.PeriodType == (int)EPeriodType.AllDay)).Any())
            {
                response.ErrorMessage = "Trùng ngày WFH";
                return response;
            }

            var userReview = await _unitOfWork.UserInternalRepository.GetByIdAsync(model.ReviewUserId);
            if (userReview == null)
            {
                response.ErrorMessage = "Người duyệt không tồn tại";
                return response;
            }

            if (model.RelatedUserIds.Any())
            {
                foreach (var relatedUserId in model.RelatedUserIds)
                {
                    var relatedUser = await _unitOfWork.UserInternalRepository.GetByIdAsync(relatedUserId);
                    if (relatedUser == null)
                    {
                        response.ErrorMessage = "Người liên quan không tồn tại";
                        return response;
                    }
                }
            }

            if (user.LevelId == null)
            {
                response.ErrorMessage = "Đăng ký không thành công. Vui lòng liên hệ phòng HR để biết thêm chi tiết";
                return response;
            }

            var holidays = await _unitOfWork.HolidayRepository.GetAllAsync();
            var holidayHelpers = holidays.Select(h => new HolidayHelper
            {
                Date = h.HolidayDate,
                IsHolidayByYear = h.IsHolidayByYear,
            }).ToList();

            var errorMessage = DateTimeHelper.ValidateDateRange(model.FromDate, model.ToDate, holidayHelpers);
            if (!string.IsNullOrEmpty(errorMessage))
            {
                response.ErrorMessage = errorMessage;
                return response;
            }

            var maxDayWfhPerMonth = GetMaxWorkFromHomeDayPerMonth(user.LevelName!);

            // 202: Fr / Jr / Middle được submit 3d trong tháng 2 / 2024
            var allowLevel = new List<string>()
            {
                "Fresher", "Junior", "Middle"
            };

            if (model.FromDate.Month != model.ToDate.Month)
            {
                #region Tính số ngày được submit trong tháng của FromDate
                // model.FromDate: 2023/10/30 - model.ToDate: 2023/11/2
                // totalWfhDayOfFromDateMonth: Tổng số ngày WFH pending, reviewed ở tháng 10 (tháng của FromDate)
                var totalWfhDayOfFromDateMonth = await _unitOfWork.WorkFromHomeApplicationRepository.GetTotalWFHDayInMonthByUserIdAndDateAsync(user.Id, model.FromDate.Date);
                // khoảng cách từ 10/30 - 10/31: 2 ngày
                var fromDateToEndOfMonthTotalDay = CommonHelper.CalcTotalBusinessDays(model.FromDate.Date, model.FromDate.LastDayOfMonth(), false, holidayHelpers);
                // tổng ngày đã đăng ký trước khi update của đơn này ở tháng của model.FromDate (để trừ đi và cộng tổng ngày sau khi update)
                double thisApplicationWfhTotalDaysOfModelFromDateMonth = 0;
                if (model.FromDate.Month != application.FromDate.Month && model.FromDate.Month != application.ToDate.Month)
                {
                    thisApplicationWfhTotalDaysOfModelFromDateMonth = 0;
                }
                else if (application.FromDate.Month == application.ToDate.Month)
                {
                    thisApplicationWfhTotalDaysOfModelFromDateMonth = CommonHelper.CalcTotalBusinessDays(application.FromDate.Date, application.ToDate.Date, application.PeriodType != (int)EPeriodType.AllDay, holidayHelpers);
                }
                else if (model.FromDate.Month == application.FromDate.Month)
                {
                    thisApplicationWfhTotalDaysOfModelFromDateMonth = CommonHelper.CalcTotalBusinessDays(application.FromDate.Date, application.FromDate.LastDayOfMonth(), false, holidayHelpers);
                }
                else if (model.FromDate.Month == application.ToDate.Month)
                {
                    thisApplicationWfhTotalDaysOfModelFromDateMonth = CommonHelper.CalcTotalBusinessDays(application.ToDate.FirstDayOfMonth(), application.ToDate.Date, false, holidayHelpers);
                }

                // Lấy số ngày phép được submit của tháng ToDate (Chỉ cheat cho năm 2024)
                if (allowLevel.Contains(user.LevelName!) && model.FromDate.Year == 2024)
                {
                    maxDayWfhPerMonth = GetMaxDayForWFHPerMonth(model.FromDate.Month);
                }

                if (maxDayWfhPerMonth < fromDateToEndOfMonthTotalDay + totalWfhDayOfFromDateMonth - thisApplicationWfhTotalDaysOfModelFromDateMonth)
                {
                    response.ErrorMessage = $"Số ngày WFH còn lại trong tháng {model.FromDate.Month} không đủ. Bạn đang đăng ký {fromDateToEndOfMonthTotalDay} ngày. Số ngày bạn có thể đăng ký là {maxDayWfhPerMonth - totalWfhDayOfFromDateMonth + thisApplicationWfhTotalDaysOfModelFromDateMonth} ngày";
                    return response;
                }
                #endregion

                #region Tính số ngày được submit trong tháng của ToDate
                // totalWfhDayOfToDateMonth: Tổng số ngày WFH pending,reviewed ở tháng 11 (tháng của ToDate)
                var totalWfhDayOfToDateMonth = await _unitOfWork.WorkFromHomeApplicationRepository.GetTotalWFHDayInMonthByUserIdAndDateAsync(user.Id, model.ToDate.Date);
                // khoảng cách từ 11/1 - 11/2: 2 ngày
                var toDateFromStartOfMonthTotalDay = CommonHelper.CalcTotalBusinessDays(model.ToDate.FirstDayOfMonth(), model.ToDate.Date, false, holidayHelpers);
                //  tổng ngày đã đăng ký trước khi update của đơn này ở tháng của model.ToDate (để trừ đi và cộng tổng ngày sau khi update)
                double thisApplicationWfhTotalDaysOfModelToDateMonth = 0;
                if (model.ToDate.Month != application.FromDate.Month && model.ToDate.Month != application.ToDate.Month)
                {
                    thisApplicationWfhTotalDaysOfModelToDateMonth = 0;
                }
                else if (application.FromDate.Month == application.ToDate.Month)
                {
                    thisApplicationWfhTotalDaysOfModelToDateMonth = CommonHelper.CalcTotalBusinessDays(application.FromDate.Date, application.ToDate.Date, application.PeriodType != (int)EPeriodType.AllDay, holidayHelpers);
                }
                else if (model.ToDate.Month == application.FromDate.Month)
                {
                    thisApplicationWfhTotalDaysOfModelToDateMonth = CommonHelper.CalcTotalBusinessDays(application.FromDate.Date, application.FromDate.LastDayOfMonth(), false, holidayHelpers);
                }
                else if (model.ToDate.Month == application.ToDate.Month)
                {
                    thisApplicationWfhTotalDaysOfModelToDateMonth = CommonHelper.CalcTotalBusinessDays(application.ToDate.FirstDayOfMonth(), application.ToDate.Date, false, holidayHelpers);
                }

                // Lấy số ngày phép được submit của tháng ToDate (Chỉ cheat cho năm 2024)
                if (allowLevel.Contains(user.LevelName!) && model.ToDate.Year == 2024)
                {
                    maxDayWfhPerMonth = GetMaxDayForWFHPerMonth(model.ToDate.Month);
                }

                if (maxDayWfhPerMonth < toDateFromStartOfMonthTotalDay + totalWfhDayOfToDateMonth - thisApplicationWfhTotalDaysOfModelToDateMonth)
                {
                    response.ErrorMessage = $"Số ngày WFH còn lại trong tháng {model.ToDate.Month} không đủ. Bạn đang đăng ký {toDateFromStartOfMonthTotalDay} ngày. Số ngày bạn có thể đăng ký là {maxDayWfhPerMonth - totalWfhDayOfToDateMonth + thisApplicationWfhTotalDaysOfModelToDateMonth} ngày";
                    return response;
                }
                #endregion
            }
            else
            {
                #region Tính số ngày được submit 
                // Lấy số ngày phép được submit của tháng ToDate (Chỉ cheat cho năm 2024)
                if (allowLevel.Contains(user.LevelName!) && model.ToDate.Year == 2024)
                {
                    maxDayWfhPerMonth = GetMaxDayForWFHPerMonth(model.ToDate.Month);
                }
                var registerTotalDay = CommonHelper.CalcTotalBusinessDays(model.FromDate.Date, model.ToDate.Date, model.PeriodType != EPeriodType.AllDay, holidayHelpers);
                var totalWfhDayUsed = await _unitOfWork.WorkFromHomeApplicationRepository.GetTotalWFHDayInMonthByUserIdAndDateAsync(user.Id, model.FromDate.Date);
                double thisApplicationWfhTotalDaysOfApplicationFromDateMonth = 0;
                if (model.FromDate.Month != application.FromDate.Month && model.FromDate.Month != application.ToDate.Month)
                {
                    thisApplicationWfhTotalDaysOfApplicationFromDateMonth = 0;
                }
                else if (application.FromDate.Month == application.ToDate.Month)
                {
                    thisApplicationWfhTotalDaysOfApplicationFromDateMonth = CommonHelper.CalcTotalBusinessDays(application.FromDate.Date, application.ToDate.Date, application.PeriodType != (int)EPeriodType.AllDay, holidayHelpers);
                }
                else if (model.FromDate.Month == application.FromDate.Month)
                {
                    thisApplicationWfhTotalDaysOfApplicationFromDateMonth = CommonHelper.CalcTotalBusinessDays(application.FromDate.Date, application.FromDate.LastDayOfMonth(), false, holidayHelpers);
                }
                else if (model.FromDate.Month == application.ToDate.Month)
                {
                    thisApplicationWfhTotalDaysOfApplicationFromDateMonth = CommonHelper.CalcTotalBusinessDays(application.ToDate.FirstDayOfMonth(), application.ToDate.Date, false, holidayHelpers);
                }
                if (maxDayWfhPerMonth < totalWfhDayUsed + registerTotalDay - thisApplicationWfhTotalDaysOfApplicationFromDateMonth)
                {
                    response.ErrorMessage = $"Số ngày WFH còn lại trong tháng {model.FromDate.Month} không đủ. Bạn đang đăng ký {registerTotalDay} ngày. Số ngày bạn có thể đăng ký là {maxDayWfhPerMonth - totalWfhDayUsed + thisApplicationWfhTotalDaysOfApplicationFromDateMonth} ngày";
                    return response;
                }
                #endregion
            }

            application.UpdatedDate = DateTime.UtcNow.UTCToIct();
            application.FromDate = model.FromDate.Date;
            application.ToDate = model.ToDate.Date;
            application.Note = model.Note;
            application.ReviewUserId = model.ReviewUserId;
            application.RelatedUserId = model.RelatedUserId;
            application.PeriodType = (int)model.PeriodType;
            application.RelatedUserIds = model.RelatedUserIds.JoinComma();

            await _unitOfWork.WorkFromHomeApplicationRepository.UpdateAsync(application);
            await _unitOfWork.SaveChangesAsync();

            var wfhApplicationData = new WorkFromHomeApplicationNotificationModel()
            {
                Id = application.Id,
                FromDate = application.FromDate,
                ToDate = application.ToDate,
                ReviewStatus = (EReviewStatus)application.ReviewStatus,
                Note = application.Note,
                ReviewNote = application.ReviewNote,
                ReviewUserId = application.ReviewUserId,
                UserId = application.UserId,
                UserFullName = user.FullName,
            };

            response.Status = true;
            response.Data = wfhApplicationData;

            return response;
        }

        public async Task SendMailAsync(int applicationId)
        {
            var application = await _unitOfWork.WorkFromHomeApplicationRepository.GetByIdAsync(applicationId);
            if (application == null)
            {
                return;
            }

            string dateString = application.FromDate.Date == application.ToDate.Date ?
                    application.FromDate.Date.ToString("dd/MM/yyyy") + " " + (application.PeriodType != (int)EPeriodType.AllDay ? 
                    (application.PeriodType == (int)EPeriodType.FirstHalf ? "(Buổi sáng)" : "(Buổi chiều)") : "") :
                    application.FromDate.Date.ToString("dd/MM/yyyy") + " - " + application.ToDate.Date.ToString("dd/MM/yyyy");

            if (application.ReviewStatus == (int)EReviewStatus.Pending)
            {
                var objSendMail = new ObjSendMail()
                {
                    FileName = "WorkFromHomeTemplate.html",
                    Mail_To = [application.ReviewUser.Email!],
                    Title = "[WFH] Yêu cầu duyệt đơn WFH của " + application.User.FullName + " ngày " + dateString,
                    Mail_cc = [],
                    JsonObject = JsonConvert.SerializeObject(new WorkFromHomeApplicationMail()
                    {
                        DateData = dateString,
                        Register = application.User.FullName!,
                        Reviewer = application.ReviewUser.FullName!,
                        ReviewLink = _frontEndDomain + Constant.ReviewWorkFromHomeApplicationPath,
                        Reason = application.Note
                    })
                };
                await _sendMailDynamicTemplateService.SendMailAsync(objSendMail);

                // Check Related user exists
                if (!string.IsNullOrEmpty(application.RelatedUserIds))
                {
                    var relatedUserIds = application.RelatedUserIds.Split(',').Select(int.Parse).ToList();
                    foreach (var relatedUserId in relatedUserIds)
                    {
                        var relatedUser = await _unitOfWork.UserInternalRepository.GetByIdAsync(relatedUserId);

                        var objRelatedSendMail = new ObjSendMail()
                        {
                            FileName = "WorkFromHomeRelatedUserTemplate.html",
                            Mail_To = [relatedUser!.Email!],
                            Title = "[WFH] Đơn WFH của " + application.User.FullName + " ngày " + dateString,
                            Mail_cc = [],
                            JsonObject = JsonConvert.SerializeObject(new WorkFromHomeApplicationMail()
                            {
                                DateData = dateString,
                                Register = application.User.FullName!,
                                Reviewer = relatedUser!.FullName!,
                                Reason = application.Note
                            })
                        };
                        await _sendMailDynamicTemplateService.SendMailAsync(objRelatedSendMail);
                    }
                }

                #region Gửi mail cập nhật status trên microsoft team cho người đăng ký
                // Note: gửi ngay lập tức nếu là ngày bắt đầu nghỉ cùng ngày với ngày gửi đơn hoặc ngày nghỉ sau ngày gửi đơn 1d
                var registerDate = application.RegisterDate.Date;

                // Nếu submit ngày wfh trong quá khứ thì không gửi mail 
                var isWfhInPast = registerDate > application.FromDate.Date;
                var hasWorkDayInRange = CommonHelper.HasWorkingDayInRange(registerDate, application.FromDate.Date);
                if (!isWfhInPast && !hasWorkDayInRange)
                {
                    var emailModel = new UpdateStatusSendMailModel()
                    {
                        DateData = dateString,
                        Type = "Làm việc tại nhà",
                        Register = application.User.FullName!,
                        Status = "Chờ duyệt"
                    };
                    objSendMail = new ObjSendMail()
                    {
                        FileName = "UpdateStatusOnMicrosoftTeamTemplate.html",
                        Mail_To = [application.User.Email!],
                        Title = $"[WFH] Yêu cầu cập nhật trạng thái - {dateString}",
                        Mail_cc = [],
                        JsonObject = JsonConvert.SerializeObject(emailModel)
                    };
                    await _sendMailDynamicTemplateService.SendMailAsync(objSendMail);
                }
                #endregion
            }
            else
            {
                var objSendMail = new ObjSendMail()
                {
                    FileName = "WorkFromHomeReviewTemplate.html",
                    Mail_To = [application.User.Email!],
                    Title = "[WFH] Duyệt đơn WFH của " + application.User.FullName + " ngày " + dateString,
                    Mail_cc = [],
                    JsonObject = JsonConvert.SerializeObject(new WorkFromHomeApplicationReviewedMail()
                    {
                        DateData = dateString,
                        IsAccept = application.ReviewStatus == (int)EReviewStatus.Reviewed,
                        Register = application.User.FullName!,
                        RegisterLink = _frontEndDomain + Constant.RegistWorkFromHomeApplicationPath,
                        ReasonReject = application.ReviewNote,
                        Reviewer = application.ReviewUser.FullName
                    })
                };
                await _sendMailDynamicTemplateService.SendMailAsync(objSendMail);
            }
        }

        public async Task SendNotificationAsync(WorkFromHomeApplicationNotificationModel notificationRequest)
        {
            string dateString = (notificationRequest.FromDate.Date == notificationRequest.ToDate.Date) ?
                $"ngày {notificationRequest.FromDate.Date:dd/MM/yyyy}" :
                $"từ ngày {notificationRequest.FromDate.Date:dd/MM/yyyy} đến ngày {notificationRequest.ToDate.Date:dd/MM/yyyy}";

            var data = new Dictionary<string, string>
            {
                { "Event", "WFH" },
                { "Id", notificationRequest.Id.ToString() }
            };

            if (notificationRequest.ReviewStatus == (int)EReviewStatus.Pending)
            {
                string title = "Yêu cầu duyệt đơn WFH";
                string body = $"{notificationRequest.UserFullName} xin WFH {dateString}. Lý do: {notificationRequest.Note}";

                data.Add("EventType", "Request");

                var notification = new Notification
                {
                    Title = title,
                    Body = body
                };

                var reviewerUserDevices = await _unitOfWork.DeviceRepository.GetByUserIdAsync(notificationRequest.ReviewUserId);

                if (reviewerUserDevices.Count > 1)
                {
                    var registrationTokens = reviewerUserDevices.Select(x => x.RegistrationToken).ToList()!;
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
                string title = "Duyệt đơn WFH";
                string body = $"{(notificationRequest.ReviewStatus == EReviewStatus.Reviewed ? "Đồng ý" : "Từ chối")} đơn WFH {dateString}. {(notificationRequest.ReviewStatus == EReviewStatus.Reviewed ? "" : $"Lý do: {notificationRequest.ReviewNote}")}";
                data.Add("EventType", "Confirmed");

                var notification = new Notification
                {
                    Title = title,
                    Body = body
                };

                var userDevices = await _unitOfWork.DeviceRepository.GetByUserIdAsync(notificationRequest.UserId);

                if (userDevices.Count > 1)
                {
                    var registrationTokens = userDevices.Select(x => x.RegistrationToken).ToList()!;
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
        public async Task<List<WorkFromHomeApplicationPagingMobileModel>> GetAllWithPagingMobileAsync(WorkFromHomeApplicationSearchMobileModel searchModel, int userId)
        {
            var recordsRaw = await _unitOfWork.WorkFromHomeApplicationRepository.GetAllWithPagingMobileAsync(searchModel, userId);
            var users = await _unitOfWork.UserInternalRepository.GetUsersAsync();
            var records = recordsRaw.Select(x => new WorkFromHomeApplicationPagingMobileModel
            {
                Id = x.Id,
                RegisterDate = x.RegisterDate,
                FromDate = x.FromDate,
                ToDate = x.ToDate,
                ReviewDate = x.ReviewDate,
                Note = x.Note,
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
                    if (user == null)
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
                }).Where(t => t != null).ToList() : null,
            }).ToList();

            var totalRecords = records.FirstOrDefault();
            records.Remove(totalRecords);

            return records;
        }
        #endregion

        #region Private Method
        private static double GetMaxWorkFromHomeDayPerMonth(string levelName)
        {
            double maxDay = 0;
            switch (levelName)
            {
                case "Fresher":
                case "Junior":
                case "Middle":
                    maxDay = 1;
                    break;

                case "Senior":
                case "Team Leader":
                    maxDay = 3;
                    break;

                case "Technical Manager":
                case "Director Manager":
                case "Manager":
                    maxDay = 5;
                    break;
            }
            return maxDay;
        }

        private static double GetMaxDayForWFHPerMonth(int month)
        {
            double maxDay = 0;
            switch (month)
            {
                case 1:
                case 3:
                case 4:
                case 5:
                case 6:
                case 7:
                case 8:
                case 9:
                case 10:
                case 11:
                case 12:
                    maxDay = 1;
                    break;
                case 2:
                    maxDay = 3;
                    break;
            }
            return maxDay;
        }

        private static bool CanSubmitWFH(UserDtoModel user, List<GroupUser> groupUser)
        {
            if (user.GroupUserId == null)
            {
                return false;
            }

            var filterGroupUser = groupUser.FirstOrDefault(x => x.Id == user.GroupUserId);
            if (filterGroupUser == null)
            {
                return false;   
            }

            if (filterGroupUser.Name == "Nhân viên thử việc" || filterGroupUser.Name == "Nhân viên học việc" || filterGroupUser.Name == "Thực tập sinh")
            {
                return false;
            }

            return true;
        }
        #endregion
    }
}
