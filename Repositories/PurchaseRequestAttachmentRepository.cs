using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Interfaces.Repositories;

namespace InternalPortal.Infrastructure.Persistence.Repositories
{
    public class PurchaseRequestAttachmentRepository : EfRepository<PurchaseRequestAttachment>, IPurchaseRequestAttachmentRepository
    {
        public PurchaseRequestAttachmentRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}