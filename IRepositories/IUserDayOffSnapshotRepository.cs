using InternalPortal.ApplicationCore.Entities;

namespace InternalPortal.ApplicationCore.Interfaces.Repositories
{
    public interface IUserDayOffSnapshotRepository : IRepository<UserDayOffSnapShot>
    {
        Task<List<UserDayOffSnapShot>> GetByUserIdAndYearAsync(int userId, DateTime currentDate);
    }
}
