using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace InternalPortal.Infrastructure.Persistence.Repositories
{
    public class LeaveApplicationTypeRepository : EfRepository<LeaveApplicationType>, ILeaveApplicationTypeRepository
    {
        public LeaveApplicationTypeRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<List<LeaveApplicationType>> GetByGroupUserIdAsync(int groupUserId)
        {
            return await DbSet.Where(o => o.LeaveApplicationTypeGroups.Any(o => o.GroupUserId == groupUserId)).ToListAsync();
        }
    }
}
