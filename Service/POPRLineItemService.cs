using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Enums;
using InternalPortal.ApplicationCore.Helpers;
using InternalPortal.ApplicationCore.Interfaces;
using InternalPortal.ApplicationCore.Interfaces.Business;
using InternalPortal.ApplicationCore.Models;

namespace InternalPortal.Infrastructure.Services.Business
{
    public class POPRLineItemService : IPOPRLineItemService
    {
        private IUnitOfWork _unitOfWork;
        public POPRLineItemService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }
        public async Task<CombineResponseModel<PoprlineItem>> DeleteAsync(int id)
        {
            var res = new CombineResponseModel<PoprlineItem>();
            var lineItem = await _unitOfWork.POPRLineItemRepository.GetByIdAsync(id);
            if(lineItem == null)
            {
                res.ErrorMessage = "Không tồn tại chi tiết yêu cầu";
                return res;
            }
            var prStatus = lineItem.PorequestLineItem.PurchaseRequest.ReviewStatus;
            var isCompensationPo = lineItem.PurchaseOrderDetail.PurchaseOrder.IsCompensationPo;
            var poStatus = lineItem.PurchaseOrderDetail.PurchaseOrder.Status;
            if (!isCompensationPo && (prStatus == (int)EPurchaseRequestStatus.AccountantApproved || prStatus == (int)EPurchaseRequestStatus.DirectorApproved))
            {
                res.ErrorMessage = "Yêu cầu đã được duyệt,không thể xóa";
                return res;
            }
            if(isCompensationPo &&(poStatus == (int)EPurchaseOrderStatus.AccountAccept || poStatus == (int)EPurchaseOrderStatus.DirectorAccept))
            {
                res.ErrorMessage = "Đơn mua hàng đã được duyệt,không thể xóa";
                return res;
            }
            var podetail = await _unitOfWork.PODetailRepository.GetByIdAsync(lineItem.PurchaseOrderDetailId);
            if(podetail.PoprlineItems.Count == 1)
            {
                await _unitOfWork.POPRLineItemRepository.DeleteAsync(lineItem);
                await _unitOfWork.PODetailRepository.DeleteAsync(podetail);
            }
            else
            {
                await _unitOfWork.POPRLineItemRepository.DeleteAsync(lineItem);
                podetail.Quantity = podetail.PoprlineItems.Where(y=>y.Id != id).Sum(t => t.Quantity);
                await _unitOfWork.PODetailRepository.UpdateAsync(podetail);
            }
            await _unitOfWork.SaveChangesAsync();
            res.Status = true;
            res.Data = lineItem;
            return res;
        }
        public async Task<CombineResponseModel<PoprlineItem>> ReceiveAsync(int id, int quantity)
        {
            var res = new CombineResponseModel<PoprlineItem>();
            var lineItem = await _unitOfWork.POPRLineItemRepository.GetByIdAsync(id);
            if(lineItem == null)
            {
                res.ErrorMessage = "Không tồn tại chi tiết yêu cầu";
                return res;
            }
            if(lineItem.PurchaseOrderDetail.PurchaseOrder.Status != (int)EPurchaseOrderStatus.Purchased 
                && lineItem.PurchaseOrderDetail.PurchaseOrder.Status == (int)EPurchaseOrderStatus.FullReceived)
            {
                res.ErrorMessage = "Không thể nhận hàng cho đơn hàng ở trạng thái " + CommonHelper.GetDescription((EPurchaseOrderStatus)lineItem.PurchaseOrderDetail.PurchaseOrder.Status);
                return res;
            }
            if(lineItem.Quantity < quantity)
            {
                res.ErrorMessage = "Số lượng nhận không lớn hơn số lượng yêu cầu";
                return res;
            }
            if(lineItem.Quantity == lineItem.QuantityReceived)
            {
                res.ErrorMessage = "Đã nhận đủ hàng";
                return res;
            }
            lineItem.QuantityReceived = quantity;
            lineItem.UpdateDate = DateTime.UtcNow.UTCToIct();
            res.Status = true;
            res.Data = lineItem;
            return res;
        }

