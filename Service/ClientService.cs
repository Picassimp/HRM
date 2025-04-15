using InternalPortal.ApplicationCore.Interfaces;
using InternalPortal.ApplicationCore.Interfaces.Business;
using InternalPortal.ApplicationCore.Models.ProjectManagement;

namespace InternalPortal.Infrastructure.Services.Business
{
    public class ClientService : IClientService
    {
        private readonly IUnitOfWork _unitOfWork;
        public ClientService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<List<CommonUserProjectsResponse>> ManagerGetDataProjectsAsync(int userId)
        {
            var responseRaw = await _unitOfWork.ClientRepository.ManagerGetDataProjectsAsync(userId);
            var response = responseRaw.GroupBy(t => t.ClientName).Select(g => new CommonUserProjectsResponse
            {
                ClientName = g.Key,
                Projects = g.ToList().Select(p => new FieldProjects
                {
                    Id = p.Id,
                    Name = p.ProjectName,
                    Integration = p.Integration
                }).ToList()
            }).ToList();
            return response;
        }
    }
}
