using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Enums;
using InternalPortal.ApplicationCore.Helpers;
using InternalPortal.ApplicationCore.Interfaces;
using InternalPortal.ApplicationCore.Interfaces.Business;
using InternalPortal.ApplicationCore.Models;
using InternalPortal.ApplicationCore.Models.PaymentPlan;

namespace InternalPortal.Infrastructure.Services.Business
{
    public class PaymentPlanService : IPaymentPlanService
    {
        private readonly IUnitOfWork _unitOfWork;
        public PaymentPlanService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }
        #region Private Method
        private int GetIdFromStringId(string s)
        {
            string numberString = new string(s.Where(char.IsDigit).ToArray());

            // Nếu chuỗi số không rỗng, chuyển đổi thành số nguyên, nếu không thì trả về 0
            return string.IsNullOrEmpty(numberString) ? 0 : int.Parse(numberString);
        }
        #endregion
        public async Task<CombineResponseModel<PaymentPlan>> CreateAsync(int id, PaymentPlanRequest request)
        {
            var res = new CombineResponseModel<PaymentPlan>();
            if (!request.PayDate.HasValue && request.Status == true) //nếu status là đã thanh toán nhưng chưa có thông tin ngày thanh toán
            {
                res.ErrorMessage = "Vui lòng chọn ngày thanh toán";
                return res;
            }
            var purchaseOrder = await _unitOfWork.PurchaseOrderRepository.GetByIdAsync(id);
            if (purchaseOrder == null)
            {
                res.ErrorMessage = "Không tồn tại đơn đặt hàng";
                return res;
            }
            var totalPrice = await _unitOfWork.PurchaseOrderRepository.GetPurchaseOrderTotalPriceAsync(purchaseOrder.Id);
            var sumPaymentPlan = (int)Math.Floor(purchaseOrder.PaymentPlans.Sum(t=>t.PaymentAmount));
            if(totalPrice.Count >0 && (int)Math.Floor(totalPrice.FirstOrDefault().TotalPriceWithVat) == sumPaymentPlan)
            {
                res.ErrorMessage = "Thanh toán đã đủ,không thể thanh toán thêm";
                return res;
            }
            if (request.Status)
            {
                var lineItem = await _unitOfWork.POPRLineItemRepository.GetByPurchareOrderIdAsync(purchaseOrder.Id);
                var isHasPrNotApproveByDirector = lineItem.Count > 0 ? 
                    lineItem.Any(t => t.PorequestLineItem.PurchaseRequest.ReviewStatus != (int)EPurchaseRequestStatus.DirectorApproved) ? true : false 
                    : false;
                if (isHasPrNotApproveByDirector)
                {
                    res.ErrorMessage = "Có yêu cầu chưa được giám đốc duyệt";
                    return res;
                }
            }
            var paymentPlan = new PaymentPlan()
            {
                PurchaseOrderId = id,
                PaymentAmount = request.Price,
                PayDate = request.PayDate,
                PayType = request.PaymentType,
                PaymentStatus = request.Status,
                Note = request.Note,
                CreateDate = DateTime.UtcNow.UTCToIct()
            };
            res.Status = true;
            res.Data = paymentPlan;
            return res;
        }

        public async Task<CombineResponseModel<PaymentPlan>> DeleteAsync(int id)
        {
            var res = new CombineResponseModel<PaymentPlan>();
            var paymentPlan = await _unitOfWork.PaymentPlanRepository.GetByIdAsync(id);
            if (paymentPlan == null)
            {
                res.ErrorMessage = "Không tồn tại kế hoạch thanh toán";
                return res;
            }
            if(paymentPlan.PaymentStatus == true)
            {
                res.ErrorMessage = "Đợt này đã thanh toán,không thể xóa";
                return res;
            }
            res.Status = true;
            res.Data = paymentPlan;
            return res;
        }

