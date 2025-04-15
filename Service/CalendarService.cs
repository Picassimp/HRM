using InternalPortal.ApplicationCore.Enums;
using InternalPortal.ApplicationCore.Interfaces;
using InternalPortal.ApplicationCore.Interfaces.Business;
using InternalPortal.ApplicationCore.Models.Calendar;
using InternalPortal.ApplicationCore.Models.CriteriaModel;
using InternalPortal.ApplicationCore.Models.LeaveApplication;
using Microsoft.Extensions.Configuration;

namespace InternalPortal.Infrastructure.Services.Business
{
    public class CalendarService : ICalendarService
    {
        private readonly ILeaveApplicationService _leaveApplicationService;
        private readonly IWorkFromHomeApplicationService _workFromHomeApplicationService;
        private readonly IOnsiteApplicationService _onsiteApplicationService;

        public CalendarService(
            ILeaveApplicationService leaveApplicationService,
            IWorkFromHomeApplicationService workFromHomeApplicationService,
            IOnsiteApplicationService onsiteApplicationService)
        {
            _leaveApplicationService = leaveApplicationService;
            _workFromHomeApplicationService = workFromHomeApplicationService;
            _onsiteApplicationService = onsiteApplicationService;
        }

        public async Task<List<object>> GetAllWithPagingAsync(CalendarRequest request, int userId)
        {
            var response = new List<object>();

            if (request.ApplicationType.HasValue)
            {
                if (request.ApplicationType.Value == (int)EApplicationMessageType.LeaveApplication)
                {
                    var leaveCalendarPagingRequest = new LeaveApplicationPagingRequest
                    {
                        Type = request.Type,
                        Status = request.Status,
                        SearchUserId = request.SearchUserId,
                        SearchReviewerId = request.SearchReviewerId,
                        FromDate = request.FromDate,
                        ToDate = request.ToDate,
                        IsNoPaging = true
                    };
                    var leaves = await _leaveApplicationService.GetAllWithPagingAsync(leaveCalendarPagingRequest, userId);

                    response.AddRange(leaves.Items.Select(o => new 
                    {
                        Id = o.Id,
                        RegisterDate = o.RegisterDate,
                        FromDate = o.FromDate,
                        ToDate = o.ToDate,
                        NumberDayOff = o.NumberDayOff,
                        LeaveApplicationNote = o.LeaveApplicationNote,
                        ReviewStatus = o.ReviewStatus,
                        ReviewNote = o.ReviewNote,
                        LeaveApplicationType = o.LeaveApplicationType,
                        UserId = o.UserId,
                        JobTitle = o.JobTitle,
                        UserName = o.UserName,
                        ReviewUserId = o.ReviewUserId,
                        ReviewUser = o.ReviewUser,
                        ReviewStatusName = o.ReviewStatusName,
                        ReviewDate = o.ReviewDate,
                        PeriodType = o.PeriodType,
                        PeriodTypeName = o.PeriodTypeName,
                        RelatedUserIds = o.RelatedUserIds,
                        IsAlertCustomer = o.IsAlertCustomer,
                        HandoverUserId = o.HandoverUserId,
                        HandoverUserName = o.HandoverUserName,
                        BorrowedDayOff = o.BorrowedDayOff,
                        Avatar = o.Avatar,
                        ApplicationRequestType = (int)EApplicationMessageType.LeaveApplication, // Leave = 1
                        TitleApplication = $"{o.UserName} - {o.LeaveApplicationType} - {o.PeriodTypeName}"
                    }).ToList());
                }
                else if (request.ApplicationType.Value == (int)EApplicationMessageType.OnsiteApplication)
                {
                    if (request.Type != ESearchType.RelatedUser)
                    {
                        var onsiteCalendarPagingRequest = new OnsiteApplicationCriteriaModel
                        {
                            PageIndex = null,
                            PageSize = null,
                            Status = request.Status,
                            Type = request.Type,
                            SearchUserId = request.SearchUserId,
                            SearchReviewerId = request.SearchReviewerId,
                            FromDate = request.FromDate,
                            ToDate = request.ToDate,
                            IsNoPaging = true
                        };
                        var onsites = await _onsiteApplicationService.GetAllWithPagingAsync(onsiteCalendarPagingRequest, userId);

                        response.AddRange(onsites.Items.Select(o => new
                        {
                            Id = o.Id,
                            RegisterDate = o.RegisterDate,
                            FromDate = o.FromDate,
                            ToDate = o.ToDate,
                            ReviewNote = o.ReviewNote,
                            Status = o.Status,
                            ReviewDate = o.ReviewDate,
                            UserId = o.UserId,
                            UserName = o.UserName,
                            JobTitle = o.JobTitle,
                            ReviewUserId = o.ReviewUserId,
                            ReviewUser = o.ReviewUser,
                            ProjectName = o.ProjectName ?? "",
                            PeriodType = o.PeriodType,
                            OnsiteNote = o.OnsiteNote,
                            NumberDayOnsite = o.NumberDayOnsite,
                            Location = o.Location ?? "",
                            ReviewStatusName = o.ReviewStatusName,
                            PeriodTypeName = o.PeriodTypeName,
                            IsCharge = o.IsCharge,
                            FileUrls = o.FileUrls,
                            Avatar = o.Avatar,
                            TitleApplication = $"{o.UserName} - Công Tác - {o.PeriodTypeName}",
                            ApplicationRequestType = (int)EApplicationMessageType.OnsiteApplication, // Onsite = 4
                        }).ToList());
                    }
                }
                else if (request.ApplicationType.Value == (int)EApplicationMessageType.WorkFromHomeApplication)
                {
                    var wfhCalendarPagingRequest = new WorkFromHomeApplicationCriteriaModel
                    {
                        PageIndex = null,
                        PageSize = null,
                        Type = request.Type,
                        Status = request.Status,
                        SearchUserId = request.SearchUserId,
                        SearchReviewerId = request.SearchReviewerId,
                        FromDate = request.FromDate,
                        ToDate = request.ToDate,
                        IsNoPaging = true
                    };
                    var wfhs = await _workFromHomeApplicationService.GetAllWithPagingAsync(wfhCalendarPagingRequest, userId);
                    response.AddRange(wfhs.Items.Select(o => new
                    {
                        Id = o.Id,
                        RegisterDate = o.RegisterDate,
                        FromDate = o.FromDate,
                        ToDate = o.ToDate,
                        TotalBusinessDays = o.TotalBusinessDays,
                        Note = o.Note,
                        ReviewStatus = o.ReviewStatus,
                        ReviewNote = o.ReviewNote,
                        UserId = o.UserId,
                        JobTitle = o.JobTitle,
                        UserName = o.UserName,
                        ReviewUserId = o.ReviewUserId,
                        ReviewUser = o.ReviewUser,
                        ReviewStatusName = o.ReviewStatusName,
                        ReviewDate = o.ReviewDate,
                        PeriodType = o.PeriodType,
                        PeriodTypeName = o.PeriodTypeName,
                        RelatedUserIds = o.RelatedUserIds,
                        Avatar = o.Avatar,
                        TitleApplication = $"{o.UserName} - Làm Ở Nhà - {o.PeriodTypeName}",
                        ApplicationRequestType = (int)EApplicationMessageType.WorkFromHomeApplication, // WFH = 3
                    }).ToList());
                }
            }
            else
            {
                #region Leave
                var leaveCalendarPagingRequest = new LeaveApplicationPagingRequest
                {
                    Type = request.Type,
                    Status = request.Status,
                    SearchUserId = request.SearchUserId,
                    SearchReviewerId = request.SearchReviewerId,
                    FromDate = request.FromDate,
                    ToDate = request.ToDate,
                    IsNoPaging = true
                };
                var leaves = await _leaveApplicationService.GetAllWithPagingAsync(leaveCalendarPagingRequest, userId);

                response.AddRange(leaves.Items.Select(o => new
                {
                    Id = o.Id,
                    RegisterDate = o.RegisterDate,
                    FromDate = o.FromDate,
                    ToDate = o.ToDate,
                    NumberDayOff = o.NumberDayOff,
                    LeaveApplicationNote = o.LeaveApplicationNote,
                    ReviewStatus = o.ReviewStatus,
                    ReviewNote = o.ReviewNote,
                    LeaveApplicationType = o.LeaveApplicationType,
                    UserId = o.UserId,
                    JobTitle = o.JobTitle,
                    UserName = o.UserName,
                    ReviewUserId = o.ReviewUserId,
                    ReviewUser = o.ReviewUser,
                    ReviewStatusName = o.ReviewStatusName,
                    ReviewDate = o.ReviewDate,
                    PeriodType = o.PeriodType,
                    PeriodTypeName = o.PeriodTypeName,
                    RelatedUserIds = o.RelatedUserIds,
                    IsAlertCustomer = o.IsAlertCustomer,
                    HandoverUserId = o.HandoverUserId,
                    HandoverUserName = o.HandoverUserName,
                    BorrowedDayOff = o.BorrowedDayOff,
                    Avatar = o.Avatar,
                    ApplicationRequestType = (int)EApplicationMessageType.LeaveApplication, // Leave = 1
                    TitleApplication = $"{o.UserName} - {o.LeaveApplicationType} - {o.PeriodTypeName}"
                }).ToList());
                #endregion

                #region Onsite
                if (request.Type != ESearchType.RelatedUser)
                {
                    var onsiteCalendarPagingRequest = new OnsiteApplicationCriteriaModel
                    {
                        Status = request.Status,
                        Type = request.Type,
                        SearchUserId = request.SearchUserId,
                        SearchReviewerId = request.SearchReviewerId,
                        FromDate = request.FromDate,
                        ToDate = request.ToDate,
                        IsNoPaging = true
                    };
                    var onsites = await _onsiteApplicationService.GetAllWithPagingAsync(onsiteCalendarPagingRequest, userId);

                    response.AddRange(onsites.Items.Select(o => new
                {
                    Id = o.Id,
                    RegisterDate = o.RegisterDate,
                    FromDate = o.FromDate,
                    ToDate = o.ToDate,
                    ReviewNote = o.ReviewNote,
                    Status = o.Status,
                    ReviewDate = o.ReviewDate,
                    UserId = o.UserId,
                    UserName = o.UserName,
                    JobTitle = o.JobTitle,
                    ReviewUserId = o.ReviewUserId,
                    ReviewUser = o.ReviewUser,
                    ProjectName = o.ProjectName ?? "",
                    PeriodType = o.PeriodType,
                    OnsiteNote = o.OnsiteNote,
                    NumberDayOnsite = o.NumberDayOnsite,
                    Location = o.Location ?? "",
                    ReviewStatusName = o.ReviewStatusName,
                    PeriodTypeName = o.PeriodTypeName,
                    IsCharge = o.IsCharge,
                    FileUrls = o.FileUrls,
                    Avatar = o.Avatar,
                    TitleApplication = $"{o.UserName} - Công Tác - {o.PeriodTypeName}",
                    ApplicationRequestType = (int)EApplicationMessageType.OnsiteApplication, // Onsite = 4
                }).ToList());
                }
                #endregion

                #region Wfh
                var wfhCalendarPagingRequest = new WorkFromHomeApplicationCriteriaModel
                {
                    Type = request.Type,
                    Status = request.Status,
                    SearchUserId = request.SearchUserId,
                    SearchReviewerId = request.SearchReviewerId,
                    FromDate = request.FromDate,
                    ToDate = request.ToDate,
                    IsNoPaging = true
                };
                var wfhs = await _workFromHomeApplicationService.GetAllWithPagingAsync(wfhCalendarPagingRequest, userId);
                response.AddRange(wfhs.Items.Select(o => new
                {
                    Id = o.Id,
                    RegisterDate = o.RegisterDate,
                    FromDate = o.FromDate,
                    ToDate = o.ToDate,
                    TotalBusinessDays = o.TotalBusinessDays,
                    Note = o.Note,
                    ReviewStatus = o.ReviewStatus,
                    ReviewNote = o.ReviewNote,
                    UserId = o.UserId,
                    JobTitle = o.JobTitle,
                    UserName = o.UserName,
                    ReviewUserId = o.ReviewUserId,
                    ReviewUser = o.ReviewUser,
                    ReviewStatusName = o.ReviewStatusName,
                    ReviewDate = o.ReviewDate,
                    PeriodType = o.PeriodType,
                    PeriodTypeName = o.PeriodTypeName,
                    RelatedUserIds = o.RelatedUserIds,
                    Avatar = o.Avatar,
                    TitleApplication = $"{o.UserName} - Làm Ở Nhà - {o.PeriodTypeName}",
                    ApplicationRequestType = (int)EApplicationMessageType.WorkFromHomeApplication, // WFH = 3
                }).ToList());
                #endregion
            }

            return response;
        }
    }
}
