using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Interfaces.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InternalPortal.Infrastructure.Persistence.Repositories
{
    public class HolidayRepository : EfRepository<Holiday>, IHolidayRepository
    {
        public HolidayRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}
