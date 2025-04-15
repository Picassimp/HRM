using FirebaseAdmin.Messaging;
using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Enums;
using InternalPortal.ApplicationCore.Helpers;
using InternalPortal.ApplicationCore.Interfaces;
using InternalPortal.ApplicationCore.Interfaces.Business;
using InternalPortal.ApplicationCore.Interfaces.MessageSenders;
using InternalPortal.ApplicationCore.Interfaces.Repositories;
using InternalPortal.ApplicationCore.Interfaces.Utilities.Firebase;
using InternalPortal.ApplicationCore.Models;
using InternalPortal.ApplicationCore.Models.AccessControl;
using InternalPortal.ApplicationCore.Models.Log;
using InternalPortal.ApplicationCore.Models.MessagingModels;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using RestSharp;
using static InternalPortal.ApplicationCore.ValueObjects.Global;

namespace InternalPortal.Infrastructure.Services.Business
{
    public class AccessControlLogService : IAccessControlLogService
    {
        private readonly IConfiguration _configuration;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogMessageSender _logMessageSender;
        private readonly IFirebaseMessageCloudService _firebaseMessageCloudService;
        public AccessControlLogService(
            IConfiguration configuration,
            IUnitOfWork unitOfWork,
            ILogMessageSender logMessageSender,
            IFirebaseMessageCloudService firebaseMessageCloudService
            )
        {
            _configuration = configuration;
            _unitOfWork = unitOfWork;
            _logMessageSender = logMessageSender;
            _firebaseMessageCloudService = firebaseMessageCloudService;
        }
        #region Private methods
        private async Task<ApiResponseModel<T>> CallApiAsync<T>(string host, string resource, Method method, Dictionary<string, object> parameters = null, object body = null)
        {
            var result = new ApiResponseModel<T>();
            var client = new RestClient(host);
            var request = new RestRequest(resource, method);
            result.Url = $"{host}/{resource}";
            request.AddHeader("Content-Type", "application/json");
            if (parameters != null && parameters.Any())
            {
                foreach (var item in parameters)
                {
                    request.AddQueryParameter(item.Key, item.Value != null ? item.Value.ToString() : null);
                }
            }
            if (body != null)
            {
                request.AddJsonBody(body);
            }
            request.RequestFormat = DataFormat.Json;
            request.Timeout = TimeSpan.FromSeconds(60);
            var response = await client.ExecuteAsync(request);

            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                result.Status = true;
                try
                {
                    var data = JsonConvert.DeserializeObject<T>(response.Content);
                    result.Data = data;
                    result.StatusCode = (int)response.StatusCode;
                }
                catch (Exception e)
                {
                }
            }
            else
            {
                try
                {
                    var error = response.Content;

                    result.Error = new ErrorResultModel { Message = error };
                    result.Status = false;
                    result.StatusCode = (int)response.StatusCode;
                }
                catch (Exception e)
                {
                    throw new Exception($"Message: {e.Message}; StackTrace: {e.StackTrace}; {e.Message}, Url: {client.BuildUri(request).AbsoluteUri}", e);
                }
                throw new Exception($"Status Code: {(int)response.StatusCode}; {result.Error.Message}; Url: {client.BuildUri(request).AbsoluteUri}");
            }
            return result;
        }
        private DateTime? ConvertDateTimeUtcToItc(DateTime? dateTime)
        {
            if (dateTime == null)
                return null;
            return dateTime.Value.UTCToIct();
        }
        private void SendNotification(int userId, string message)
        {
            Dictionary<string, string> data = new Dictionary<string, string>();
            data.Add("Event", "AccessLog");
            data.Add("UserId", userId.ToString());

            string title = "NOIS Welcome";
            string body = message;
            data.Add("EventType", "NoisAccessLog");
            Notification notification = new Notification
            {
                Title = title,
                Body = body
            };

            var userDevices = _unitOfWork.DeviceRepository.GetByUserIdAsync(userId).Result.ToList();
            if (userDevices.Count > 1)
            {
                List<string> registrationTokens = userDevices.Select(x => x.RegistrationToken).ToList();
                var task = Task.Run(async () => await _firebaseMessageCloudService.SendMultiCastAsync(registrationTokens, notification, data));
                task.Wait();
            }
            else
            {
                var userDevice = userDevices.FirstOrDefault();
                if (userDevice == null) return;
                string registrationToken = userDevice.RegistrationToken;
                var task = Task.Run(async () => await _firebaseMessageCloudService.SendAsync(registrationToken, notification, data));
                task.Wait();
            }
        }

