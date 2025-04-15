using InternalPortal.ApplicationCore.Models.Pa;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InternalPortal.ApplicationCore.Interfaces.Business
{
    public interface IPaService
    {
        Task<List<MyAnnualPa>> GetMyPaAnnualAsync(int userId);
        Task<List<MyManualPaGroupYear>> GetMyPaManualAsync(int userId);
    }
}
