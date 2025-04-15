using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Models;
using InternalPortal.ApplicationCore.Models.CriteriaModel;
using InternalPortal.ApplicationCore.Models.PagingModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InternalPortal.ApplicationCore.Interfaces.Repositories
{
    public interface IOverTimeApplicationRepository : IRepository<OverTimeApplication>
    {
        Task<List<OverTimePagingModel>> GetAllWithPagingAsync(OverTimeCriteriaModel requestModel, int userId);
        Task<List<OverTimePagingMobileModelRaw>> GetAllWithPagingForMobileAsync(OverTimeCriteriaModel requestModel, int userId);
    }
}
