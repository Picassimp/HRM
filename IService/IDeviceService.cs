using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InternalPortal.ApplicationCore.Interfaces.Business
{
    public interface IDeviceService
    {
        Task SaveDeviceAsync(int userId, string deviceId, string registrationToken);
    }
}
