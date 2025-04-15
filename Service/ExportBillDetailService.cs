using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Enums;
using InternalPortal.ApplicationCore.Helpers;
using InternalPortal.ApplicationCore.Interfaces;
using InternalPortal.ApplicationCore.Interfaces.Business;
using InternalPortal.ApplicationCore.Models;

namespace InternalPortal.Infrastructure.Services.Business
{
    public class ExportBillDetailService : IExportBillDetailService
    {
        private readonly IUnitOfWork _unitOfWork;
        public ExportBillDetailService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }
        public async Task<CombineResponseModel<ExportBillDetail>> DeleteAsync(int id)
        {
            var res = new CombineResponseModel<ExportBillDetail>();
            var exportBillDetail = await _unitOfWork.ExportBillDetailRepository.GetByIdAsync(id);
            if (exportBillDetail == null)
            {
                res.ErrorMessage = "Không tồn tại chi tiết đơn hàng";
                return res;
            }
            var isExport = exportBillDetail.ExportBill.IsExport != null ? exportBillDetail.ExportBill.IsExport : false;
            if ((bool)isExport)
            {
                res.ErrorMessage = "Đã xuất kho,không thể xóa";
                return res;
            }
            if (exportBillDetail.ExportBill.Status == (int)EExportBillStatus.Accept || exportBillDetail.ExportBill.Status == (int)EExportBillStatus.Reject
                || exportBillDetail.ExportBill.Status == (int)EExportBillStatus.Cancel)
            {
                res.ErrorMessage = "Không thể xóa chi tiết của đơn hàng có trạng thái: " + CommonHelper.GetDescription((EPurchaseOrderStatus)exportBillDetail.ExportBill.Status);
                return res;
            }
            if (exportBillDetail.ExportBillLineItems.Count > 0)
            {
                await _unitOfWork.ExportBillLineItemRepository.DeleteRangeAsync(exportBillDetail.ExportBillLineItems.ToList());
            }
            res.Status = true;
            res.Data = exportBillDetail;
            return res;
        }
    }
}
