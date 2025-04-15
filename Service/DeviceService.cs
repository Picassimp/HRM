using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Interfaces;
using InternalPortal.ApplicationCore.Interfaces.Business;

namespace InternalPortal.Infrastructure.Services.Business
{
    public class DeviceService : IDeviceService
    {
        private readonly IUnitOfWork _unitOfWork;

        public DeviceService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }
        public async Task SaveDeviceAsync(int userId, string deviceId, string registrationToken)
        {
            var device = await _unitOfWork.DeviceRepository.GetUserDeviceAsync(userId,deviceId);

            if (device == null)
            {
                await _unitOfWork.DeviceRepository.CreateAsync(new Device()
                {
                    DeviceId = deviceId,
                    UserId = userId,
                    RegistrationToken = registrationToken,
                });
            }
            else
            {
                device.RegistrationToken = registrationToken;
                await _unitOfWork.DeviceRepository.UpdateAsync(device);
            }

            await _unitOfWork.SaveChangesAsync();
        }
    }
}
