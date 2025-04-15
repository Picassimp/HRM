using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Models.UserCertificate;

namespace InternalPortal.ApplicationCore.Interfaces.Repositories
{
    public interface IUserCertificateRepository : IRepository<UserCertificate>
    {
        Task<List<UserCertificate>> GetMyCertificate (int userId);  
    }
}
