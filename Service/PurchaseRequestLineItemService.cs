using InternalPortal.ApplicationCore.Helpers;
using InternalPortal.ApplicationCore.Interfaces;
using InternalPortal.ApplicationCore.Interfaces.Business;
using InternalPortal.ApplicationCore.Models.PurchaseOrder.PurchaseRequest;

namespace InternalPortal.Infrastructure.Services.Business
{
    public class PurchaseRequestLineItemService : IPurchaseRequestLineItemService
    {
        private readonly IUnitOfWork _unitOfWork;
        public PurchaseRequestLineItemService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }
        public async Task<List<PurchaseRequestLineItemResponse>> GetPurchaseRequestLineItemAsync(int purchaseRequestId, int purchaseOrderId, int exportBillId)
        {
            var responseRaw = await _unitOfWork.PurchaseRequestLineItemRepository.GetPurchaseRequestLineItemAsync(purchaseRequestId, purchaseOrderId, exportBillId);
            var response = responseRaw.Count > 0 ? responseRaw.Select(t =>
            {
                var pos = !string.IsNullOrEmpty(t.FromPOs) ? t.FromPOs.Split(",").Distinct().ToList() : new List<string>();
                var item = new PurchaseRequestLineItemResponse()
                {
                    Id = t.Id,
                    CategoryName = t.CategoryName,
                    Name = t.Name,
                    Detail = !string.IsNullOrEmpty(t.Detail) ? t.Detail.Split(",").ToList() : new List<string>(),
                    RequestQty = t.RequestQty,
                    Description = t.Description,
                    FromPOs = !string.IsNullOrEmpty(t.FromPOs) ? t.FromPOs.Split(",").Distinct().Select(t => new IdDisplay
                    {
                        Id = int.Parse(t),
                        StringId = CommonHelper.ToIdDisplayString("PO", int.Parse(t))
                    }).ToList() : new List<IdDisplay>(),
                    FromEBs = !string.IsNullOrEmpty(t.FromEBs) ? t.FromEBs.Split(",").Distinct().Select(t => new IdDisplay
                    {
                        Id = int.Parse(t),
                        StringId = CommonHelper.ToIdDisplayString("XK", int.Parse(t))
                    }).ToList() : new List<IdDisplay>(),
                    RemainQty = t.RequestQty - t.QtyFromPO - t.QtyFromEB,
                    IsinPO = t.PurchaseOrderId != 0 && t.PurchaseOrderId == purchaseOrderId ? true : false,
                    IsinEB = t.ExportBillId != 0 && t.ExportBillId == exportBillId ? true : false
                };
                return item;
            }).ToList() : new List<PurchaseRequestLineItemResponse>();
            return response;
        }
    }
}
