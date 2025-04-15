using Dapper;
using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Interfaces.Repositories;
using InternalPortal.ApplicationCore.Models.ProjectManagement;
using InternalPortal.ApplicationCore.Models.User;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace InternalPortal.Infrastructure.Persistence.Repositories
{
    public class UserInternalRepository : EfRepository<UserInternal>, IUserInternalRepository
    {
        public UserInternalRepository(ApplicationDbContext context) : base(context)
        {
        }

        /// <summary>
        /// Note: hàm này có sử dụng ở api me
        /// </summary>
        /// <param name="objectId"></param>
        /// <returns></returns>
        public async Task<UserInternal?> GetByObjectIdAsync(string objectId)
        {
            return await DbSet.FirstOrDefaultAsync(o => o.ObjectId == objectId && !o.IsDeleted);
        }

        public async Task<List<InternalUserDetailModel>> GetAllManagerUsersAsync()
        {
            string query = @"Internal_User_GetAllManagers";
            var res = await Context.Database.GetDbConnection().QueryAsync<InternalUserDetailModel>(query, commandType: CommandType.StoredProcedure);
            return res.ToList();
        }

        /// <summary>
        /// Validate ObjectId Only
        /// </summary>
        /// <param name="objectId"></param>
        /// <returns></returns>
        public async Task<UserDtoModel?> GetUserDtoByObjectIdAsync(string objectId)
        {
            var user = await DbSet.FirstOrDefaultAsync(o => o.ObjectId == objectId && !o.IsDeleted);
            if (user == null)
                return null;
            return new UserDtoModel()
            {
                Id = user.Id,
                FullName = user.FullName,
                Name = user.Name,
                Email = user.Email,
                ObjectId = user.ObjectId,
                RemainDayOffLastYear = user.RemainDayOffLastYear,
                LevelId = user.LevelId,
                LevelName = user.Level?.Name,
                GroupUserId = user.GroupUserId,
                GroupUserName = user.GroupUser?.Name
            };
        }

        /// <summary>
        /// Lấy danh sách những người đăng ký đơn làm thêm theo manager
        /// </summary>
        /// <param name="managerId"></param>
        /// <returns></returns>
        public async Task<List<InternalUserDetailModel>> GetUsersSubmitOverTimeApplicationManagerAsync(int managerId)
        {
            string query = @"Internal_User_GetUsersSubmitOverTimeApplicationManager";

            var parameters = new DynamicParameters(
              new
              {
                  managerId
              });

            var response = await Context.Database.GetDbConnection().QueryAsync<InternalUserDetailModel>(query, parameters, commandType: CommandType.StoredProcedure);

            return response.ToList();
        }

        /// <summary>
        /// Lấy danh sách những người đăng ký đơn công tác theo manager
        /// </summary>
        /// <param name="managerId"></param>
        /// <returns></returns>
        public async Task<List<InternalUserDetailModel>> GetUsersSubmitOnSiteApplicationManagerAsync(int managerId)
        {
            string query = @"Internal_User_GetUsersSubmitOnSiteApplicationManager";

            var parameters = new DynamicParameters(
              new
              {
                  managerId
              });

            var response = await Context.Database.GetDbConnection().QueryAsync<InternalUserDetailModel>(query, parameters, commandType: CommandType.StoredProcedure);

            return response.ToList();
        }

        /// <summary>
        /// Lấy danh sách những người đăng ký đơn làm việc ở nhà theo manager
        /// </summary>
        /// <param name="managerId"></param>
        /// <returns></returns>
        public async Task<List<InternalUserDetailModel>> GetUsersSubmitWorkFromHomeApplicationManagerAsync(int managerId)
        {
            string query = @"Internal_User_GetUsersSubmitWorkFromHomeApplicationManager";

            var parameters = new DynamicParameters(
              new
              {
                  managerId
              });

            var response = await Context.Database.GetDbConnection().QueryAsync<InternalUserDetailModel>(query, parameters, commandType: CommandType.StoredProcedure);

            return response.ToList();
        }

        public async Task<List<UserInternal>> GetListUserAsync()
        {
            var listUser = await DbSet.Where(o => !o.HasLeft && !o.IsDeleted).ToListAsync();
            return listUser;
        }

        public async Task<List<ProjectDataModel>> GetProjectMemberFilterDataAsync()
        {
            var projectMemberFilterData = await DbSet.Where(pm => pm.FullName != null && !pm.IsDeleted && !pm.HasLeft).Select(u =>
                new ProjectDataModel
                {
                    Id = u.Id,
                    Name = u.FullName,
                    Email = u.Email
                }).ToListAsync();
            return projectMemberFilterData;
        }

        public async Task<List<InternalUserDetailModel>> GetAllManagers()
        {
            var query = "Internal_User_GetAllManagers";
            var parameters = new DynamicParameters();
            var res = await Context.Database.GetDbConnection().QueryAsync<InternalUserDetailModel>(query, parameters, commandType: CommandType.StoredProcedure);
            return res.ToList();
        }

        public async Task<List<InternalUserDetailModel>> GetUsersAsync()
        {
            string query = "Internal_User_GetAll";

            var res = await Context.Database.GetDbConnection().QueryAsync<InternalUserDetailModel>(query, commandType: CommandType.StoredProcedure);
            return res.ToList();
        }

        public async Task<List<InternalUserDetailModel>> GetUsersInLeaveApplicationByManagerIdAsync(int managerId)
        {
            string query = "Internal_User_GetByManagerId";

            var parameters = new DynamicParameters(
                new
                {
                    @managerId = managerId
                }
            );

            var res = await Context.Database.GetDbConnection().QueryAsync<InternalUserDetailModel>(query, parameters, commandType: CommandType.StoredProcedure);
            return res.ToList();
        }

        public async Task<UserInternal?> GetByEmailAsync(string email)
        {
            return await DbSet.FirstOrDefaultAsync(o => o.Email == email && !o.IsDeleted && !o.HasLeft);
        }

        public async Task<List<UserInternalDayOffResponse>> GetAllWithPagingAsync(int userId, UserDayOffCriteriaModel requestModel)
        {
            string query = "Internal_GetDayOffPaging";

            var parameters = new DynamicParameters(
                new
                {
                    @userId = userId,
                    @countSkip = requestModel.PageIndex,
                    @pageSize = requestModel.PageSize
                }
            );

            var res = await Context.Database.GetDbConnection().QueryAsync<UserInternalDayOffResponse>(query, parameters, commandType: CommandType.StoredProcedure);

            return res.ToList();
        }
    }
}
