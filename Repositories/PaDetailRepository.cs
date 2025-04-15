using Dapper;
using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Interfaces.Repositories;
using InternalPortal.ApplicationCore.Models.PaDetail;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace InternalPortal.Infrastructure.Persistence.Repositories
{
    public class PaDetailRepository : EfRepository<PaDetail>, IPaDetailRepository
    {
        public PaDetailRepository(ApplicationDbContext context) : base(context)
        {
        }
        public async Task<List<QuestionGroupMarkModel>> GetQuestionGroupMarksAsync(int paId, int paHistoryId)
        {
            string query = @"Internal_PaDetail_GetQuestionGroupMarks";

            var parameters = new DynamicParameters(new
            {
                paId = paId,
                paHistoryId = paHistoryId,
            });

            var res = await Context.Database.GetDbConnection().QueryAsync<QuestionGroupMarkModel>(query, parameters, commandType: CommandType.StoredProcedure);
            return res.ToList();
        }
        public async Task<List<PaFormQuestionsModel>> GetPaFormQuestionsAsync(int paId, int paHistoryId)
        {
            string query = @"Internal_PaDetail_GetPaFormQuestions";

            var parameters = new DynamicParameters(new
            {
                paId = paId,
                paHistoryId = paHistoryId,
            });

            var res = await Context.Database.GetDbConnection().QueryAsync<PaFormQuestionsModel>(query, parameters, commandType: CommandType.StoredProcedure);
            return res.ToList();
        }
    }
}
