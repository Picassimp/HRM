using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Interfaces.Repositories;
using InternalPortal.ApplicationCore.Models.UserCertificate;
using Microsoft.EntityFrameworkCore;

namespace InternalPortal.Infrastructure.Persistence.Repositories
{
    public class UserCertificateRepository : EfRepository<UserCertificate>, IUserCertificateRepository
    {
        public UserCertificateRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<List<UserCertificate>> GetMyCertificate(int userId)
        {
            return await DbSet.Where(t=>t.UserId == userId).ToListAsync(); 
        }
    }
}