        public async Task<CombineResponseModel<PoprlineItem>> UpdateAsync(int id, int quantity)
        {
            var res = new CombineResponseModel<PoprlineItem>();
            var lineItem = await _unitOfWork.POPRLineItemRepository.GetByIdAsync(id);
            if (lineItem == null)
            {
                res.ErrorMessage = "Không tồn tại chi tiết yêu cầu";
                return res;
            }
            var prStatus = lineItem.PorequestLineItem.PurchaseRequest.ReviewStatus;
            var isCompensationPo = lineItem.PurchaseOrderDetail.PurchaseOrder.IsCompensationPo;
            var poStatus = lineItem.PurchaseOrderDetail.PurchaseOrder.Status;
            if (!isCompensationPo && (prStatus == (int)EPurchaseRequestStatus.AccountantApproved || prStatus == (int)EPurchaseRequestStatus.DirectorApproved))
            {
                res.ErrorMessage = "Yêu cầu đã được duyệt,không thể cập nhật";
                return res;
            }
            if (isCompensationPo && (poStatus == (int)EPurchaseOrderStatus.AccountAccept || poStatus == (int)EPurchaseOrderStatus.DirectorAccept))
            {
                res.ErrorMessage = "Đơn mua hàng đã được duyệt,không thể cập nhật";
                return res;
            }
            lineItem.Quantity = quantity;
            lineItem.UpdateDate = DateTime.UtcNow.UTCToIct();
            var podetail = await _unitOfWork.PODetailRepository.GetByIdAsync(lineItem.PurchaseOrderDetailId);
            podetail.Quantity = podetail.PoprlineItems.Sum(t => t.Quantity);
            podetail.VatPrice =  Math.Round((decimal)(podetail.VatPrice != 0 ? (quantity * podetail.Price * podetail.Vat / 100) : 0));//update lại tiền thuế khi thay đổi số lượng
            podetail.UpdateDate = DateTime.UtcNow.UTCToIct();
            await _unitOfWork.POPRLineItemRepository.UpdateAsync(lineItem);
            await _unitOfWork.PODetailRepository.UpdateAsync(podetail);
            await _unitOfWork.SaveChangesAsync();
            res.Status = true;
            res.Data= lineItem;
            return res;
        }
        public async Task<CombineResponseModel<PoprlineItem>> LackReceiveAsync(int id)
        {
            var res = new CombineResponseModel<PoprlineItem>();
            var lineItem = await _unitOfWork.POPRLineItemRepository.GetByIdAsync(id);
            if (lineItem == null)
            {
                res.ErrorMessage = "Không tồn tại chi tiết yêu cầu";
                return res;
            }
            if ((bool)lineItem.IsReceived)
            {
                res.ErrorMessage = "Đã nhận hàng trước đó";
                return res;
            }
            if (lineItem.PurchaseOrderDetail.PurchaseOrder.Status != (int)EPurchaseOrderStatus.Purchased
                && lineItem.PurchaseOrderDetail.PurchaseOrder.Status == (int)EPurchaseOrderStatus.FullReceived)
            {
                res.ErrorMessage = "Không thể nhận hàng cho đơn hàng ở trạng thái " + CommonHelper.GetDescription((EPurchaseOrderStatus)lineItem.PurchaseOrderDetail.PurchaseOrder.Status);
                return res;
            }
            var returnPrice = Math.Round((decimal)(lineItem.QuantityReceived * lineItem.PurchaseOrderDetail.Price * lineItem.PurchaseOrderDetail.Vat / 100));//cập nhật lại tiền thuế khi nhận thiếu
            lineItem.IsReceived = true;
            lineItem.PurchaseOrderDetail.PurchaseOrder.Status = (int)EPurchaseOrderStatus.LackReceived;
            lineItem.PurchaseOrderDetail.VatPrice = Math.Round(returnPrice);
            lineItem.UpdateDate = DateTime.UtcNow.UTCToIct();
            res.Status = true;
            res.Data = lineItem;
            return res;
        }
    }
}
