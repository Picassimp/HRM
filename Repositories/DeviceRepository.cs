using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Interfaces;
using InternalPortal.ApplicationCore.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InternalPortal.Infrastructure.Persistence.Repositories
{
    public class DeviceRepository : EfRepository<Device>, IDeviceRepository
    {
        public DeviceRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<List<Device>> GetByUserIdAsync(int userId)
        {
            return await DbSet.Where(x => x.UserId == userId).ToListAsync();
        }

        public async Task<Device> GetUserDeviceAsync(int userId, string deviceId)
        {
            var device = await DbSet.Where(x => x.UserId == userId & x.DeviceId == deviceId).FirstOrDefaultAsync();
            return device;
        }
    }
}