        public async Task<CombineResponseModel<PaymentPlan>> UpdateAsync(int id, PaymentPlanRequest request)
        {
            var res = new CombineResponseModel<PaymentPlan>();
            if (!request.PayDate.HasValue && request.Status == true) //nếu status là đã thanh toán nhưng chưa có thông tin ngày thanh toán
            {
                res.ErrorMessage = "Vui lòng chọn ngày thanh toán";
                return res;
            }
            var paymentPlan = await _unitOfWork.PaymentPlanRepository.GetByIdAsync(id);
            if (paymentPlan == null)
            {
                res.ErrorMessage = "Không tồn tại kế hoạch thanh toán";
                return res;
            }
            if (paymentPlan.PaymentStatus == true)
            {
                res.ErrorMessage = "Đợt này đã thanh toán,không thể cập nhật";
                return res;
            }
            var purchaseOrder = await _unitOfWork.PurchaseOrderRepository.GetByIdAsync(paymentPlan.PurchaseOrder.Id);
            var totalPrice = await _unitOfWork.PurchaseOrderRepository.GetPurchaseOrderTotalPriceAsync(paymentPlan.PurchaseOrder.Id);//lấy tổng tiền của po
            var paymentPlans = purchaseOrder.PaymentPlans.ToList();
            var sumpaymentPlans = (int)Math.Floor(paymentPlans.Sum(t => t.PaymentAmount));//lấy tổng các đợt thanh toán
            if (totalPrice.Count > 0 && sumpaymentPlans - (int)Math.Floor(paymentPlan.PaymentAmount) + request.Price > (int)Math.Floor(totalPrice.FirstOrDefault().TotalPriceWithVat))
            {
                res.ErrorMessage = "Tổng tiền tất cả các đợt thanh toán không được lớn hơn tổng tiền của đơn hàng";
                return res;
            }
            paymentPlan.PaymentAmount = request.Price;
            paymentPlan.PayDate = request.PayDate;
            paymentPlan.PayType = request.PaymentType;
            paymentPlan.PaymentStatus = request.Status;
            paymentPlan.Note = request.Note;
            paymentPlan.UpdateDate = DateTime.UtcNow.UTCToIct();
            res.Status = true;
            res.Data = paymentPlan;
            return res;
        }
        public async Task<PagingResponseModel<PaymentPlanPagingResponse>> GetAllWithPagingAsync(PaymentPlanPagingModel model)
        {
            if (!string.IsNullOrEmpty(model.PurchaseOrderId))
            {
                model.PurchaseOrderId = GetIdFromStringId(model.PurchaseOrderId).ToString();
            }
            var responseRaw = await _unitOfWork.PaymentPlanRepository.GetAllWithPagingAsync(model);
            var response = responseRaw.Count > 0 ? responseRaw.Select(t =>
            {
                var item = new PaymentPlanPagingResponse()
                {
                    PaymentId = t.PaymentId,
                    RequestName = t.RequestName,
                    PurchaseRequestId = t.PurchaseRequestId,
                    PurchaseRequestStringId = CommonHelper.ToIdDisplayString("PR",t.PurchaseRequestId),
                    DepartmentName = t.DepartmentName,
                    PurchaseOrderId = t.PurchaseOrderId,
                    PurchaseOrderStringId = CommonHelper.ToIdDisplayString("PO",t.PurchaseOrderId),
                    PayDate = t.PayDate,
                    PaymentAmount = t.PaymentAmount,
                    PaymentStatus = t.PaymentStatus,
                    ReviewStatus = t.ReviewStatus,
                    StatusName = CommonHelper.GetDescription((EPurchaseRequestStatus)t.ReviewStatus),
                    Batch = t.Batch,
                    TotalRecord = t.TotalRecord
                };
                return item;
            }).ToList() : new List<PaymentPlanPagingResponse>();
            var totalRecord = response.Count > 0 ? response.FirstOrDefault().TotalRecord : 0;
            var res = new PagingResponseModel<PaymentPlanPagingResponse>()
            {
                Items = response,
                TotalRecord = totalRecord
            };
            return res;
        }

