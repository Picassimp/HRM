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
    public interface IOnsiteApplicationRepository : IRepository<OnsiteApplication>
    {
        Task<List<OnsiteApplicationPagingRawModel>> GetAllWithPagingAsync(OnsiteApplicationCriteriaModel searchModel, int userId);
        Task<List<OnsiteApplication>> GetByUserIdAndDateAsync(int userId, DateTime fromDate, DateTime toDate);
        Task<List<OnsiteApplicationMobilePagingModelRaw>> GetAllWithPagingMobileAsync(OnsiteApplicationMobileCriteriaModel searchModel, int userId);
    }
}
