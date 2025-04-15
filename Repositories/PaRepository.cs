using Dapper;
using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Interfaces.Repositories;
using InternalPortal.ApplicationCore.Models.Pa;
using Microsoft.EntityFrameworkCore;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InternalPortal.Infrastructure.Persistence.Repositories
{
    public class PaRepository : EfRepository<Pa>, IPaRepository
    {
        public PaRepository(ApplicationDbContext context) : base(context)
        {
        }
        public async Task<List<MyAnnualPaRaw>> GetMyAnnualAsync(int userId)
        {
            string query = @"Internal_Pa_GetMyAnnualPa";
            var parameters = new DynamicParameters(
                 new
                 {
                     userId
                 }
              );
            var res = await Context.Database.GetDbConnection()
             .QueryAsync<MyAnnualPaRaw>(query, parameters,
                 commandType: CommandType.StoredProcedure);
            return res.ToList();
        }
        public async Task<List<MyManualPaRaw>> GetMyManualAsync(int userId)
        {
            string query = @"Internal_Pa_GetMyManualPa";
            var parameters = new DynamicParameters(
                 new
                 {
                     userId
                 }
              );
            var res = await Context.Database.GetDbConnection()
             .QueryAsync<MyManualPaRaw>(query, parameters,
                 commandType: CommandType.StoredProcedure);
            return res.ToList();
        }
    }
}
