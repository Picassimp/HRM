using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Enums;
using InternalPortal.ApplicationCore.Helpers;
using InternalPortal.ApplicationCore.Interfaces;
using InternalPortal.ApplicationCore.Interfaces.Business;
using InternalPortal.ApplicationCore.Models.Pa;
using InternalPortal.ApplicationCore.Models.PaDetail;
using Newtonsoft.Json;

namespace InternalPortal.Infrastructure.Services.Business
{
    public class PaDetailService : IPaDetailService
    {
        private readonly IUnitOfWork _unitOfWork;

        public PaDetailService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }
        #region private methods
        /// <summary>
        /// Tính điểm đánh giá của một form đánh giá.
        /// </summary>
        /// <param name="pa"></param>
        /// <param name="evaluatee"></param>
        /// <param name="appraiser"></param>
        /// <returns></returns>
        private async Task<double> GetPaMarkAsync(int paId, int paHistoryId, bool status)
        {
            if (status)
            {
                var result = await _unitOfWork.PaDetailRepository.GetQuestionGroupMarksAsync(paId, paHistoryId);
                var questionGroupMarks = result.Where(x => x.Mark > 0).ToList();
                var paMark = Math.Round(questionGroupMarks.Sum(item => GetQuestionGroupMark(item.Percent, item.Count, item.Mark) * 10), 1, MidpointRounding.AwayFromZero);
                return paMark;
            }
            else
            {
                return 0;
            }
        }
        /// <summary>
        /// Tính điểm đánh giá tổng hợp cho người tham gia không phải là Manager.
        /// </summary>
        /// <param name="pa"></param>
        /// <param name="evaluatee"></param>
        /// <returns></returns>
        private async Task<double> GetAppraisersMarkAsync(Pa pa, PaDetail evaluatee)
        {
            var appraisers = evaluatee.PaRelatives.ToList();
            var manager = appraisers.FirstOrDefault(x => x.IsManager == true && !x.IsReference);
            var users = appraisers.Where(x => x.IsManager == false && !x.IsReference).ToList();

            var group = GetMarkConfigGroup(pa.RateConfig, users.Count + (manager == null ? 0 : 1));
            
            if (group != null)
            {
                double userMarkTotal = 0;
                foreach (var user in users)
                {
                    double userMark = await GetPaMarkAsync(pa.Id, user.PaHistory.Id, user.PaHistory.Status == (int)EPaHistoryStatus.Completed);
                    userMarkTotal += userMark;
                }
                double usersMark = Math.Round(userMarkTotal, 1, MidpointRounding.AwayFromZero);
                double managerMark = manager == null ? 0 : Math.Round(await GetPaMarkAsync(pa.Id, manager.PaHistory.Id, manager.PaHistory.Status == (int)EPaHistoryStatus.Completed), 1, MidpointRounding.AwayFromZero);

                double userPercent = group.Rate.User * 0.01;
                double managerPercent = group.Rate.Manager * 0.01;

                int countCompleted = users.Where(x => x.PaHistory.Status == (int)EPaHistoryStatus.Completed).Count();
                double userSum = countCompleted <= 0 ? 0 : Math.Round((usersMark / countCompleted) * userPercent, 2, MidpointRounding.AwayFromZero);
                double managerSum = Math.Round(managerMark * managerPercent, 2, MidpointRounding.AwayFromZero);

                double sum = userSum + managerSum;
                return Math.Round(sum, 1, MidpointRounding.AwayFromZero);
            }
            else
            {
                return 0;
            }
        }
        /// <summary>
        /// Tính điểm trung bình không tính tự đánh giá cho người tham gia là Manager.
        /// </summary>
        /// <param name="pa"></param>
        /// <param name="evaluatee"></param>
        /// <returns></returns>
        private async Task<double> GetAppraisersMarkForManagerAsync(Pa pa, PaDetail evaluatee, int levelId)
        {
            if (levelId != 4)
            {
                var appraisers = evaluatee.PaRelatives.Where(x => x.PaHistory!.Status == (int)EPaHistoryStatus.Completed && !x.IsReference).ToList();
                double appraisersMarkTotal = 0;
                foreach (var appraiser in appraisers)
                {
                    double appraisersMark = await GetPaMarkAsync(pa.Id, appraiser.PaHistory!.Id, appraiser.PaHistory.Status == (int)EPaHistoryStatus.Completed);
                    appraisersMarkTotal += appraisersMark;
                }
                double sum = Math.Round(appraisersMarkTotal, 1, MidpointRounding.AwayFromZero);
                double mark = Math.Round(sum / appraisers.Count(), 1, MidpointRounding.AwayFromZero);
                return Double.IsNaN(mark) ? 0 : mark;
            }
            else
            {
                return await GetAppraisersMarkAsync(pa, evaluatee);
            }
        }
        /// <summary>
        /// Kiểm tra người tham gia có cấp độ (leader, manager) trở lên hay không.
        /// </summary>
        /// <param name="levelId"></param>
        /// <returns></returns>
        private bool IsManagerLevel(int levelId)
        {
            //4:Team Leader
            //5:Technical Manager
            //6:Director Manager
            //7:Manager
            var managerLevels = new List<int> { 4, 5, 6, 7 };
            return managerLevels.Contains(levelId);
        }
        /// <summary>
        /// Chuẩn bị data để tính điểm
        /// </summary>
        /// <param name="pa"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        private MarkConfigurationModel GetMarkConfigGroup(string rateconfig, int count)
        {
            var model = JsonConvert.DeserializeObject<List<MarkConfigurationModel>>(rateconfig);

            if (!model.Any())
            {
                return null;
            }
            else
            {
                int id = 1;
                switch (count)
                {
                    case 1:
                        id = 0;
                        break;
                    case 2:
                        id = 1;
                        break;
                    case 3:
                        id = 2;
                        break;
                    case 4:
                        id = 3;
                        break;
                    case 5:
                        id = 4;
                        break;
                    default:
                        id = 5;
                        break;
                }

                return model.FirstOrDefault(x => x.Group == id);
            }
        }
        /// <summary>
        /// Tính điểm đánh giá của một nhóm câu hỏi trong một form đánh giá.
        /// </summary>
        /// <param name="percent"></param>
        /// <param name="count"></param>
        /// <param name="mark"></param>
        /// <returns></returns>
        private double GetQuestionGroupMark(double percent, int count, double mark)
        {
            return (mark == 0 ? 0 : Math.Round((mark * percent * 0.01) / count, 2, MidpointRounding.AwayFromZero));
        }
        #endregion

