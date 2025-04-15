using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Models.CriteriaModel;
using InternalPortal.ApplicationCore.Models.PagingModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InternalPortal.ApplicationCore.Interfaces.Repositories
{
    public interface IWorkFromHomeApplicationRepository : IRepository<WorkFromHomeApplication>
    {
        Task<List<WorkFromHomeApplicationPagingRawModel>> GetAllWithPagingAsync(WorkFromHomeApplicationCriteriaModel model, int userId);
        Task<List<WorkFromHomeApplication>> GetByUserIdAndDateAsync(int userId, DateTime fromDate, DateTime toDate);
        Task<double> GetTotalWFHDayInMonthByUserIdAndDateAsync(int userId, DateTime date);
        Task<List<WorkFromHomeApplicationPagingMobileModelRaw>> GetAllWithPagingMobileAsync(WorkFromHomeApplicationSearchMobileModel searchModel, int userId);
    }
}
