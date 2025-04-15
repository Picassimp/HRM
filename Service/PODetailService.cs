using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Enums;
using InternalPortal.ApplicationCore.Helpers;
using InternalPortal.ApplicationCore.Interfaces;
using InternalPortal.ApplicationCore.Interfaces.Business;
using InternalPortal.ApplicationCore.Models;
using InternalPortal.ApplicationCore.Models.PurchaseOrder.PODetail;

namespace InternalPortal.Infrastructure.Services.Business
{
    public class PODetailService : IPODetailService
    {
        private readonly IUnitOfWork _unitOfWork;
        public PODetailService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<CombineResponseModel<Podetail>> DeleteAsync(int id)
        {
            var res = new CombineResponseModel<Podetail>();
            var podetail = await _unitOfWork.PODetailRepository.GetByIdAsync(id);
            if (podetail == null)
            {
                res.ErrorMessage = "Không tồn tại chi tiết đơn hàng";
                return res;
            }
            if (podetail.PurchaseOrder.Status == (int)EPurchaseOrderStatus.Purchased || podetail.PurchaseOrder.Status == (int)EPurchaseOrderStatus.LackReceived 
                || podetail.PurchaseOrder.Status == (int)EPurchaseOrderStatus.FullReceived || podetail.PurchaseOrder.IsClose)
            {
                res.ErrorMessage = "Không thể xóa chi tiết của đơn hàng có trạng thái: " + CommonHelper.GetDescription((EPurchaseOrderStatus)podetail.PurchaseOrder.Status) + " hoặc đã đóng";
                return res;
            }
            var isHasPrApprove = podetail.PoprlineItems.Any(t => t.PorequestLineItem.PurchaseRequest.ReviewStatus == (int)EPurchaseRequestStatus.AccountantApproved
            || t.PorequestLineItem.PurchaseRequest.ReviewStatus == (int)EPurchaseRequestStatus.DirectorApproved);
            if (isHasPrApprove && !podetail.PurchaseOrder.IsCompensationPo)
            {
                res.ErrorMessage = "Đã có yêu cầu được duyệt,không thể xóa";
                return res;
            }
            if (podetail.PurchaseOrder.IsCompensationPo && (podetail.PurchaseOrder.Status == (int)EPurchaseOrderStatus.AccountAccept
                || podetail.PurchaseOrder.Status == (int)EPurchaseOrderStatus.DirectorAccept))
            {
                res.ErrorMessage = "Đơn mua hàng đã được duyệt,không thể xóa";
                return res;
            }
            if (podetail.IsFromRequest)
            {
                var poprLineItems = await _unitOfWork.POPRLineItemRepository.GetByPurchaseOrderDetailIdAsync(id);
                if (poprLineItems.Count > 0)
                {
                    await _unitOfWork.POPRLineItemRepository.DeleteRangeAsync(poprLineItems);
                }
            }
            res.Status = true;
            res.Data = podetail;
            return res;
        }

        public async Task<CombineResponseModel<Podetail>> ReceiveAsync(int id, int quantity)
        {
            var res = new CombineResponseModel<Podetail>(); 
            var podetail = await _unitOfWork.PODetailRepository.GetByIdAsync(id);
            if( podetail == null )
            {
                res.ErrorMessage = "Không tồn tại chi tiết đơn hàng";
                return res;
            }
            if(podetail.PurchaseOrder.Status != (int)EPurchaseOrderStatus.Purchased || podetail.PurchaseOrder.IsClose)
            {
                res.ErrorMessage = "Không thể nhận hàng cho đơn hàng có trạng thái: " + CommonHelper.GetDescription((EPurchaseOrderStatus)podetail.PurchaseOrder.Status) + " hoặc đã đóng";
                return res;
            }
            if(podetail.Quantity < quantity)
            {
                res.ErrorMessage = "Số lượng nhận không lớn hơn số lượng yêu cầu";
                return res;
            }
            podetail.QuantityReceived = quantity;
            podetail.UpdateDate = DateTime.UtcNow.UTCToIct();
            res.Status = true;
            res.Data = podetail;
            return res;
        }

        public async Task<CombineResponseModel<Podetail>> UpdateAsync(int id, PODetailRequest request)
        {
            var res = new CombineResponseModel<Podetail>(); 
            var podetail = await _unitOfWork.PODetailRepository.GetByIdAsync(id);
            if(podetail == null) 
            {
                res.ErrorMessage = "Không tồn tại chi tiết đơn hàng";
                return res;
            }
            if(podetail.PurchaseOrder.Status == (int)EPurchaseOrderStatus.LackReceived 
                || podetail.PurchaseOrder.Status == (int)EPurchaseOrderStatus.FullReceived 
                || podetail.PurchaseOrder.Status == (int)EPurchaseOrderStatus.Purchased || podetail.PurchaseOrder.IsClose)
            {
                res.ErrorMessage = "Không thể cập nhật chi tiết của đơn hàng có trạng thái: " + CommonHelper.GetDescription((EPurchaseOrderStatus)podetail.PurchaseOrder.Status) + " hoặc đã đóng"; 
                return res;
            }
            var isHasPrApprove = podetail.PoprlineItems.Any(t => t.PorequestLineItem.PurchaseRequest.ReviewStatus == (int)EPurchaseRequestStatus.AccountantApproved
            || t.PorequestLineItem.PurchaseRequest.ReviewStatus == (int)EPurchaseRequestStatus.DirectorApproved);
            if (!podetail.PurchaseOrder.IsCompensationPo)
            {
                if (isHasPrApprove)
                {
                    res.ErrorMessage = "Đã có yêu cầu được duyệt,không thể cập nhật";
                    return res;
                }
            }
            if(podetail.PurchaseOrder.IsCompensationPo && (podetail.PurchaseOrder.Status == (int)EPurchaseOrderStatus.AccountAccept 
                || podetail.PurchaseOrder.Status == (int)EPurchaseOrderStatus.DirectorAccept))
            {
                res.ErrorMessage = "Đơn mua hàng đã được duyệt,không thể cập nhật";
                return res;
            }
            podetail.Price = request.Price ?? 0;
            podetail.ShoppingUrl = request.ShoppingUrl ?? "";
            podetail.Vat = request.Vat ?? 0;
            podetail.VatPrice = request.VatPrice;
            podetail.UpdateDate = DateTime.UtcNow.UTCToIct();
            res.Status = true;
            res.Data = podetail;
            return res;
        }
    }
}
