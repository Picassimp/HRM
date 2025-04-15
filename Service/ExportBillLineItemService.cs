using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Helpers;
using InternalPortal.ApplicationCore.Interfaces;
using InternalPortal.ApplicationCore.Interfaces.Business;
using InternalPortal.ApplicationCore.Models;

namespace InternalPortal.Infrastructure.Services.Business
{
    public class ExportBillLineItemService : IExportBillLineItemService
    {
        private readonly IUnitOfWork _unitOfWork;
        public ExportBillLineItemService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }
        public async Task<CombineResponseModel<ExportBillLineItem>> DeleteAsync(int id)
        {
            var res = new CombineResponseModel<ExportBillLineItem>();
            var exportBillLineItem = await _unitOfWork.ExportBillLineItemRepository.GetByIdAsync(id);
            if(exportBillLineItem == null)
            {
                res.ErrorMessage = "Không tồn tại chi tiết phiếu xuất";
                return res;
            }
            var exportBillDetail = await _unitOfWork.ExportBillDetailRepository.GetByIdAsync(exportBillLineItem.ExportBillDetailId);
            var isExport = exportBillDetail.ExportBill.IsExport != null ? exportBillDetail.ExportBill.IsExport : false;
            if ((bool)isExport)
            {
                res.ErrorMessage = "Đã xuất kho,không thể xóa";
                return res;
            }
            if (exportBillDetail.ExportBillLineItems.Count == 1)
            {
                await _unitOfWork.ExportBillLineItemRepository.DeleteAsync(exportBillLineItem);
                await _unitOfWork.ExportBillDetailRepository.DeleteAsync(exportBillDetail);
            }
            else
            {
                await _unitOfWork.ExportBillLineItemRepository.DeleteAsync(exportBillLineItem);
                exportBillDetail.Quantity = exportBillDetail.ExportBillLineItems.Where(t => t.Id != id).Sum(t => t.Quantity);
                await _unitOfWork.ExportBillDetailRepository.UpdateAsync(exportBillDetail);
            }
            await _unitOfWork.SaveChangesAsync();
            res.Status = true;
            res.Data = exportBillLineItem;
            return res;
        }

        public async Task<CombineResponseModel<ExportBillLineItem>> UpdateAsync(int id, int quantity)
        {
            var res = new CombineResponseModel<ExportBillLineItem>();
            var exportBillLineItem = await _unitOfWork.ExportBillLineItemRepository.GetByIdAsync(id);
            if(exportBillLineItem == null)
            {
                res.ErrorMessage = "Không tồn tại chi tiết phiếu xuất";
                return res; 
            }
            exportBillLineItem.Quantity = quantity;
            exportBillLineItem.UpdateDate = DateTime.UtcNow.UTCToIct();
            var exportBillDetail = await _unitOfWork.ExportBillDetailRepository.GetByIdAsync(exportBillLineItem.ExportBillDetailId);
            var isExport = exportBillDetail.ExportBill.IsExport != null ? exportBillDetail.ExportBill.IsExport : false;
            if ((bool)isExport)
            {
                res.ErrorMessage = "Đã xuất kho,không thể cập nhật";
                return res;
            }
            exportBillDetail.Quantity = exportBillDetail.ExportBillLineItems.Sum(t => t.Quantity);
            exportBillDetail.UpdateDate = DateTime.UtcNow.UTCToIct();
            await _unitOfWork.ExportBillLineItemRepository.UpdateAsync(exportBillLineItem);
            await _unitOfWork.ExportBillDetailRepository.UpdateAsync(exportBillDetail);
            await _unitOfWork.SaveChangesAsync();
            res.Status = true;
            res.Data = exportBillLineItem;
            return res;
        }
    }
}