        private void SendNotificationFromCard(int userId, DateTime? birthDay, DateTime checkInDate)
        {
            var message = string.Empty;
            // Kiểm tra xem hôm đó có phải là sinh nhật không
            var isBirthDay = birthDay.HasValue ? (birthDay.Value.Date.Day == checkInDate.Day && birthDay.Value.Date.Month == checkInDate.Month) : false;
            if (isBirthDay)
            {
                message = "NOIS chúc bạn sinh nhật vui vẻ, tràn đầy hạnh phúc!";
            }
            else
            {
                message = AccessLogMessage.GetRandomMessage();
            }

            // Send Notification
            SendNotification(userId, message);
        }
        #endregion
        public async Task<CombineResponseModel<ErrorModel>> CallApiToAccessControl(AccessControlApiRequest requestModel)
        {
            var res = new CombineResponseModel<ErrorModel>();
            try
            {
                string host = _configuration["AccessControlHost"];
                string apiKeyHost = _configuration["AccessControlApiKey"];
                string resource = _configuration["AccessControlResource"];
                if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(apiKeyHost))
                {
                    res.ErrorMessage = "Lỗi không thể gửi API qua hệ thống";
                    return res;
                }

                // Call API to Azure service
                var body = new AccessControlApiSend
                {
                    Location = requestModel.Location,
                    Email = requestModel.Email,
                    ApiKey = apiKeyHost
                };
                var parameters = new Dictionary<string, object>();

                await CallApiAsync<string>(host, resource, Method.Post, parameters, body);
                return res;
            }
            catch (Exception e)
            {
                res.ErrorMessage = "Lỗi không thể gửi API qua hệ thống";
                return res;
            }
        }
        public async Task CreateLogAsync(ApiLogModel<AccessControlLogRequest> model)
        {
            // Log request from client
            await _logMessageSender.WriteSystemLogAsync(new SystemLogModel
            {
                LogLevel = (int)ELogLevel.Information,
                Event = "Access Control Log Request",
                RemoteId = model.IpAddress,
                ReferrerUrl = model.Url,
                Method = model.Method,
                ApiRequest = JsonConvert.SerializeObject(model.Data),
                CreatedOnUtc = DateTime.UtcNow,
            });

            // Validation
            if (model.Data == null)
                return;
            var logs = model.Data.Logs;

            // Skip if logs empty
            if (logs == null || !logs.Any())
                return;

            var emails = logs.Select(o => o.Email).Distinct().ToList();
            List<UserInternal> users = new List<UserInternal>();
            foreach (var email in emails)
            {
                var userByEmail = await _unitOfWork.UserInternalRepository.GetByEmailAsync(email);
                if (userByEmail != null)
                {
                    users.Add(userByEmail);
                }
            }
            foreach (var log in logs)
            {
                var user = users.Find(o => o.Email == log.Email);

                if (user == null)
                    continue;

                if (log.CheckIn == null)
                    continue;

                var checkInDate = log.CheckIn.Value.UTCToIct().Date;

                // Kiểm tra xem hôm đó có phải là lần đầu quẹt thẻ hay không
                var isHaveAnyCheckIn = await _unitOfWork.AccessControlLogRepository.IsHaveAnyCheckInAsync(user.Id, checkInDate);
                // Check in, check out using UTC format datetime.
                var newAccessControlLog = new AccessControlLog
                {
                    UserId = user.Id,
                    CheckIn = ConvertDateTimeUtcToItc(log.CheckIn),
                    CheckOut = ConvertDateTimeUtcToItc(log.CheckOut),
                    CreatedDate = DateTime.UtcNow.UTCToIct()
                };

                await _unitOfWork.AccessControlLogRepository.CreateAsync(newAccessControlLog);
                await _unitOfWork.SaveChangesAsync();

                if (!isHaveAnyCheckIn)
                {
                    SendNotificationFromCard(user.Id, user.Birthday, checkInDate);
                }
            }
        }
    }
}
