using InternalPortal.ApplicationCore.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InternalPortal.ApplicationCore.Interfaces.Repositories
{
    public interface IDeviceRepository : IRepository<Device>
    {
        Task<List<Device>> GetByUserIdAsync(int userId);
        Task<Device> GetUserDeviceAsync(int userId,string deviceId);
    }
}
