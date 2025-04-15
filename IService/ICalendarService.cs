using InternalPortal.ApplicationCore.Models.Calendar;

namespace InternalPortal.ApplicationCore.Interfaces.Business
{
    public interface ICalendarService
    {
        Task<List<object>> GetAllWithPagingAsync(CalendarRequest request, int userId);
    }
}