        /// <summary>
        /// User xem chi tiết thông tin đánh giá của mình.
        /// </summary>
        /// <param name="paId"></param>
        /// <param name="evaluateeId"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task<UserPaDetailResponse> GetUserPaDetailAsync(int paId, int evaluateeId)
        {
            var pa = await _unitOfWork.PaRepository.GetByIdAsync(paId);
            // Kiểm tra kỳ đánh giá không tồn tại.
            if (pa == null)
            {
                throw new Exception("Kỳ đánh giá này không tồn tại.");
            }

            // Kiểm tra kỳ đánh giá chưa triển khai.
            if (pa.IsPublic == false)
            {
                throw new Exception("Kỳ đánh giá này chưa triển khai.");
            }

            var paDetail = pa.PaDetails.FirstOrDefault(x => x.UserId == evaluateeId);
            // Kiểm tra người tham gia không tồn tại.
            if (paDetail == null)
            {
                throw new Exception("Người tham gia này không tồn tại.");
            }

            var paRelatives = paDetail.PaRelatives.Where(x => x.PaHistory != null).ToList();
            // Kiểm tra người tham gia chưa có người đánh giá đã hoàn thành.
            if (paRelatives.Count(x => x.PaHistory.Status == (int)EPaHistoryStatus.Completed) <= 0)
            {
                throw new Exception("Đánh giá này chưa có ai đánh giá.");
            }

            var self = paRelatives.FirstOrDefault(x => x.AssessUser == null);
            var appraisers = paRelatives.Where(x => x.AssessUser != null).ToList();

            #region Lấy ra feedback cá nhân
            var personalFeedbacks = new List<PersonalFeedbackUserPaDetailResponse>();
            if (self != null)
            {
                var paFeedbacks = self.PaHistory.PaFeedbacks.ToList();

                personalFeedbacks.Add(new PersonalFeedbackUserPaDetailResponse()
                {
                    QuestionGroupName = "What should the individual START doing to be more effective?",
                    Answers = paFeedbacks.Where(x => !string.IsNullOrEmpty(x.AnswerFirst)).Select(x => x.AnswerFirst).ToList(),
                });

                personalFeedbacks.Add(new PersonalFeedbackUserPaDetailResponse()
                {
                    QuestionGroupName = "What should the individual CONTINUE doing to be more effective?",
                    Answers = paFeedbacks.Where(x => !string.IsNullOrEmpty(x.AnswerSecond)).Select(x => x.AnswerSecond).ToList(),
                });

                personalFeedbacks.Add(new PersonalFeedbackUserPaDetailResponse()
                {
                    QuestionGroupName = "What should the individual STOP doing to be more effective?",
                    Answers = paFeedbacks.Where(x => !string.IsNullOrEmpty(x.AnswerThird)).Select(x => x.AnswerThird).ToList(),
                });

                personalFeedbacks.Add(new PersonalFeedbackUserPaDetailResponse()
                {
                    QuestionGroupName = "General Assessment",
                    Answers = paFeedbacks.Where(x => !string.IsNullOrEmpty(x.AnswerFourth)).Select(x => x.AnswerFourth).ToList(),
                });

                personalFeedbacks.Add(new PersonalFeedbackUserPaDetailResponse()
                {
                    QuestionGroupName = "What would you do in next 12 months for developing your career?",
                    Answers = paFeedbacks.Where(x => !string.IsNullOrEmpty(x.AnswerFifth)).Select(x => x.AnswerFifth).ToList(),
                });

                personalFeedbacks.Add(new PersonalFeedbackUserPaDetailResponse()
                {
                    QuestionGroupName = "How would you want to be in next 3 years?",
                    Answers = paFeedbacks.Where(x => !string.IsNullOrEmpty(x.AnswerSixth)).Select(x => x.AnswerSixth).ToList(),
                });
            }
            #endregion

            #region Lấy ra feedback tất cả thành viên
            var allFeedbacks = new List<AllFeedbackUserPaDetailResponse>();
            var firstAnswersList = new List<string>();
            var secondAnswersList = new List<string>();
            var thirdAnswersList = new List<string>();
            var fourthAnswersList = new List<string>();
            var fifthAnswersList = new List<string>();
            var sixthAnswersList = new List<string>();

            foreach (var item in appraisers)
            {
                var paFeedbacks = item.PaHistory.PaFeedbacks.ToList();
                firstAnswersList.AddRange(paFeedbacks.Where(x => !string.IsNullOrEmpty(x.AnswerFirst)).Select(x => x.AnswerFirst).ToList());
                secondAnswersList.AddRange(paFeedbacks.Where(x => !string.IsNullOrEmpty(x.AnswerSecond)).Select(x => x.AnswerSecond).ToList());
                thirdAnswersList.AddRange(paFeedbacks.Where(x => !string.IsNullOrEmpty(x.AnswerThird)).Select(x => x.AnswerThird).ToList());
                fourthAnswersList.AddRange(paFeedbacks.Where(x => !string.IsNullOrEmpty(x.AnswerFourth)).Select(x => x.AnswerFourth).ToList());
                fifthAnswersList.AddRange(paFeedbacks.Where(x => !string.IsNullOrEmpty(x.AnswerFifth)).Select(x => x.AnswerFifth).ToList());
                sixthAnswersList.AddRange(paFeedbacks.Where(x => !string.IsNullOrEmpty(x.AnswerSixth)).Select(x => x.AnswerSixth).ToList());
            }

            allFeedbacks.Add(new AllFeedbackUserPaDetailResponse()
            {
                QuestionGroupName = "What should the individual START doing to be more effective?",
                Answers = firstAnswersList,
            });

            allFeedbacks.Add(new AllFeedbackUserPaDetailResponse()
            {
                QuestionGroupName = "What should the individual CONTINUE doing to be more effective?",
                Answers = secondAnswersList,
            });

            allFeedbacks.Add(new AllFeedbackUserPaDetailResponse()
            {
                QuestionGroupName = "What should the individual STOP doing to be more effective?",
                Answers = thirdAnswersList,
            });

            allFeedbacks.Add(new AllFeedbackUserPaDetailResponse()
            {
                QuestionGroupName = "General Assessment",
                Answers = fourthAnswersList,
            });
            #endregion

            int levelId = (int)self.PaHistory.Level.Id;
            bool isManagerLevel = IsManagerLevel(levelId);

            return new UserPaDetailResponse()
            {
                PaId = pa.Id,
                Name = pa.Name,
                PersonalFeedBack = personalFeedbacks,
                AllFeedBack = allFeedbacks,
                TeamAverage = isManagerLevel ? await GetAppraisersMarkForManagerAsync(pa, paDetail, levelId) : await GetAppraisersMarkAsync(pa, paDetail),
                IsManager = isManagerLevel ? (levelId == 4 ? false : true) : false,
                Evaluatee = new EvaluateeUserPaDetailResponse()
                {
                    EvaluateeId = paDetail.User.Id,
                    FullName = paDetail.User.FullName,
                    Level = self.PaHistory?.Level?.Name,
                    Department = paDetail.User.Department?.Name,
                    JobTitle = self.PaHistory?.Job?.Name,
                    AcceptedOfferDate = paDetail.User.AcceptOfferDate,
                    Completed = self.PaHistory.Status == (int)EPaHistoryStatus.Completed,
                    Mark = await GetPaMarkAsync(paId, self.PaHistory.Id, self.PaHistory.Status == (int)EPaHistoryStatus.Completed),
                },
            };
        }
        /// <summary>
        /// User xem form đánh giá của mình.
        /// </summary>
        /// <param name="paId"></param>
        /// <param name="evaluateeId"></param>
        /// <param name="appraiserId"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task<UserPaFormDataResponse> GetUserPaFormAsync(int paId, int evaluateeId, int appraiserId)
        {
            bool isRead = false;

            var pa = await _unitOfWork.PaRepository.GetByIdAsync(paId);
            // Kiểm tra kỳ đánh giá không tồn tại.
            if (pa == null)
            {
                throw new Exception("Kỳ đánh giá này không tồn tại.");
            }

            // Kiểm tra kỳ đánh giá chưa triển khai.
            if (!pa.IsPublic)
            {
                throw new Exception("Kỳ đánh giá này chưa triển khai.");
            }

            var paDetail = pa.PaDetails.FirstOrDefault(x => x.UserId == evaluateeId);
            // Kiểm tra người tham gia này tồn tại.
            if (paDetail == null)
            {
                throw new Exception("Người tham gia này không tồn tại.");
            }

            var paRelatives = paDetail.PaRelatives.Where(x => x.PaHistory != null);
            var appraiser = paRelatives.FirstOrDefault(x => x.AssessUserId == (evaluateeId == appraiserId ? null : appraiserId));
            // Kiểm tra người đánh giá có được xét đánh giá người tham gia.
            if (appraiser == null)
            {
                throw new Exception("Không thể xem đánh giá người dùng này.");
            }

            if ((pa.IsPublic == true && pa.IsCompleted == true) || appraiser.PaHistory!.Status == (int)EPaHistoryStatus.Completed)
            {
                isRead = true;
            }
            var now = DateTime.Now.UTCToIct();

            #region Lấy ra tất cả các projects
            var joinedProjects = appraiser.PaHistory!.PaProjects.Select(item => new JoinedProjectUserPaFormDataResponse()
            {
                Id = item.Id,
                Name = item.ProjectName ?? string.Empty,
                Status = (EPaProjectStatus?)item.ProjectStatus,
                StartDate = item.StartDate,
                EndDate = item.EndDate,
            }).ToList();
            #endregion

            #region Lấy ra tất cả các câu hỏi đã đánh giá
            var reviews = appraiser.PaHistory.PaScores.Select(item => new ReviewUserPaFormDataResponse()
            {
                QuestionGroupId = item.Question.QuestionGroupId,
                QuestionId = item.QuestionId,
                EvaluationId = item.QuestionScore,
            }).ToList();
            #endregion

            #region Lấy ra tất cả các điểm câu hỏi đã đánh giá
            var questions = await _unitOfWork.PaDetailRepository.GetQuestionGroupMarksAsync(pa.Id, appraiser.PaHistory.Id);
            var reviewMarks = questions.Select((item) =>
            {
                return new ReviewMarksUserPaFormDataResponse()
                {
                    QuestionGroupId = item.Id,
                    Mark = item.Mark == 0 ? 0 : GetQuestionGroupMark(item.Percent, item.Count, item.Mark),
                };
            }).ToList();
            #endregion

            #region Lấy ra tất cả các feedback
            var allFeedbacks = appraiser.PaHistory.PaFeedbacks.FirstOrDefault();
            var generalAssessments = new List<GeneralAssessmentUserPaFormDataResponse>()
            {
                new GeneralAssessmentUserPaFormDataResponse()
                {
                    QuestionGroupId = 1,
                    QuestionId = 1,
                    Answer = allFeedbacks?.AnswerFirst ?? "",
                },
                new GeneralAssessmentUserPaFormDataResponse()
                {
                    QuestionGroupId = 1,
                    QuestionId = 2,
                    Answer = allFeedbacks?.AnswerSecond ?? "",
                },
                new GeneralAssessmentUserPaFormDataResponse()
                {
                    QuestionGroupId = 1,
                    QuestionId = 3,
                    Answer = allFeedbacks?.AnswerThird ?? "",
                },
                new GeneralAssessmentUserPaFormDataResponse()
                {
                    QuestionGroupId = 1,
                    QuestionId = 4,
                    Answer = allFeedbacks?.AnswerFourth ?? "",
                }
            };
            var careerPaths = new List<CareerPathUserPaFormDataResponse>()
            {
                new CareerPathUserPaFormDataResponse()
                {
                    QuestionGroupId = 1,
                    QuestionId = 1,
                    Answer = allFeedbacks?.AnswerFifth ?? "",
                },
                new CareerPathUserPaFormDataResponse()
                {
                    QuestionGroupId = 1,
                    QuestionId = 2,
                    Answer = allFeedbacks?.AnswerSixth ?? "",
                }
            };
            #endregion

            return new UserPaFormDataResponse()
            {
                Id = pa.Id,
                Name = pa.Name!,
                FirstRequest = appraiser.PaHistory.FirstRequest,
                SecondRequest = appraiser.PaHistory.SecondRequest,
                ClosedSecondRequest = pa.ClosedOnDate.HasValue ? (now - pa.ClosedOnDate.Value).Days > 7 : false,
                Evaluatee = new EvaluateeUserPaFormDataResponse()
                {
                    Id = paDetail.User.Id,
                    FullName = paDetail.User.FullName!,
                    JobTitle = appraiser.PaHistory.Job!.Name + " - " + appraiser.PaHistory.Level!.Name,
                    JobId = appraiser.PaHistory.Job.Id,
                    LevelId = appraiser.PaHistory.Level.Id,
                },
                IsSelf = evaluateeId == appraiserId,
                JoinedProjects = joinedProjects,
                Reviews = reviews,
                GeneralAssessments = generalAssessments,
                CareerPaths = careerPaths,
                Readonly = isRead,
                IsAppraiser = evaluateeId != appraiserId,
                Status = pa.IsPublic ? pa.IsCompleted ? EPaPeriodStatus.Closed : EPaPeriodStatus.Submitted : EPaPeriodStatus.New,
                Completed = appraiser.PaHistory.Status == (int)EPaHistoryStatus.Completed,
                ReviewMarks = reviewMarks,
                IsManager = appraiser.IsManager,
            };
        }
        /// <summary>
        /// Lấy ra danh sách tất cả các câu hỏi của một form đánh giá.
        /// </summary>
        /// <param name="pa"></param>
        /// <param name="paDetail"></param>
        /// <param name="paRelative"></param>
        /// <returns></returns>
        public async Task<List<QuestionGroupForm>> GetPaFormQuestionsAsync(int paId, int paHistoryId)
        {
            var questions = await _unitOfWork.PaDetailRepository.GetPaFormQuestionsAsync(paId, paHistoryId);
            var questionGroups = questions.GroupBy(x => new { x.QuestionGroupId, x.QuestionGroupName, x.QuestionGroupPercent, x.QuestionGroupMinium }).Select(y => new QuestionGroupForm()
            {
                Id = y.Key.QuestionGroupId,
                Name = y.Key.QuestionGroupName,
                Mark = y.Key.QuestionGroupPercent,
                MinimumQuestionsMustAchieved = y.Key.QuestionGroupMinium,
                Questions = y.GroupBy(z => new { z.QuestionId, z.QuestionContent, z.QuestionDescription }).Select(item => new QuestionForm()
                {
                    Id = item.Key.QuestionId,
                    Name = item.Key.QuestionContent,
                    Description = item.Key.QuestionDescription,
                    GuidelineRange = item.Select(g => new GuidelineRangeForm()
                    {
                        Id = g.ScoreRange,
                        Name = g.ScoreRangeName,
                    }).ToList(),
                }).ToList(),
            }).ToList();
            return questionGroups;
        }
        /// <summary>
        /// Hiển thị ghi chú của quản lý lên form đánh giá.
        /// </summary>
        /// <param name="paId"></param>
        /// <param name="evaluateeId"></param>
        /// <param name="userId"></param>
        /// <param name="IsAdmin"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task<PaNoteResponse> GetOneToOneNoteAsync(int paId, int evaluateeId)
        {
            var pa = await _unitOfWork.PaRepository.GetByIdAsync(paId);
            // Kiểm tra kỳ đánh giá này không tồn tại.
            if (pa == null)
            {
                throw new Exception("Kỳ đánh giá này không tồn tại.");
            }

            var paDetail = pa.PaDetails.FirstOrDefault(x => x.UserId == evaluateeId);
            // Kiểm tra người tham gia này không tồn tại.
            if (paDetail == null)
            {
                throw new Exception("Người tham gia này không tồn tại.");
            }

            var manager = paDetail.PaRelatives.FirstOrDefault(x => x.IsManager == true);

            return new PaNoteResponse()
            {
                Note = manager == null ? "" : manager.PaHistory?.OneToOneNote,
                Proposal = manager == null ? "" : manager.PaHistory?.Proposal
            };
        }
        /// <summary>
        /// Quản lý cập nhật ghi chú của mình trên form đánh giá của người tham gia.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="appraiserId"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task UpdateOneToOneNoteAsync(PaNoteRequest request, int appraiserId)
        {
            var pa = await _unitOfWork.PaRepository.GetByIdAsync(request.PaId);
            // Kiểm tra kỳ đánh giá này không tồn tại.
            if (pa == null)
            {
                throw new Exception("Kỳ đánh giá này không tồn tại.");
            }

            var paDetail = pa.PaDetails.FirstOrDefault(x => x.UserId == request.EvaluateeId);
            // Kiểm tra người tham gia này không tồn tại.
            if (paDetail == null)
            {
                throw new Exception("Người tham gia này không tồn tại.");
            }

            var paRelative = paDetail.PaRelatives.FirstOrDefault(x => x.AssessUserId == appraiserId);
            // Kiểm tra người dùng không phải là người đánh giá form này.
            if (paRelative == null)
            {
                throw new Exception("Bạn không phải là người đánh giá form này.");
            }

            // Kiểm tra người dùng không phải là quản lý của người tham gia này.
            if (paRelative.IsManager == false)
            {
                throw new Exception("Bạn không phải là quản lý của người này.");
            }

            // Chặn update khi kỳ pa đã đóng
            if (!pa.IsCompleted)
            {
                paRelative.PaHistory.OneToOneNote = request.Note;
                paRelative.PaHistory.Proposal = request.Proposal;
            }
            else
            {
                throw new Exception("Kỳ đánh giá đã đóng! Không thể cập nhật");
            }
            await _unitOfWork.SaveChangesAsync();
        }
        public async Task<PaHistory> UpdatePaFormAsync(int appraiserId, PaFormDataRequest request)
        {
            var pa = await _unitOfWork.PaRepository.GetByIdAsync(request.Id);
            // Kiểm tra kỳ đánh giá không tồn tại.
            if (pa == null)
            {
                throw new Exception("Kỳ đánh giá này không tồn tại.");
            }

            // Kiểm tra kỳ đánh giá chưa triển khai.
            if (pa.IsPublic == false)
            {
                throw new Exception("Kỳ đánh giá này chưa triển khai.");
            }

            // Kiểm tra kỳ đánh giá đã đóng.
            if (pa.IsPublic == true && pa.IsCompleted == true)
            {
                throw new Exception("Kỳ đánh giá này đã đóng! Không thể cập nhật.");
            }

            var paDetail = pa.PaDetails.FirstOrDefault(x => x.UserId == request.Evaluatee.Id);
            // Kiểm tra người tham gia không tìm thấy.
            if (paDetail == null)
            {
                throw new Exception("Người tham gia này không tồn tại.");
            }

            var paRelative = paDetail.PaRelatives.FirstOrDefault(x => x.AssessUserId == (request.Evaluatee.Id == appraiserId ? null : appraiserId));
            // Kiểm tra người đánh giá không thể đánh giá người tham gia khi chưa được xét.
            if (paRelative == null)
            {
                throw new Exception("Không thể đánh giá người tham gia này.");
            }

            var paHistory = paRelative.PaHistory;
            // Kiểm tra đánh giá đã hoàn thành thì không thể lưu nữa.
            if (paHistory.Status == (int)EPaHistoryStatus.Completed)
            {
                throw new Exception("Đánh giá này đã hoàn thành! Không thể cập nhật");
            }

            var questions = await _unitOfWork.PaDetailRepository.GetQuestionGroupMarksAsync(pa.Id, paHistory.Id);
            // Kiểm tra kỳ đánh giá chưa triển khai.
            if (questions.Sum(x => x.Percent) < 100)
            {
                throw new Exception("Đánh giá này chưa chuẩn bị đủ điểm hoặc câu hỏi đánh giá.");
            }

            #region Thêm, xóa, cập nhật thông tin dự án trong đánh giá.
            var allProjects = paHistory.PaProjects.ToList();
            var addProjects = new List<PaProject>();

            foreach (var item in request.JoinedProjects)
            {
                var project = allProjects.FirstOrDefault(x => x.Id == item.Id);
                if (project == null)
                {
                    addProjects.Add(new PaProject()
                    {
                        PaHistoryId = paHistory.Id,
                        ProjectName = item.Name,
                        ProjectStatus = (int?)item.Status,
                        StartDate = item.StartDate,
                        EndDate = item.EndDate,
                    });
                }
                else
                {
                    project.ProjectName = item.Name;
                    project.ProjectStatus = (int?)item.Status;
                    project.StartDate = item.StartDate;
                    project.EndDate = item.EndDate;
                    allProjects.Remove(project);
                }
            }

            await _unitOfWork.PaProjectRepository.CreateRangeAsync(addProjects);
            await _unitOfWork.PaProjectRepository.DeleteRangeAsync(allProjects);
            #endregion

            #region Đánh điểm và lưu điểm bài đánh giá
            var allScores = paHistory.PaScores.ToList();
            var addScores = new List<PaScore>();

            foreach (var item in request.Reviews)
            {
                if (item.QuestionId != 0)
                {
                    var score = allScores.FirstOrDefault(x => x.QuestionId == item.QuestionId);
                    if (score != null)
                    {
                        score.QuestionScore = item.EvaluationId == null ? 0 : ((int)item.EvaluationId > 5 || (int)item.EvaluationId < 1) ? 0 : (int)item.EvaluationId;
                    }
                }
            }
            await _unitOfWork.PaScoreRepository.CreateRangeAsync(addScores);
            #endregion

            #region Thêm hoặc cập nhật feedback của người đánh giá.
            // câu trả lời thứ nhất
            var answerFirst = request.GeneralAssessments.FirstOrDefault(x => x.QuestionId == 1);
            // câu trả lời thứ hai
            var answerSecond = request.GeneralAssessments.FirstOrDefault(x => x.QuestionId == 2);
            // câu trả lời thứ ba
            var answerThird = request.GeneralAssessments.FirstOrDefault(x => x.QuestionId == 3);
            // câu trả lời thứ tư
            var answerFourth = request.GeneralAssessments.FirstOrDefault(x => x.QuestionId == 4);
            // câu trả lời thứ năm
            var answerFifth = request.CareerPaths.FirstOrDefault(x => x.QuestionId == 1);
            // câu trả lời thứ sáu
            var answerSixth = request.CareerPaths.FirstOrDefault(x => x.QuestionId == 2);

            var feedBack = paHistory.PaFeedbacks.FirstOrDefault();
            if (feedBack == null)
            {
                // feedback chưa có trong cơ sở dữ liệu nên tạo mới
                await _unitOfWork.PaFeedbackRepository.CreateAsync(new PaFeedback()
                {
                    PaHistoryId = paHistory.Id,
                    AnswerFirst = answerFirst.Answer,
                    AnswerSecond = answerSecond.Answer,
                    AnswerThird = answerThird.Answer,
                    AnswerFourth = answerFourth.Answer,
                    AnswerFifth = answerFifth.Answer,
                    AnswerSixth = answerSixth.Answer,
                });
            }
            else
            {
                // feedback này có trong cơ sở dữ liệu nên cập nhật
                feedBack.AnswerFirst = answerFirst.Answer;
                feedBack.AnswerSecond = answerSecond.Answer;
                feedBack.AnswerThird = answerThird.Answer;
                feedBack.AnswerFourth = answerFourth.Answer;
                feedBack.AnswerFifth = answerFifth.Answer;
                feedBack.AnswerSixth = answerSixth.Answer;
            }
            #endregion

            paHistory.FirstRequest = request.FirstRequest;
            await _unitOfWork.SaveChangesAsync();

            // Trả ra lịch sử đánh giá để khi hoàn thành đánh giá thì lấy ra cập nhật tình trạng của nó.
            return paHistory;
        }
        /// <summary>
        /// User hoàn thành form đánh giá của mình.
        /// </summary>
        /// <param name="appraiserId"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        public async Task CompletePaFormAsync(int appraiserId, PaFormDataRequest request)
        {
            // Lưu thông tin cập nhật bài đánh giá.
            var paHistory = await UpdatePaFormAsync(appraiserId, request);

            // Cập nhật lại tình trạng bài đánh giá thành hoàn thành.
            paHistory.Status = (int)EPaHistoryStatus.Completed;
            await _unitOfWork.SaveChangesAsync();
        }
    }
}
