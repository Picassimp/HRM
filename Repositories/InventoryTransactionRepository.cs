using Dapper;
using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Interfaces.Repositories;
using InternalPortal.ApplicationCore.Models.Inventory;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InternalPortal.Infrastructure.Persistence.Repositories
{
    public class InventoryTransactionRepository : EfRepository<InventoryTransaction>, IInventoryTransactionRepository
    {
        public InventoryTransactionRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<List<InventoryTransactionResponse>> GetAllByMonthYearAsync(int month, int year)
        {
            string query = @"Internal_GetAllTransactionByMonthYear";

            var parameters = new DynamicParameters(
                new
                {
                    @month = month,
                    @year = year
                }
            );
            var res = await Context.Database.GetDbConnection()
                .QueryAsync<InventoryTransactionResponse>(query, parameters,
                    commandType: CommandType.Text);
            return res.ToList();
        }

        public async Task<List<TransactionResponse>> GetByUserIdAsync(int userId, int month, int year)
        {
            string query = @"Internal_GetTransactionByUserId";

            var parameters = new DynamicParameters(
                new
                {
                    @userId = userId,
                    @month = month,
                    @year = year
                }
            );
            var res = await Context.Database.GetDbConnection()
                .QueryAsync<TransactionResponse>(query, parameters,
                    commandType: CommandType.StoredProcedure);
            return res.ToList();
        }
    }
}
