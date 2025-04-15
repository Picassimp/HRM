using InternalPortal.ApplicationCore.Enums;
using InternalPortal.ApplicationCore.Interfaces;
using InternalPortal.ApplicationCore.Interfaces.Business;
using InternalPortal.ApplicationCore.Models.Pa;

namespace InternalPortal.Infrastructure.Services.Business
{
    public class PaService : IPaService
    {
        private readonly IUnitOfWork _unitOfWork;
        public PaService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        /// <summary>
        /// Lấy kỳ pa hằng năm theo UserId
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public async Task<List<MyAnnualPa>> GetMyPaAnnualAsync(int userId)
        {
            var myPaAnnualRaws = await _unitOfWork.PaRepository.GetMyAnnualAsync(userId);
            List<MyAnnualPa> listMyPaAnnual = new List<MyAnnualPa>();

            listMyPaAnnual = myPaAnnualRaws.OrderByDescending(x => x.Id).GroupBy(x => new { x.Id }).Select(x =>
            {
                var myAnnualPa = new MyAnnualPa();

                myAnnualPa.Id = x.Key.Id;
                myAnnualPa.Name = x.FirstOrDefault().Name;
                myAnnualPa.Year = x.FirstOrDefault().Year;

                bool isPublic = x.FirstOrDefault()?.IsPublic ?? false;
                bool isCompleted = x.FirstOrDefault()?.IsCompleted ?? false;

                myAnnualPa.Status = (isPublic == false && isCompleted == false)
                            ? EPaPeriodStatus.New
                            : (isPublic == true && isCompleted == false
                            ? EPaPeriodStatus.Submitted
                            : EPaPeriodStatus.Closed);

                myAnnualPa.Children = new List<MyAnnualPaResponse>();

                // Thêm tự đánh giá
                var self = new MyAnnualPaResponse();
                var myPaAnnual = x.FirstOrDefault(x => x.UserId == userId && !x.AssessUserId.HasValue);
                self.Id = myPaAnnual?.UserId;
                self.PaId = myAnnualPa.Id;
                self.Name = "Tự đánh giá";
                self.Completed = myPaAnnual?.Status == EPaHistoryStatus.Completed ? true : false;
                self.Children = new List<MyAnnualPaAssess>();

                if (myPaAnnual != null)
                {
                    myAnnualPa.Children.Add(self);
                }

                // Thêm đánh giá cho đồng nghiệp
                var assess = new MyAnnualPaResponse();
                var assessPas = x.Where(x => x.AssessUserId.HasValue && x.AssessUserId == userId).ToList();
                assess.PaId = myAnnualPa.Id;
                assess.Name = "Đánh giá cho đồng nghiệp";
                assess.Children = assessPas.Select(x => new MyAnnualPaAssess
                {
                    Id = x.UserId,
                    Name = x.FullName,
                    PaId = myAnnualPa.Id,
                    HasOneToOne = !string.IsNullOrEmpty(x.OneToOneNote),
                    Completed = x.Status == EPaHistoryStatus.Completed ? true : false
                }).ToList();

                var annualHasCompletedCount = assess.Children.Where(o => o.Completed).Count();
                assess.Completed = assess.Children.Count == annualHasCompletedCount;

                if (assessPas != null && assessPas.Any())
                {
                    myAnnualPa.Children.Add(assess);
                }

                //Check result
                myAnnualPa.HasResult = (self != null && self.Completed)
                                        || (x.Where(o => o.AssessUserId.HasValue
                                        && o.AssessUserId != userId
                                        && o.Status == EPaHistoryStatus.Completed).Count() > 0);

                //Check Completed
                if (myPaAnnual == null && assess.Children != null && assess.Children.Any())
                {
                    myAnnualPa.Completed = assess.Completed;
                }

                if (myPaAnnual != null && assess.Children != null && assess.Children.Any())
                {
                    myAnnualPa.Completed = (self.Completed == true && assess.Completed == true) ? true : false;
                }

                if (myPaAnnual != null && assessPas.Any() == false)
                {
                    myAnnualPa.Completed = self.Completed;
                }
                return myAnnualPa;
            }).ToList();
            return listMyPaAnnual;
        }
        /// <summary>
        /// Lấy kỳ tái ký theo UserId
        /// </summary>
        /// <param name="userId"></param>
        public async Task<List<MyManualPaGroupYear>> GetMyPaManualAsync(int userId)
        {
            var myPaManualRaws = await _unitOfWork.PaRepository.GetMyManualAsync(userId);
            List<MyManualPaGroupYear> listMyPaManual = new List<MyManualPaGroupYear>();
            listMyPaManual = myPaManualRaws.OrderByDescending(x => x.Year).GroupBy(x => new { x.Year }).Select(o =>
            {
                var myManualPaGroupYear = new MyManualPaGroupYear();
                myManualPaGroupYear.Name = Convert.ToString(o.Key.Year);
                myManualPaGroupYear.Value = o.Key.Year;

                myManualPaGroupYear.Children = o.OrderByDescending(x => x.Month).GroupBy(x => new { x.Month }).Select(o =>
                {
                    var manualPaGroupMonthYear = new MyManualPaGroupMonthYear
                    {
                        Name = "Tháng " + Convert.ToString(o.Key.Month),
                        Value = o.Key.Month.Value
                    };
                    manualPaGroupMonthYear.Children = o.OrderByDescending(x => x.Id).GroupBy(x => new { x.Id }).Select(o =>
                    {
                        var myManualPa = new MyManualPa();
                        myManualPa.Id = o.FirstOrDefault().Id;
                        myManualPa.Name = o.FirstOrDefault().Name;

                        bool isPublic = o.FirstOrDefault()?.IsPublic ?? false;
                        bool isCompleted = o.FirstOrDefault()?.IsCompleted ?? false;

                        myManualPa.Status = (isPublic == false && isCompleted == false)
                                    ? EPaPeriodStatus.New
                                    : (isPublic == true && isCompleted == false
                                    ? EPaPeriodStatus.Submitted
                                    : EPaPeriodStatus.Closed);

                        myManualPa.Children = new List<MyManualPaResponse>();

                        // Thêm tự đánh giá
                        var self = new MyManualPaResponse();
                        var myPaManual = o.FirstOrDefault(x => x.UserId == userId && !x.AssessUserId.HasValue);
                        self.Id = myPaManual?.UserId;
                        self.Name = "Tự đánh giá";
                        self.PaId = myManualPa.Id;
                        self.Completed = myPaManual?.Status == EPaHistoryStatus.Completed ? true : false;
                        self.Children = new List<MyManualPaAssess>();

                        if (myPaManual != null)
                        {
                            myManualPa.Children.Add(self);
                        }

                        // Thêm đánh giá cho đồng nghiệp
                        var assess = new MyManualPaResponse();
                        var assessPas = o.Where(x => x.AssessUserId.HasValue && x.AssessUserId == userId).ToList();
                        assess.Name = "Đánh giá cho đồng nghiệp";
                        assess.PaId = myManualPa.Id;
                        assess.Children = assessPas.Select(x => new MyManualPaAssess
                        {
                            PaId = myManualPa.Id,
                            Id = x.UserId,
                            Name = x.FullName,
                            HasOneToOne = !string.IsNullOrEmpty(x.OneToOneNote),
                            Completed = x.Status == EPaHistoryStatus.Completed ? true : false
                        }).ToList();
                        var annualHasCompletedCount = assess.Children.Where(o => o.Completed).Count();
                        assess.Completed = assess.Children.Count == annualHasCompletedCount;

                        if (assessPas != null && assessPas.Any())
                        {
                            myManualPa.Children.Add(assess);
                        }

                        // Check result
                        myManualPa.HasResult = (self != null && self.Completed)
                        || (o.Where(o => o.AssessUserId.HasValue
                        && o.AssessUserId != userId
                        && o.Status == EPaHistoryStatus.Completed).Count() > 0);

                        //Check completed
                        if (myPaManual == null && assess.Children != null && assess.Children.Any())
                        {
                            myManualPa.Completed = assess.Completed;
                        }

                        if (myPaManual != null && assess.Children != null && assess.Children.Any())
                        {
                            myManualPa.Completed = (self.Completed == true && assess.Completed == true) ? true : false;
                        }

                        if (myPaManual != null && assessPas.Any() == false)
                        {
                            myManualPa.Completed = self.Completed;
                        }
                        return myManualPa;
                    }).ToList();
                    return manualPaGroupMonthYear;
                }).ToList();
                return myManualPaGroupYear;
            }).ToList();
            return listMyPaManual;
        }
    }
}
