using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Enums;
using InternalPortal.ApplicationCore.Helpers;
using InternalPortal.ApplicationCore.Interfaces;
using InternalPortal.ApplicationCore.Interfaces.Business;
using InternalPortal.ApplicationCore.Interfaces.Caching;
using InternalPortal.ApplicationCore.Models;
using InternalPortal.ApplicationCore.Models.LeaveApplication;
using InternalPortal.ApplicationCore.Models.User;
using InternalPortal.ApplicationCore.Models.UserContract;
using InternalPortal.ApplicationCore.Models.UserInternal;
using InternalPortal.ApplicationCore.Models.UserSocialInsurance;
using System.Globalization;
using System.Net.Mail;
using static InternalPortal.ApplicationCore.ValueObjects.Global;

namespace InternalPortal.Infrastructure.Services.Business
{
    public class UserInternalService : IUserInternalService
    {
        private readonly ICacheService _cacheService;
        private readonly IUnitOfWork _unitOfWork;
        public UserInternalService(
            IUnitOfWork unitOfWork,
            ICacheService cacheService
            )
        {
            _cacheService = cacheService;
            _unitOfWork = unitOfWork;
        }
        #region private methods
        private bool IsValidEmail(string emailaddress)
        {
            try
            {
                MailAddress m = new MailAddress(emailaddress);

                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }
        private bool IsCompeletePersonalForm(UserInternal user)
        {
            // Số điện thoại cá nhân
            if (string.IsNullOrEmpty(user.MobilePhone)) return false;

            // Email cá nhân
            if (string.IsNullOrEmpty(user.PersonalEmail)) return false;

            // Nơi sinh
            if (string.IsNullOrEmpty(user.BirthCertificateRegistration)) return false;

            // CCCD/ CMND
            if (string.IsNullOrEmpty(user.IdentityCard)) return false;

            // Ngày cấp
            if (user.DateOfIssue.HasValue == false) return false;

            // Nơi cấp
            if (string.IsNullOrEmpty(user.IssuedBy)) return false;

            // Địa chỉ tạm trú
            if (string.IsNullOrEmpty(user.PlaceOfTemporaryResidence)) return false;

            // Địa chỉ thường trú
            if (string.IsNullOrEmpty(user.PlaceOfResidence)) return false;

            // Tên người thân liên hệ khi cần
            if (string.IsNullOrEmpty(user.RelativeName)) return false;

            // Mối quan hệ
            if (string.IsNullOrEmpty(user.Relationship)) return false;

            // Số điện thoại người thân
            if (string.IsNullOrEmpty(user.RelativeMobilePhone)) return false;

            // Tài khoản ngân hàng
            // Nếu có tài khoản ngân hàng, phải nhập đầy đủ thông tin
            if (user.HasBankAccount)
            {
                // Tên ngân hàng
                if (string.IsNullOrEmpty(user.BankName)) return false;

                // Chi nhánh
                if (string.IsNullOrEmpty(user.BankBranch)) return false;

                // Số tài khoản ngân hàng
                if (string.IsNullOrEmpty(user.BankAccountNumber)) return false;
            }

            return true;
        }
        #endregion

        #region public methods
        public bool IsOfficialStaff(string? groupUserName)
        {
            return groupUserName == Constant.OFFICIAL_STAFF_GROUP_NAME;
        }
        public async Task<InternalUserDetailModel?> GetDetailByObjectIdAsync(string objectId, string? managerObjectId = null)
        {
            var user = await _unitOfWork.UserInternalRepository.GetByObjectIdAsync(objectId);
            if (user == null)
            {
                return null;
            }
            var detailModel = new InternalUserDetailModel();
            detailModel.CopyFrom(user, IsOfficialStaff(user.GroupUser?.Name));

            detailModel.DepartmentId = user.DepartmentId;
            detailModel.DepartmentName = user.Department?.Name;

            detailModel.GroupUserId = user.GroupUserId;
            detailModel.GroupUserName = user.GroupUser?.Name;

            detailModel.JobId = user.JobId;
            detailModel.JobName = user.Job?.Name;

            detailModel.LevelId = user.LevelId;
            detailModel.LevelName = user.Level?.Name;

            if (!string.IsNullOrEmpty(managerObjectId))
            {
                var manager = await _unitOfWork.UserInternalRepository.GetByObjectIdAsync(managerObjectId);
                detailModel.ManagerId = manager?.Id;
                detailModel.ManagerFullName = manager?.FullName;
            }

            var hrValue = await _unitOfWork.GlobalConfigurationRepository.GetByNameAsync(Constant.FirstHrEmail);
            bool isHr = false;
            if (hrValue != null && !string.IsNullOrEmpty(hrValue.Value))
            {
                var hrEmails = hrValue.Value!.Split(",").Where(email => !string.IsNullOrEmpty(email)).Select(o => o.Trim()).ToList();
                if (hrEmails.Any(o => o.Equals(detailModel.Email)))
                {
                    isHr = true;
                }
            }
            detailModel.IsHr = isHr;

            var accountantValue = await _unitOfWork.GlobalConfigurationRepository.GetByNameAsync(Constant.SecondHrEmail);
            bool isAccountant = false;
            if (accountantValue != null && !string.IsNullOrEmpty(accountantValue.Value))
            {
                var accountantEmails = accountantValue.Value!.Split(",").Where(email => !string.IsNullOrEmpty(email)).Select(o => o.Trim()).ToList();
                if (accountantEmails.Any(o => o.Equals(detailModel.Email)))
                {
                    isAccountant = true;
                }
            }
            detailModel.IsAccountant = isAccountant;

            var directorValue = await _unitOfWork.GlobalConfigurationRepository.GetByNameAsync(Constant.DirectorEmail);
            bool isDirector = false;
            if (directorValue != null && !string.IsNullOrEmpty(directorValue.Value))
            {
                var directorEmails = directorValue.Value!.Split(",").Where(email => !string.IsNullOrEmpty(email)).Select(o => o.Trim()).ToList();
                if (directorEmails.Any(o => o.Equals(detailModel.Email)))
                {
                    isDirector = true;
                }
            }
            detailModel.IsDirector = isDirector;

            var accountantPaymentRequestValue = await _unitOfWork.GlobalConfigurationRepository.GetByNameAsync(Constant.PaymentRequestAccountEmails);
            bool isPaymentRequestAccountant = false;
            if (accountantPaymentRequestValue != null && !string.IsNullOrEmpty(accountantPaymentRequestValue.Value))
            {
                var accountantEmails = accountantPaymentRequestValue.Value!.Split(",").Where(email => !string.IsNullOrEmpty(email)).Select(o => o.Trim()).ToList();
                if (accountantEmails.Any(o => o.Equals(detailModel.Email)))
                {
                    isPaymentRequestAccountant = true;
                }
            }
            detailModel.IsPaymentRequestAccountant = isPaymentRequestAccountant;

            var directorPaymentRequestValue = await _unitOfWork.GlobalConfigurationRepository.GetByNameAsync(Constant.PaymentRequestDirectorEmails);
            bool isPaymentRequestDirector = false;
            if (directorPaymentRequestValue != null && !string.IsNullOrEmpty(directorPaymentRequestValue.Value))
            {
                var directorEmails = directorPaymentRequestValue.Value!.Split(",").Where(email => !string.IsNullOrEmpty(email)).Select(o => o.Trim()).ToList();
                if (directorEmails.Any(o => o.Equals(detailModel.Email)))
                {
                    isPaymentRequestDirector = true;
                }
            }
            detailModel.IsPaymentRequestDirector = isPaymentRequestDirector;
            return detailModel;
        }
        public async Task<CombineResponseModel<bool>> NormalUpdateAsync(int userId, UserInternalNormalRequest model)
        {
            var res = new CombineResponseModel<bool>();
            var me = await _unitOfWork.UserInternalRepository.GetByIdAsync(userId);
            if (me == null)
            {
                res.ErrorMessage = "Người dùng không tồn tại";
                return res;
            }
            // Validation
            if (!string.IsNullOrEmpty(model.PersonalEmail) && !IsValidEmail(model.PersonalEmail))
            {
                res.ErrorMessage = "Email cá nhân không hợp lệ";
                return res;
            }

            // Chỉ cho phép sửa khi ở màn hình lần đầu đăng nhập
            if (me.IsFirstLogin)
            {
                me.FullName = model.FullName;

                if (!string.IsNullOrEmpty(model.Birthday))
                {
                    var isValidDate = DateTime.TryParseExact(model.Birthday, "dd/MM/yyyy", new CultureInfo("en-GB"), DateTimeStyles.None, out DateTime birthDay);
                    if (!isValidDate)
                    {
                        res.ErrorMessage = "Ngày sinh không hợp lệ";
                        return res;
                    }
                    me.Birthday = birthDay;
                }
                me.Gender = model.Gender == null ? null : (int)model.Gender;
                me.BirthCertificateRegistration = model.BirthPlace;
            }
            // Update additional information
            me.PersonalEmail = model.PersonalEmail;
            me.IdentityCard = model.IdentityCard;

            if (!string.IsNullOrEmpty(model.DateOfIssue))
            {
                var isValidDate = DateTime.TryParseExact(model.DateOfIssue, "dd/MM/yyyy", new CultureInfo("en-GB"), DateTimeStyles.None, out DateTime dateOfIssue);
                if (!isValidDate)
                {
                    res.ErrorMessage = "Ngày cung cấp CCCD không hợp lệ";
                    return res;
                }
                me.DateOfIssue = dateOfIssue;
            }
            me.IssuedBy = model.IssuedBy;
            me.PlaceOfTemporaryResidence = model.PlaceOfTemporaryResidence;
            me.PlaceOfResidence = model.PlaceOfResidence;
            me.RelativeName = model.RelativeName;
            me.Relationship = model.Relationship;
            me.RelativeMobilePhone = model.RelativeMobilePhone;
            me.HasBankAccount = model.HasBankAccount;
            me.BankName = model.BankName;
            me.BankBranch = model.BankBranch;
            me.BankAccountNumber = model.BankAccountNumber;
            me.Education = model.Education == null ? null : (int)model.Education;
            me.TaxIdentificationNumber = model.TaxIdentificationNumber;
            me.MobilePhone = model.MobilePhone;

            // If user filled full infomation, then check IsFirstLogin is false
            if (IsCompeletePersonalForm(me))
                me.IsFirstLogin = false;

            await _unitOfWork.UserInternalRepository.UpdateAsync(me);
            await _unitOfWork.SaveChangesAsync();
            res.Status = true;
            res.Data = me.IsFirstLogin;
            return res;
        }
        public async Task<List<InternalUserDetailModel>> GetAllManagerUsersAsync()
        {
            var users = await _unitOfWork.UserInternalRepository.GetAllManagerUsersAsync();
            return users;
        }

        public async Task<UserDtoModel?> GetUserDtoByObjectIdAsync(string objectId, bool isCache = true)
        {
            UserDtoModel? result = null;

            if (isCache)
            {
                result = await _cacheService.GetFnAsync(string.Format(CacheKey.User_ObjectId, objectId), 60, () =>
                {
                    return _unitOfWork.UserInternalRepository.GetUserDtoByObjectIdAsync(objectId);
                });
            }
            else
            {
                result = await _unitOfWork.UserInternalRepository.GetUserDtoByObjectIdAsync(objectId);
            }
            return result;
        }
        public async Task<UserInformationModel> GetProfileAsync(int userId)
        {
            var me = (await _unitOfWork.UserInternalRepository.GetByIdAsync(userId))!;
            var result = new UserInformationModel();
            // Mapping user profile
            UserInternalNormalModel userInternal = new UserInternalNormalModel
            {
                FullName = me.FullName!,
                Gender = me.Gender == null ? null : (EGender)me.Gender,
                Birthday = me.Birthday,
                DepartmentId = me.DepartmentId,
                JobTitle = me.JobTitle,
                GroupUserId = me.GroupUserId,
                MobilePhone = me.MobilePhone,
                PersonalEmail = me.PersonalEmail,
                Email = me.Email!,
                AcceptOfferDate = me.AcceptOfferDate,
                IdentityCard = me.IdentityCard!,
                DateOfIssue = me.DateOfIssue,
                IssuedBy = me.IssuedBy!,
                PlaceOfResidence = me.PlaceOfResidence!,
                BankAccountNumber = me.BankAccountNumber,
                BankBranch = me.BankBranch,
                BankName = me.BankName,
                HasBankAccount = me.HasBankAccount,
                PlaceOfTemporaryResidence = me.PlaceOfTemporaryResidence!,
                Relationship = me.Relationship!,
                RelativeMobilePhone = me.RelativeMobilePhone!,
                RelativeName = me.RelativeName!,
                BirthPlace = me.BirthCertificateRegistration!,
                IsFirstLogin = me.IsFirstLogin,
                LevelId = me.LevelId,
                JobId = me.JobId,
                Education = me.Education == null ? null : (EEducationLevel)me.Education,
                TaxIdentificationNumber = me.TaxIdentificationNumber
            };

            // UserContracts
            var userContracts = await _unitOfWork.UserContractRepository.GetByUserIdAsync(me.Id);
            var mainContracts = userContracts.Where(o => !o.RefContractId.HasValue).ToList();
            var subContracts = userContracts.Where(o => o.RefContractId.HasValue).ToList();

            var userContractModels = mainContracts.Select(x => new UserContractModel
            {
                Id = x.Id,
                ContractCode = x.ContractCode!,
                UserId = x.UserId,
                ContractTypeId = x.ContractTypeId,
                ContractTypeName = x.ContractType?.Name,
                ExpiryDate = x.ExpiryDate,
                SignatureDate = x.SignatureDate,
                SubContracts = subContracts.Where(o => o.RefContractId!.Value == x.Id).Select(o => new SubContractResponse
                {
                    Id = o.Id,
                    ContractCode = o.ContractCode!,
                    UserId = o.UserId,
                    ContractTypeId = o.ContractTypeId,
                    ContractTypeName = o.ContractType?.Name,
                    ExpiryDate = o.ExpiryDate,
                    SignatureDate = o.SignatureDate,
                }).OrderByDescending(z => z.SignatureDate).ToList()
            }).OrderByDescending(z => z.SignatureDate).ToList();

            UserSocialInsuranceModel? userSocialInsuranceModel = null;
            var userSocialInsurance = await _unitOfWork.UserSocialInsuranceRepository.GetByUserIdFirstOrDefaultAsync(me.Id);
            if (userSocialInsurance == null)
            {
                userSocialInsuranceModel = new UserSocialInsuranceModel();
            }
            else
            {
                userSocialInsuranceModel = new UserSocialInsuranceModel
                {
                    Id = userSocialInsurance.Id,
                    SocialInsuranceNumber = userSocialInsurance.SocialInsuranceNumber,
                    HealthCarePlace = userSocialInsurance.HealthCarePlace,
                    IssuedBook = userSocialInsurance.IssuedBook,
                    IssuedBookNote = userSocialInsurance.IssuedBookNote,
                    ReturnPiece = userSocialInsurance.ReturnPiece,
                    ReturnPieceNote = userSocialInsurance.ReturnPieceNote,
                    ReturnBookToStaff = userSocialInsurance.ReturnBookToStaff,
                    ReturnBookToStaffNote = userSocialInsurance.ReturnBookToStaffNote,
                    ReturnPieceToStaff = userSocialInsurance.ReturnPieceToStaff,
                    ReturnPieceToStaffNote = userSocialInsurance.ReturnPieceToStaffNote,
                    UserId = me.Id
                };
            }

            result.UserInternalNormalModel = userInternal;
            result.UserContractModels = userContractModels;
            result.UserSocialInsuranceModel = userSocialInsuranceModel;

            return result;
        }

        public async Task UpdateDayOffAsync(UserInternal user, LeaveApplication leaveApplication)
        {
            if (leaveApplication.NumberDayOffLastYear > 0) //sử dụng ngày phép năm ngoai
            {
                user.OffDayUseRamainDayOffLastYear += leaveApplication.NumberDayOffLastYear; // cộng ngày phép đã sử dụng năm ngoái
                user.RemainDayOffLastYear -= leaveApplication.NumberDayOffLastYear; // trừ số ngày phép năm ngoái còn lại
            }

            if (leaveApplication.NumberDayOff - leaveApplication.NumberDayOffLastYear > 0) // Trường hợp sử dụng phép của năm ngoái và năm nay & phép ứng
            {
                // Tính số ngày nghỉ năm nay của đơn
                var dayOffCurrentYear = leaveApplication.NumberDayOff - leaveApplication.NumberDayOffLastYear;

                // Cộng ngày phép đã nghỉ
                user.OffDay += dayOffCurrentYear;

                // Có xài ứng phép
                if (leaveApplication.BorrowedDayOff > 0)
                {
                    // Nghỉ cùng tháng thì trừ đúng số ngày nghỉ theo đơn
                    if (leaveApplication.FromDate.Month == leaveApplication.ToDate.Month)
                    {
                        // Ví dụ: vào tháng 10, có 10 ngày phép năm & 2 ngày phép ứng
                        // Submit nghỉ 12d trong cùng tháng 10

                        var borrowedDayOffAllow = user.BorrowedDayOff - user.UsedBorrowedDayOff > 0 ? user.BorrowedDayOff - user.UsedBorrowedDayOff : 0;

                        // Khi duyệt tại tháng 10, phép ứng vẫn còn 2d, yearOffDay = 10
                        if (leaveApplication.BorrowedDayOff <= borrowedDayOffAllow)
                        {
                            user.YearOffDay -= dayOffCurrentYear - leaveApplication.BorrowedDayOff;
                            user.UsedBorrowedDayOff += leaveApplication.BorrowedDayOff;
                        }

                        // Khi duyệt tại tháng 11, phép ứng chỉ còn 1d, yearOffDay = 11
                        else
                        {
                            dayOffCurrentYear -= user.YearOffDay;
                            user.YearOffDay = 0;
                            user.UsedBorrowedDayOff += dayOffCurrentYear;

                            leaveApplication.BorrowedDayOff = dayOffCurrentYear;
                        }
                    }

                    // Nghỉ qua tháng
                    else
                    {
                        // Ví dụ: Vào tháng 1 đầu năm, YearOffDay = 1, NumberDayOffLastYear = 7.5, BorrowedDayOff = 1
                        // Xin nghỉ 9 ngày, NumberDayOff = 9, Số ngày phép của năm ngoái NumberDayOffLastYear = 7.5, Số ngày mượn BorrowedDayOff = 0.5

                        // Tại thời điểm duyệt, kiểm tra số ngày được phép nghỉ năm nay có lớn hơn số ngày xin nghỉ hay không ?
                        // Nếu lớn hơn thì trừ số ngày nghỉ & cập nhật phép mượn
                        // Ngược lại thì trừ số ngày phép dc xài & cập nhật phép mượn về 0

                        // Khi duyệt tại tháng 1, user.YearOffDay = 1
                        // dayOffCurrentYear = numberDayOff - numberDayOffLastYear = 9 - 7.5 = 1.5 >= yearOffDay = 1
                        if (dayOffCurrentYear >= user.YearOffDay)
                        {
                            dayOffCurrentYear -= user.YearOffDay;
                            user.YearOffDay = 0;
                            user.UsedBorrowedDayOff += dayOffCurrentYear;

                            leaveApplication.BorrowedDayOff = dayOffCurrentYear;
                        }

                        // Khi duyệt tại tháng 2, user.YearOffDay = 2
                        // dayOffCurrentYear = 1.5 < yearOffDay = 2
                        else
                        {
                            user.YearOffDay -= dayOffCurrentYear;
                            leaveApplication.BorrowedDayOff = 0;
                        }
                    }

                    leaveApplication.UpdatedDate = DateTime.UtcNow.UTCToIct();
                    await _unitOfWork.LeaveApplicationRepository.UpdateAsync(leaveApplication);
                }

                // Không xài phép ứng
                else
                {
                    user.YearOffDay -= dayOffCurrentYear;
                }
            }

            await _unitOfWork.UserInternalRepository.UpdateAsync(user);
            await _unitOfWork.SaveChangesAsync();
        }

        public async Task<List<InternalUserDetailModel>> GetUsersAsync()
        {
            var users = await _unitOfWork.UserInternalRepository.GetUsersAsync();
            foreach (var item in users)
            {
                if (item.GroupUserName != Constant.OFFICIAL_STAFF_GROUP_NAME)
                {
                    item.OffDay = 0;
                    item.YearOffDay = 0;
                    item.MaxDayOff = 0;
                    item.OffDayUseRamainDayOffLastYear = 0;
                    item.RemainDayOffLastYear = 0;
                }
            }
            return users;
        }

        public async Task<CombineResponseModel<UserInternalForLeaveApplication>> GetDayOffByUserIdAsync(int userId)
        {
            var res = new CombineResponseModel<UserInternalForLeaveApplication>();
            var leaveApplicationTypes = new List<LeaveApplicationTypeModel>();

            var user = await _unitOfWork.UserInternalRepository.GetByIdAsync(userId);
            if (user == null || user.IsDeleted || user.HasLeft)
            {
                res.ErrorMessage = "Người dùng không tồn tại";
                return res;
            } 

            if (user.GroupUserId.HasValue)
            {
                var leaveTypes = await _unitOfWork.LeaveApplicationTypeRepository.GetByGroupUserIdAsync(user.GroupUserId.Value);

                // Mapping Entity to Model
                leaveApplicationTypes = leaveTypes.Select(o => new LeaveApplicationTypeModel
                {
                    Name = o.Name!,
                    Id = o.Id,
                    IsSubTractCumulation = o.IsSubTractCumulation
                }).ToList();
            }

            if (!IsOfficialStaff(user.GroupUser?.Name))
            {
                var response = new UserInternalForLeaveApplication()
                {
                    DayOff = 0,
                    DayOffAllowInYear = 0,
                    MaxDayOff = 0,
                    DayOffAllow = 0,
                    LeaveApplicationTypes = leaveApplicationTypes,
                    SickDayOffAllow = 0
                };

                res.Status = true;
                res.Data = response;
                return res;
            }

            var pendingLeaves = await _unitOfWork.LeaveApplicationRepository.GetPendingByUserIdAsync(user.Id);
            var pendingLeaveSicks = await _unitOfWork.LeaveApplicationRepository.GetPendingSickByUserIdAsync(user.Id);
            var allowDayOff = user.YearOffDay + user.RemainDayOffLastYear;
            var allowDayOffSick = user.SickDayOff;
            var sickDayOffType = leaveApplicationTypes.Find(x => x.Name == Constant.SICK_LEAVE_APPLICATION_TYPE);

            var borrowDayOffAllow = user.BorrowedDayOff - user.UsedBorrowedDayOff > 0 ? user.BorrowedDayOff - user.UsedBorrowedDayOff : 0;
            var userInternalForLeaveApplication = new UserInternalForLeaveApplication()
            {
                DayOff = user.OffDay + user.OffDayUseRamainDayOffLastYear + user.OffDayForSick,
                DayOffAllowInYear = user.YearOffDay + user.RemainDayOffLastYear + user.RemainDayOff2021 + user.SickDayOff + borrowDayOffAllow,
                MaxDayOff = user.MaxDayOff,
                DayOffAllow = allowDayOff >= 0 ? allowDayOff : 0,
                LeaveApplicationTypes = leaveApplicationTypes,
                SickDayOffAllow = allowDayOffSick >= 0 ? allowDayOffSick : 0,
                MaxDayOffSick = user.SickDayOff + user.OffDayForSick,
                OffDayForSick = user.OffDayForSick,
                SickDayOffTypeId = sickDayOffType != null ? sickDayOffType.Id : 0,
                PendingSickDay = pendingLeaveSicks.Sum(o => o.NumberDayOff),
                PendingLeaveDay = pendingLeaves.Sum(o => o.NumberDayOff - o.BorrowedDayOff),
                BorrowedDayOffAllow = borrowDayOffAllow,
                PendingBorrowedDayOff = pendingLeaves.Sum(o => o.BorrowedDayOff)
            };

            res.Status = true;
            res.Data = userInternalForLeaveApplication;
            return res;
        }

        public async Task<PagingResponseModel<UserInternalDayOffResponse>> GetDayOffWithPagingAsync(int userId, UserDayOffCriteriaModel request)
        {
            var recordsRaw = await _unitOfWork.UserInternalRepository.GetAllWithPagingAsync(userId, request);

            var totalRecords = recordsRaw.FirstOrDefault();
            if (totalRecords != null)
            {
                recordsRaw.Remove(totalRecords);
            }

            var pendingLeaves = await _unitOfWork.LeaveApplicationRepository.GetPendingByUserIdAsync(userId);
            var pendingSickLeaves = await _unitOfWork.LeaveApplicationRepository.GetPendingSickByUserIdAsync(userId);

            // Lấy snapshot tháng 3, vì sau 31/3 cột ngày nghỉ phép cuối năm bị reset 0
            // nhưng vẫn tính lại ngày phép cuối năm chuyển giao
            DateTime currentDate = DateTime.UtcNow.UTCToIct();

            var userDayOffSnapShots = await _unitOfWork.UserDayOffSnapshotRepository.GetByUserIdAndYearAsync(userId, currentDate);

            var exchangeDayOffs = await _unitOfWork.ExchangeDayOffRepository.GetByUserIdAsync(userId, currentDate.Year);
            // Lấy danh sách người dùng liên quan tới ngày nghỉ phép
            var records = recordsRaw.Select(r =>
            {
                var user = new UserInternalDayOffResponse
                {
                    Id = r.Id,
                    FullName = r.FullName ?? "",
                    OffDay = r.GroupUserName == Constant.OFFICIAL_STAFF_GROUP_NAME ? r.OffDay : 0,
                    YearOffDay = r.GroupUserName == Constant.OFFICIAL_STAFF_GROUP_NAME ? r.YearOffDay : 0,
                    MaxDayOff = r.GroupUserName == Constant.OFFICIAL_STAFF_GROUP_NAME ? r.MaxDayOff : 0,
                    OffDayUseRamainDayOffLastYear = r.GroupUserName == Constant.OFFICIAL_STAFF_GROUP_NAME ? r.OffDayUseRamainDayOffLastYear : 0,
                    RemainDayOffLastYear = r.GroupUserName == Constant.OFFICIAL_STAFF_GROUP_NAME ? r.RemainDayOffLastYear : 0,
                    BonusDayOff = r.BonusDayOff,
                    SickDayOff = r.GroupUserName == Constant.OFFICIAL_STAFF_GROUP_NAME ? r.SickDayOff + r.OffDayForSick : 0,
                    OffDayForSick = r.GroupUserName == Constant.OFFICIAL_STAFF_GROUP_NAME ? r.OffDayForSick : 0,
                    DayOffExchange = exchangeDayOffs?.DayOffExchange ?? 0,
                    MaxBorrowedDayOff = r.GroupUserName == Constant.OFFICIAL_STAFF_GROUP_NAME ? r.MaxBorrowedDayOff : 0, 
                    BorrowedDayOff = r.GroupUserName == Constant.OFFICIAL_STAFF_GROUP_NAME ? (r.BorrowedDayOff - r.UsedBorrowedDayOff > 0 ? r.BorrowedDayOff - r.UsedBorrowedDayOff : 0) : 0,
                    UsedBorrowedDayOff = r.GroupUserName == Constant.OFFICIAL_STAFF_GROUP_NAME ? r.UsedBorrowedDayOff : 0,
                    TotalRecord = r.TotalRecord,
                    IsYearOffDayEnoughToCompensate = r.YearOffDay > r.UsedBorrowedDayOff
                };

                // Tính số ngày phép năm cũ chuyển sang - Tổng số ngày nghỉ phép cũ của năm ngoái
                // Lấy số phép năm cũ trước tháng 3 - Do đã reset không còn lưu db
                var userDayOffSnapShot = userDayOffSnapShots.Find(u => u.UserId == r.Id);

                if (userDayOffSnapShot != null)
                {
                    decimal remainDayOffLastYear = userDayOffSnapShot.RemainDayOffLastYear;
                    user.ForwardDayOffLastYear = remainDayOffLastYear + user.OffDayUseRamainDayOffLastYear;
                }
                else
                {
                    user.ForwardDayOffLastYear = user.RemainDayOffLastYear + user.OffDayUseRamainDayOffLastYear;
                }

                // Tính số ngày được phép sử dụng
                var pendingLeave = pendingLeaves.Where(o => o.UserId == user.Id);
                var pendingSickLeave = pendingSickLeaves.Where(o => o.UserId == user.Id);

                // Tính số ngày ứng còn được sử dụng
                var borrowedDayOffAllow = r.BorrowedDayOff - r.UsedBorrowedDayOff > 0 ? r.BorrowedDayOff - r.UsedBorrowedDayOff : 0;

                var allowDayOff = user.YearOffDay + borrowedDayOffAllow + user.RemainDayOffLastYear - pendingLeave.Sum(x => x.NumberDayOff);
                var allowDayOffSick = r.SickDayOff - pendingSickLeave.Sum(x => x.NumberDayOff);

                user.AllowDayOff = allowDayOff >= 0 ? allowDayOff : 0;
                user.AllowDayOffSick = allowDayOffSick >= 0 ? allowDayOffSick : 0;

                // Tính lại ngày phép đã có
                if (r.GroupUserName == Constant.OFFICIAL_STAFF_GROUP_NAME)
                {
                    // Nhân viên có 3 ngày phép ứng, BorrowedDayOff = 3
                    // Tháng 1: nghỉ 3d, OffDay = 3 (1 ngày phép năm & 2 ngày phép ứng)
                    // Tháng 2: YearOffDay = 1 + 3 - 0 - 2 = 2d, Tháng 3: YearOffDay = 2 + 3 - 0 - 2 = 3d
                    // Tháng 9: YearOffDay = 8 + 3 - 0 - 2 = 9d
                    // Tháng 10: BorrowedDayOff = 2, YearOffDay = 9 => YearOffDay = 9 + 3 - 0 - 2 = 10d
                    // Tháng 11: BorrowedDayOff = 1, YearOffDay = 9 => YearOffDay = 9 + 3 - 0 - 1 = 11d
                    // Tháng 12: BorrowedDayOff = 1, YearOffDay = 9 => YearOffDay = 9 + 3 - 0 - 0 = 12d

                    if (r.BorrowedDayOff < r.UsedBorrowedDayOff)
                    {
                        user.YearOffDay = user.YearOffDay + user.OffDay - user.BonusDayOff - r.BorrowedDayOff;
                    }
                    else
                    {
                        user.YearOffDay = user.YearOffDay + user.OffDay - user.BonusDayOff - r.UsedBorrowedDayOff;
                    }
                }

                return user;
            }).ToList();

            var res = new PagingResponseModel<UserInternalDayOffResponse>
            {
                Items = records,
                TotalRecord = totalRecords?.TotalRecord ?? 0
            };

            return res;
        }
        #endregion
    }
}