        public async Task<CombineResponseModel<PaymentPlan>> ChangeStatusAsync(int id)
        {
            var res = new CombineResponseModel<PaymentPlan>();
            var paymentPlan = await _unitOfWork.PaymentPlanRepository.GetByIdAsync(id);
            if (paymentPlan == null)
            {
                res.ErrorMessage = "Không tồn tại đợt thanh toán";
                return res; 
            }
            if (!paymentPlan.PayDate.HasValue)
            {
                res.ErrorMessage = "Chưa chọn ngày thanh toán";
                return res;
            }
            if (paymentPlan.PaymentStatus)
            {
                res.ErrorMessage = "Đợt này đã thanh toán";
                return res;
            }
            var isAllowToChange = false;
            var lineItem = await _unitOfWork.POPRLineItemRepository.GetByPurchareOrderIdAsync(paymentPlan.PurchaseOrderId);
            if(lineItem.Count > 0)
            {
                isAllowToChange = lineItem.Any(t => t.PorequestLineItem.PurchaseRequest.ReviewStatus != (int)EPurchaseRequestStatus.DirectorApproved);//nếu có bất kì rq mà chưa được giám đốc duyệt
            }
            if(isAllowToChange)
            {
                res.ErrorMessage = "Có yêu cầu chưa được giám đốc duyệt";
                return res;
            }
            paymentPlan.PaymentStatus = true;
            paymentPlan.UpdateDate = DateTime.UtcNow.UTCToIct();
            res.Status = true;
            res.Data = paymentPlan;
            return res;
        }

        public async Task<CombineResponseModel<PaymentPlan>> RefundAsync(int id, PaymentRefundRequest request)
        {
            var res = new CombineResponseModel<PaymentPlan>();
            var paymentPlan = await _unitOfWork.PaymentPlanRepository.GetByIdAsync(id);
            if (paymentPlan == null)
            {
                res.ErrorMessage = "Không tồn tại đợt thanh toán";
                return res;
            }
            var isHasUnpaid = paymentPlan.PurchaseOrder.PaymentPlans.Any(t => !t.PaymentStatus);
            if (isHasUnpaid)
            {
                res.ErrorMessage = "Vui lòng cập nhật cho những đợt chưa thanh toán";
                return res;
            }
            var poTotalPrice = await _unitOfWork.PurchaseOrderRepository.GetPurchaseOrderTotalPriceAsync(paymentPlan.PurchaseOrderId);
            var totalPrice = poTotalPrice.Count > 0 ? poTotalPrice.FirstOrDefault().TotalPriceWithVat : 0;
            var sumPaymentPlans = paymentPlan.PurchaseOrder.PaymentPlans.Sum(t => t.PaymentAmount);
            var refundAmount = (int)Math.Floor(totalPrice) - (int)Math.Floor(sumPaymentPlans);
            if(refundAmount < 0)
            {
                var sumRefundAmount = paymentPlan.PurchaseOrder.PaymentPlans.Sum(t => t.RefundAmount);
                if(Math.Abs(refundAmount) == Math.Abs((decimal)sumRefundAmount))
                {
                    res.ErrorMessage = "Số tiền hoàn trả đã đủ";
                    return res;
                }
                if(Math.Abs(refundAmount)< Math.Abs((decimal)refundAmount) - (int)Math.Floor(request.RefundAmount))
                {
                    res.ErrorMessage = "Tổng số tiền hoàn các đợt không được lớn hơn số tiền hoàn của đơn hàng";
                    return res;
                }
            }
            paymentPlan.Note = request.Note ?? string.Empty;
            paymentPlan.RefundAmount = request.RefundAmount;
            paymentPlan.UpdateDate = DateTime.UtcNow.UTCToIct();
            res.Status = true;
            res.Data = paymentPlan;
            return res;
        }
    }
}
