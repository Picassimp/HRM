using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Enums;
using InternalPortal.ApplicationCore.Helpers;
using InternalPortal.ApplicationCore.Interfaces;
using InternalPortal.ApplicationCore.Interfaces.Business;
using InternalPortal.ApplicationCore.Interfaces.Email;
using InternalPortal.ApplicationCore.Interfaces.Utilities.AzureBlob;
using InternalPortal.ApplicationCore.Models;
using InternalPortal.ApplicationCore.Models.PurchaseOrder.Product;
using InternalPortal.ApplicationCore.Models.PurchaseOrder.PurchaseOrder;
using InternalPortal.ApplicationCore.Models.PurchaseOrder.PurchaseRequest;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using static InternalPortal.ApplicationCore.ValueObjects.Global;

namespace InternalPortal.Infrastructure.Services.Business
{
    public class PurchaseOrderService : IPurchaseOrderService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IConfiguration _configuration;
        private readonly IBlobService _blobService;
        private readonly ISendMailDynamicTemplateService _sendMailDynamicTemplateService;
        public PurchaseOrderService(IUnitOfWork unitOfWork,
            IConfiguration configuration,
            IBlobService blobService,
            ISendMailDynamicTemplateService sendMailDynamicTemplateService)
        {
            _unitOfWork = unitOfWork;
            _configuration = configuration;
            _blobService = blobService;
            _sendMailDynamicTemplateService = sendMailDynamicTemplateService;
        }
        #region Private Method
        private int GetIdFromStringId(string s)
        {
            string numberString = new string(s.Where(char.IsDigit).ToArray());

            // Nếu chuỗi số không rỗng, chuyển đổi thành số nguyên, nếu không thì trả về 0
            return string.IsNullOrEmpty(numberString) ? 0 : int.Parse(numberString);
        }
        #endregion
        public async Task<CombineResponseModel<List<PurchaseOrderFile>>> AddFileAsync(int id,string email, PurchaseOrderFileRequest request)
        {
            var res = new CombineResponseModel<List<PurchaseOrderFile>>();  
            var purchaseOrder = await _unitOfWork.PurchaseOrderRepository.GetByIdAsync(id);
            if (purchaseOrder == null)
            {
                res.ErrorMessage = "Không tồn tại đơn đặt hàng";
                return res;
            }
            var hrEmail = await _unitOfWork.GlobalConfigurationRepository.GetByNameAsync(Constant.FirstHrEmail);
            if (hrEmail == null || string.IsNullOrEmpty(hrEmail.Value))
            {
                res.ErrorMessage = "Người dùng chưa được config";
                return res;
            }
            var hr1Email = hrEmail.Value.Split(",").Select(t=>t.Trim()).FirstOrDefault(t=>t.Equals(email,StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrEmpty(hr1Email))
            {
                res.ErrorMessage = "Bạn không có quyền cập nhật đơn đặt hàng";
                return res;
            }

            // khi trên FE khi xóa 1 file nào đó sẽ lưu id file sẽ xóa vào PurchaseOrderFileIds và sẽ xóa ở db những file có id nằm trong PurchaseOrderFileIds
            var purchaseOrderFileIds = !string.IsNullOrEmpty(request.PurchaseOrderFileIds) ? request.PurchaseOrderFileIds.Split(",").ToList() : new List<string>();
            if(purchaseOrderFileIds.Count > 0)
            {
                foreach(var item in purchaseOrderFileIds)
                {
                    var purchaseOrderFileExist = purchaseOrder.PurchaseOrderFiles.FirstOrDefault(t => t.Id == int.Parse(item));
                    if(purchaseOrderFileExist != null)
                    {
                        await _unitOfWork.PurchaseOrderFileRepository.DeleteAsync(purchaseOrderFileExist);
                    }
                }
            }
            var purchaseOrderFiles = new List<PurchaseOrderFile>();
            if(request.Files.Count > 0)
            {
                var now = DateTime.UtcNow.UTCToIct();
                foreach(var file in request.Files)
                {
                    using (var ms = new MemoryStream())
                    {
                        await file.CopyToAsync(ms);
                        var fileBytes = ms.ToArray();
                        var filename = $"{now.Year}/{now.Month}/{Guid.NewGuid().ToString()}";
                        var imageUrl = await _blobService.UploadAsync(fileBytes, BlobContainerName.PurchaseOrder, filename, file.ContentType);
                        if (imageUrl == null)
                        {
                            res.ErrorMessage = "Tải hình lên lỗi";
                            return res;
                        }
                        var purchaseOrderFile = new PurchaseOrderFile()
                        {
                            PurchaseOrderId = id,
                            FileUrl = imageUrl.RelativeUrl,
                            CreateDate = DateTime.UtcNow.UTCToIct()
                        };
                        purchaseOrderFiles.Add(purchaseOrderFile);
                    }
                }
            }
            res.Status = true;
            res.Data = purchaseOrderFiles;
            return res;
        }

        public async Task<CombineResponseModel<PurchaseOrder>> AddItemFromRequestAsync(int purchaseOrderId, ItemCreateRequest request)
        {
            var res = new CombineResponseModel<PurchaseOrder>();
            var purchaseOrder = await _unitOfWork.PurchaseOrderRepository.GetByIdAsync(purchaseOrderId);
            if(purchaseOrder == null)
            {
                res.ErrorMessage = "Không tồn tại đơn mua hàng";
                return res;
            }
            var purchaseRequest = await _unitOfWork.PurchaseRequestRepository.GetByIdAsync(request.RequestId);
            if (purchaseRequest == null)
            {
                res.ErrorMessage = "Không tồn tại đơn yêu cầu";
                return res;
            }
            //nếu không phải po bù thì chỉ thêm những rq chưa được duyệt
            if (!purchaseOrder.IsCompensationPo && (purchaseRequest.ReviewStatus == (int)EPurchaseRequestStatus.AccountantApproved 
                || purchaseRequest.ReviewStatus == (int)EPurchaseRequestStatus.DirectorApproved) 
                || purchaseRequest.ReviewStatus == (int)EPurchaseRequestStatus.Delivered)
            {
                res.ErrorMessage = "Yêu cầu đã được duyệt";
                return res;
            }
            var podetails = await _unitOfWork.PODetailRepository.GetByPurchaseOrderIdAsync(purchaseOrderId,true);
            var poprlineItems = await _unitOfWork.POPRLineItemRepository.GetByPurchareOrderIdAsync(purchaseOrderId);
            var lineItemFromRequest = await _unitOfWork.PurchaseRequestLineItemRepository.GetByPurchaseRequestIdAsync(request.RequestId);
            var lineItemIdsFromRequest = string.Join(",", lineItemFromRequest.Select(t => t.Id)).Split(",").ToList();
            if (string.IsNullOrEmpty(request.LineItemIds)) //không check item nào
            {
                if (poprlineItems.Any())//kiểm tra xem đã được thêm trước đó chưa
                {
                    foreach(var id in lineItemIdsFromRequest)
                    {
                        var lineItemExist = poprlineItems.FirstOrDefault(t => t.PorequestLineItemId == int.Parse(id));
                        if(lineItemExist != null)
                        {
                            var podetailDelete = podetails.FirstOrDefault(t=>t.Id == lineItemExist.PurchaseOrderDetailId && t.PoprlineItems.Count == 1);
                            if(podetailDelete != null)//nếu podetail chỉ có 1 item thì xóa luôn podetail
                            {
                                await _unitOfWork.POPRLineItemRepository.DeleteAsync(lineItemExist);
                                await _unitOfWork.PODetailRepository.DeleteAsync(podetailDelete);
                            }
                            else
                            {
                                await _unitOfWork.POPRLineItemRepository.DeleteAsync(lineItemExist);
                            }
                        }
                    }
                    poprlineItems = poprlineItems.Where(t => !lineItemIdsFromRequest.Contains(t.PorequestLineItemId.ToString())).ToList();
                } 
            }
            else
            {
                var selectItems = request.LineItemIds.Split(",").ToList();//Lấy các item được chọn
                var existItem = poprlineItems.Where(t => lineItemIdsFromRequest.Contains(t.PorequestLineItemId.ToString()));//tìm những item đã được check trước đó
                var itemCreate = new List<ItemCreateResponse>();
                if (existItem.Any())
                {
                    var lineItemsIdsExist = string.Join(",",existItem.Select(t=>t.PorequestLineItemId)).Split(",").ToList();//lấy những id được check trước đó
                    var insertItems = selectItems.Where(t => !lineItemsIdsExist.Contains(t)).ToList();//lấy những item chưa check
                    var deleteItem = lineItemsIdsExist.Where(t => !selectItems.Contains(t));//lấy các item đã check trước đó nhưng giờ không check nữa
                    if (insertItems.Any())
                    {
                        var selectLineItemIds = string.Join(",", insertItems);
                        itemCreate = await _unitOfWork.PurchaseRequestLineItemRepository.GetItemCreateResponse(selectLineItemIds, purchaseOrderId);
                        itemCreate = itemCreate.Where(t => t.RemainQty != 0).ToList();
                        if (itemCreate.Any())
                        {
                            foreach(var item in itemCreate)
                            {
                                //var detailExist = podetails.FirstOrDefault(t => t.PurchaseOrderId == purchaseOrderId && t.ProductId == item.ProductId && t.IsFromRequest);
                                var newPOPRLineItem = new PoprlineItem()
                                {
                                    PorequestLineItemId = item.Id,
                                    Quantity = item.RemainQty,
                                    CreateDate = DateTime.UtcNow.UTCToIct(),
                                    IsReceived = false
                                };
                                // không merge các line,mỗi lần thêm => tạo 1 line mới
                                var newPODetail = new Podetail()
                                {
                                    PurchaseOrderId = purchaseOrderId,
                                    ProductId = item.ProductId,
                                    Quantity = item.RemainQty,
                                    SubProductId = null,
                                    CreateDate = DateTime.UtcNow.UTCToIct(),
                                    IsFromRequest = true,
                                    Vat = 0
                                };
                                newPODetail.PoprlineItems.Add(newPOPRLineItem);
                                await _unitOfWork.PODetailRepository.CreateAsync(newPODetail);
                                podetails.Add(newPODetail);
                                //else
                                //{
                                //    detailExist.UpdateDate = DateTime.UtcNow.UTCToIct();
                                //    newPOPRLineItem.PurchaseOrderDetailId = detailExist.Id;
                                //    poprlineItems.Add(newPOPRLineItem);
                                //    await _unitOfWork.POPRLineItemRepository.CreateAsync(newPOPRLineItem);
                                //} 
                            }
                        }
                    }
                    if (deleteItem.Any())
                    {
                        foreach(var id in deleteItem)
                        {
                            var lineItemExist = poprlineItems.FirstOrDefault(t => t.PorequestLineItemId == int.Parse(id));
                            var podetailDelete = podetails.FirstOrDefault(t => t.Id == lineItemExist.PurchaseOrderDetailId && t.PoprlineItems.Count == 1);
                            if (podetailDelete != null)//nếu podetail chỉ có 1 item 
                            {
                                await _unitOfWork.POPRLineItemRepository.DeleteAsync(lineItemExist);
                                await _unitOfWork.PODetailRepository.DeleteAsync(podetailDelete);
                            }
                            else
                            {
                                await _unitOfWork.POPRLineItemRepository.DeleteAsync(lineItemExist);
                            }
                        }
                    }
                }
                else
                {
                    itemCreate = await _unitOfWork.PurchaseRequestLineItemRepository.GetItemCreateResponse(request.LineItemIds, purchaseOrderId);
                    itemCreate = itemCreate.Where(t => t.RemainQty != 0).ToList();
                    if (itemCreate.Any())
                    {
                        foreach (var item in itemCreate)
                        {
                            //var detailExist = podetails.FirstOrDefault(t => t.PurchaseOrderId == purchaseOrderId && t.ProductId == item.ProductId && t.IsFromRequest);
                            var newPOPRLineItem = new PoprlineItem()
                            {
                                PorequestLineItemId = item.Id,
                                Quantity = item.RemainQty,
                                CreateDate = DateTime.UtcNow.UTCToIct(),
                                IsReceived = false
                            };
                            var newPODetail = new Podetail()
                            {
                                PurchaseOrderId = purchaseOrderId,
                                ProductId = item.ProductId,
                                Quantity = item.RemainQty,
                                SubProductId = null,
                                CreateDate = DateTime.UtcNow.UTCToIct(),
                                IsFromRequest = true,
                                Vat = 0
                            };
                            newPODetail.PoprlineItems.Add(newPOPRLineItem);
                            await _unitOfWork.PODetailRepository.CreateAsync(newPODetail);
                            podetails.Add(newPODetail);
                            //else
                            //{
                            //    detailExist.UpdateDate = DateTime.UtcNow.UTCToIct();
                            //    newPOPRLineItem.PurchaseOrderDetailId = detailExist.Id;
                            //    poprlineItems.Add(newPOPRLineItem);
                            //    await _unitOfWork.POPRLineItemRepository.CreateAsync(newPOPRLineItem);
                            //}
                        }
                    }
                }
            }
            var group = poprlineItems.GroupBy(t => t.PurchaseOrderDetailId).Select(t=> new
            {
                Id = t.Key,
                TotalQty = t.ToList().Sum(t=>t.Quantity)
            });
            foreach(var item in group)// cập nhật lại số lượng cho podetail
            {
                var podetailExists = podetails.FirstOrDefault(t=>t.Id == item.Id);
                podetailExists.Quantity = item.TotalQty;
            }
            await _unitOfWork.POPRLineItemRepository.UpdateRangeAsync(poprlineItems);
            await _unitOfWork.PODetailRepository.UpdateRangeAsync(podetails);
            await _unitOfWork.SaveChangesAsync();
            res.Status = true;
            return res;
        }

        public async Task<CombineResponseModel<List<Podetail>>> AddNewProductAsync(int id,string email, ProductRequest request)
        {
            var res = new CombineResponseModel<List<Podetail>>();
            if(request.Quantity <= 0)
            {
                res.ErrorMessage = "Số lượng không hợp lệ";
                return res;
            }
            var hrEmail = await _unitOfWork.GlobalConfigurationRepository.GetByNameAsync(Constant.FirstHrEmail);
            if (hrEmail == null || string.IsNullOrEmpty(hrEmail.Value))
            {
                res.ErrorMessage = "Người dùng chưa được config";
                return res;
            }
            var hr1Email = hrEmail.Value.Split(",").Select(t => t.Trim()).FirstOrDefault(t => t.Equals(email, StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrEmpty(hr1Email))
            {
                res.ErrorMessage = "Bạn không có quyền thêm sản phẩm";
                return res;
            }
            var purchaseOrder = await _unitOfWork.PurchaseOrderRepository.GetByIdAsync(id);
            if(purchaseOrder == null)
            {
                res.ErrorMessage = "Không tồn tại đơn mua hàng";
                return res;
            }
            var product = await _unitOfWork.ProductRepository.GetByIdAsync(request.ProductId);
            if(product == null)
            {
                res.ErrorMessage = "Không tồn tại sản phẩm";
                return res;
            }
            var podetails = new List<Podetail>();
            var productKit = await _unitOfWork.ProductKitRepository.GetByProductIdAsync(request.ProductId);//lấy các sub product
            var podetailsExist = await _unitOfWork.PODetailRepository.GetByPurchaseOrderIdAsync(purchaseOrder.Id, false);//tìm các phẩm mới đã được thêm trước đó
            if(productKit.Count > 0)
            {
                if(podetailsExist.Count == 0)//chưa có sản phẩm thì tạo mới
                {
                    foreach (var item in productKit)
                    {
                        var podetail = new Podetail();
                        podetail.PurchaseOrderId = id;
                        podetail.ProductId = item.ProductId;
                        podetail.Quantity = request.Quantity * item.Quantity;
                        podetail.CreateDate = DateTime.UtcNow.UTCToIct();
                        podetail.SubProductId = item.SubProductId;
                        podetail.IsFromRequest = false;
                        podetail.Price = 0;
                        podetails.Add(podetail);
                    }
                }
                else
                {
                    foreach(var item in productKit)
                    {
                        var podetailExist = podetailsExist.FirstOrDefault(t => t.ProductId == item.ProductId && t.SubProductId == item.SubProductId);
                        if(podetailExist != null)//nếu thêm sản phẩm đã có ở PO thì tăng số lượng chưa có thì tạo mới
                        {
                            podetailExist.Quantity = podetailExist.Quantity + (request.Quantity * item.Quantity);
                            await _unitOfWork.PODetailRepository.UpdateAsync(podetailExist);
                        }
                        else
                        {
                            var podetail = new Podetail();
                            podetail.PurchaseOrderId = id;
                            podetail.ProductId = item.ProductId;
                            podetail.Quantity = request.Quantity * item.Quantity;
                            podetail.CreateDate = DateTime.UtcNow.UTCToIct();
                            podetail.SubProductId = item.SubProductId;
                            podetail.IsFromRequest = false;
                            podetail.Price = 0;
                            podetails.Add(podetail);
                        }
                    }
                }
            }
            else
            {
                var podetailExist = podetailsExist.FirstOrDefault(t=> t.ProductId == request.ProductId && t.SubProductId == null);
                if(podetailExist != null)//nếu thêm sản phẩm đã có ở PO thì tăng số lượng chưa có thì tạo mới
                {
                    podetailExist.Quantity = podetailExist.Quantity + request.Quantity;
                    await _unitOfWork.PODetailRepository.UpdateAsync(podetailExist);
                }
                else
                {
                    var podetail = new Podetail();
                    podetail.PurchaseOrderId = id;
                    podetail.ProductId = request.ProductId;
                    podetail.Quantity = request.Quantity;
                    podetail.Price = 0;
                    podetail.CreateDate = DateTime.UtcNow.UTCToIct();
                    podetail.SubProductId = null;
                    podetail.IsFromRequest = false;
                    podetails.Add(podetail);
                }
            }
            res.Status = true;
            res.Data = podetails;
            return res;
        }

        public async Task<CombineResponseModel<PurchaseOrder>> ChangeStatusAsync(int id, string email, bool isPurchase)
        {
            var res = new CombineResponseModel<PurchaseOrder>();
            var hrEmail = await _unitOfWork.GlobalConfigurationRepository.GetByNameAsync(Constant.FirstHrEmail);
            if (hrEmail == null || string.IsNullOrEmpty(hrEmail.Value))
            {
                res.ErrorMessage = "Người dùng chưa được config";
                return res;
            }
            var hr1Email = hrEmail.Value.Split(",").Select(t => t.Trim()).FirstOrDefault(t => t.Equals(email, StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrEmpty(hr1Email))
            {
                res.ErrorMessage = "Bạn không có quyền thay đổi trạng thái đơn hàng";
                return res;
            }
            var purchaseOrder = await _unitOfWork.PurchaseOrderRepository.GetByIdAsync(id);
            if(purchaseOrder == null)
            {
                res.ErrorMessage = "Không tồn tại đơn đặt hàng";
                return res;
            }
            if(purchaseOrder.Status == (int)EPurchaseOrderStatus.FullReceived || purchaseOrder.Status == (int)EPurchaseOrderStatus.LackReceived 
                || purchaseOrder.Status == (int)EPurchaseOrderStatus.Purchased || purchaseOrder.IsClose)
            {
                res.ErrorMessage = "Không thể cập nhật cho đơn hàng có trạng thái: " + CommonHelper.GetDescription((EPurchaseOrderStatus)purchaseOrder.Status) + " hoặc đã đóng";
                return res;
            }
            if (!purchaseOrder.IsCompensationPo)
            {
                var lineItems = await _unitOfWork.POPRLineItemRepository.GetByPurchareOrderIdAsync(purchaseOrder.Id);
                var isAllRequestAccept = false;
                if (lineItems.Count > 0)
                {
                    isAllRequestAccept = lineItems.Any(t => t.PorequestLineItem.PurchaseRequest.ReviewStatus != (int)EPurchaseRequestStatus.DirectorApproved) ? false : true;
                    if (isAllRequestAccept)
                    {
                        purchaseOrder.Status = (int)EPurchaseOrderStatus.Purchased;
                        purchaseOrder.UpdateDate = DateTime.UtcNow.UTCToIct();
                    }
                    else
                    {
                        res.ErrorMessage = "Có yêu cầu chưa được giám đốc duyệt";
                        return res;
                    }
                }
                else
                {
                    res.ErrorMessage = "Không có sản phẩm trong đơn mua hàng";
                    return res;
                }
            }
            else
            {
                if(purchaseOrder.Status != (int)EPurchaseOrderStatus.DirectorAccept)
                {
                    res.ErrorMessage = "Đơn mua hàng chưa được giám đốc duyệt";
                    return res;
                }
                else
                {
                    purchaseOrder.Status = (int)EPurchaseOrderStatus.Purchased;
                    purchaseOrder.UpdateDate = DateTime.UtcNow.UTCToIct();
                }
            }
            res.Status = true;
            res.Data = purchaseOrder;
            return res;
        }

        public async Task<CombineResponseModel<bool>> CheckFullReceiveAsync(int id,bool isSkip)
        {
            var res = new CombineResponseModel<bool>();
            var purchaseOrder = await _unitOfWork.PurchaseOrderRepository.GetByIdAsync(id);
            if (purchaseOrder == null)
            {
                res.ErrorMessage = "Không tồn tại đơn đặt hàng";
            }
            var lineItem = await _unitOfWork.POPRLineItemRepository.GetByPurchareOrderIdAsync(id);
            var isAllRequestAccept = false;
            if (lineItem.Count > 0)
            {
                isAllRequestAccept = lineItem.Any(t => t.PorequestLineItem.PurchaseRequest.ReviewStatus != (int)EPurchaseRequestStatus.DirectorApproved) ? false : true;
            }
            if(!isAllRequestAccept)
            {
                res.ErrorMessage = "Có yêu cầu trong đơn đặt hàng chưa được giám đốc duyệt";
                return res;
            }
            var isReceiveFull = false;
            if (lineItem.Count > 0)
            {
                if(lineItem.Any(t=>t.Quantity == 0))
                {
                    isReceiveFull = false;
                }
                else
                {
                    isReceiveFull = lineItem.Any(t => t.Quantity != t.QuantityReceived) ? false : true;//check các item từ yêu cầu nhận đủ chưa
                } 
            }
            if (isReceiveFull || isSkip)
            {
                if (purchaseOrder.IsClose)
                {
                    res.ErrorMessage = "Đơn mua hàng đã được đóng trước đó";
                }
                purchaseOrder.Status = isReceiveFull ? (int)EPurchaseOrderStatus.FullReceived : (int)EPurchaseOrderStatus.LackReceived;
                purchaseOrder.UpdateDate = DateTime.UtcNow.UTCToIct();
                purchaseOrder.IsClose = true;
                lineItem.ForEach(t => t.IsReceived = true);
                await _unitOfWork.POPRLineItemRepository.UpdateRangeAsync(lineItem);
                await _unitOfWork.PurchaseOrderRepository.UpdateAsync(purchaseOrder);
                await _unitOfWork.SaveChangesAsync();
            }
            res.Status = true;
            res.Data = isReceiveFull;
            return res;
        }

        public async Task<CombineResponseModel<PurchaseOrder>> CreateAsync(int userId,string email,PurchaseOrderRequest request)
        {
            var res = new CombineResponseModel<PurchaseOrder>();
            if(request.PaymentMethod == (int)EPaymentMethod.Card)
            {
                if(string.IsNullOrEmpty(request.BankNumber) || string.IsNullOrEmpty(request.BankName))
                {
                    res.ErrorMessage = "Vui lòng nhập đủ thông tin";
                    return res;
                }
            }
            var hrEmail = await _unitOfWork.GlobalConfigurationRepository.GetByNameAsync(Constant.FirstHrEmail);
            if (hrEmail == null || string.IsNullOrEmpty(hrEmail.Value))
            {
                res.ErrorMessage = "Người dùng chưa được config";
                return res;
            }
            var hr1Email = hrEmail.Value.Split(",").Select(t => t.Trim()).FirstOrDefault(t => t.Equals(email, StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrEmpty(hr1Email))
            {
                res.ErrorMessage = "Bạn không có quyền tạo PO";
                return res;
            }
            var vendorId = request.VendorId;
            if(vendorId != 0)
            {
                var vendor = await _unitOfWork.VendorRepository.GetByIdAsync(vendorId);
                if(vendor == null) 
                {
                    if (!string.IsNullOrEmpty(request.VendorName))
                    {
                        var vendorCreate = new Vendor()
                        {
                            VendorName = request.VendorName,
                            CreateDate = DateTime.UtcNow.UTCToIct()
                        };
                        await _unitOfWork.VendorRepository.CreateAsync(vendorCreate);
                        await _unitOfWork.SaveChangesAsync();
                        vendorId = vendorCreate.Id;
                    }
                }
            }
            else
            {
                if (!string.IsNullOrEmpty(request.VendorName))
                {
                    var vendorCreate = new Vendor()
                    {
                        VendorName = request.VendorName,
                        CreateDate = DateTime.UtcNow.UTCToIct()
                    };
                    await _unitOfWork.VendorRepository.CreateAsync(vendorCreate);
                    await _unitOfWork.SaveChangesAsync();
                    vendorId = vendorCreate.Id;
                }
            }
            var purchaserOrder = new PurchaseOrder()
            {
                VendorId = vendorId == 0 ? null : vendorId,
                CreateUserId = userId,
                ExpectedDate = request.ExpectedDate,
                PaymentMethod = request.PaymentMethod,
                BankNumber = request.BankNumber ?? "",
                BankName = request.BankName ?? "",
                IsClose = false,
                Status = (int)EPurchaseOrderStatus.Pending,
                CreateDate = DateTime.UtcNow.UTCToIct(),
                Note = request.Note,
                Vat = request.Vat ?? 0,
            };
            res.Status = true;
            res.Data = purchaserOrder;
            return res;
        }

        public async Task<CombineResponseModel<PurchaseOrder>> CreateCompensationPOAsync(int id,string email,int userId,AdditionalPurchaseOrderRequest request)
        {
            var res = new CombineResponseModel<PurchaseOrder>();
            if (string.IsNullOrEmpty(request.Reason))
            {
                res.ErrorMessage = "Lý do không được trống";
                return res;
            }
            var purchaseOrder = await _unitOfWork.PurchaseOrderRepository.GetByIdAsync(id);
            if(purchaseOrder == null)
            {
                res.ErrorMessage = "Không tồn tại đơn đặt hàng";
            }
            var hrEmail = await _unitOfWork.GlobalConfigurationRepository.GetByNameAsync(Constant.FirstHrEmail);
            if (hrEmail == null || string.IsNullOrEmpty(hrEmail.Value))
            {
                res.ErrorMessage = "Người dùng chưa được config";
                return res;
            }
            var hr1Email = hrEmail.Value.Split(",").Select(t => t.Trim()).FirstOrDefault(t => t.Equals(email, StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrEmpty(hr1Email))
            {
                res.ErrorMessage = "Bạn không có quyền tạo PO";
                return res;
            }
            var vendor = await _unitOfWork.VendorRepository.GetByIdAsync(request.VendorId);
            if (vendor == null)
            {
                res.ErrorMessage = "Không tồn tại nhà cung cấp";
                return res;
            }
            var purchaseOrderCreate = new PurchaseOrder()
            {
                VendorId = vendor.Id,
                CreateDate = DateTime.UtcNow.UTCToIct(),
                ExpectedDate = null,
                Note = null,
                PaymentMethod = null,
                BankNumber = null,
                BankName = null,
                IsCompensationPo = true,
                CreateUserId = userId,
                Status = (int)EPurchaseOrderStatus.Pending,
                Reason = request.Reason
            };
            var lineItems = await _unitOfWork.POPRLineItemRepository.GetByPurchareOrderIdAsync(purchaseOrder.Id);
            var linetItemIds = string.Join(",", lineItems.Where(t => (bool)t.IsReceived).Select(t => t.PorequestLineItemId));
            var itemCreate = await _unitOfWork.PurchaseRequestLineItemRepository.GetItemCreateResponse(linetItemIds,0);
            itemCreate = itemCreate.Where(t=>t.RemainQty != 0).ToList();//Lấy ra các item còn thiếu
            if(itemCreate.Count == 0)
            {
                res.ErrorMessage = "Đơn hàng đã đủ số lượng,không thể tạo PO bù";
                return res;
            }
            //var group = itemCreate.GroupBy(t => t.ProductId);
            var podetails = new List<Podetail>();
            foreach( var item in itemCreate)
            {
                var podetail = new Podetail()
                {
                    ProductId = item.ProductId,
                    Quantity = item.RemainQty,
                    Price = 0,
                    ShoppingUrl = string.Empty,
                    CreateDate = DateTime.UtcNow.UTCToIct(),
                    SubProductId = null,
                    IsFromRequest = true,
                    QuantityReceived = 0,
                    Vat = 0
                };
                var poprLineItem = new PoprlineItem()
                {
                    PorequestLineItemId = item.Id,
                    Quantity = item.RemainQty,
                    QuantityReceived = 0,
                    CreateDate = DateTime.UtcNow.UTCToIct(),
                    IsReceived = false
                };
                podetail.PoprlineItems.Add(poprLineItem);
                podetails.Add(podetail);
            }
            purchaseOrderCreate.Podetails = podetails;
            res.Status = true;
            res.Data = purchaseOrderCreate;
            return res;
        }

        public async Task<CombineResponseModel<PurchaseOrder>> CreatePOFromRequestAsync(int id,int userId,string email,AdditionalPurchaseOrderRequest requestPO)
        {
            var res = new CombineResponseModel<PurchaseOrder>();
            var request = await _unitOfWork.PurchaseRequestRepository.GetByIdAsync(id);
            if (request == null)
            {
                res.ErrorMessage = "Không tồn tại yêu cầu";
                return res;
            }
            if(request.ReviewStatus == (int)EPurchaseRequestStatus.AccountantApproved 
                || request.ReviewStatus == (int)EPurchaseRequestStatus.DirectorApproved 
                || request.ReviewStatus == (int)EPurchaseRequestStatus.Delivered)
            {
                res.ErrorMessage = "Yêu cầu đã được duyệt";
                return res;
            }
            var hrEmail = await _unitOfWork.GlobalConfigurationRepository.GetByNameAsync(Constant.FirstHrEmail);
            if (hrEmail == null || string.IsNullOrEmpty(hrEmail.Value))
            {
                res.ErrorMessage = "Người dùng chưa được config";
                return res;
            }
            var hr1Email = hrEmail.Value.Split(",").Select(t => t.Trim()).FirstOrDefault(t => t.Equals(email, StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrEmpty(hr1Email))
            {
                res.ErrorMessage = "Bạn không có quyền tạo PO";
                return res;
            }
            var vendor = await _unitOfWork.VendorRepository.GetByIdAsync(requestPO.VendorId);
            if(vendor == null)
            {
                res.ErrorMessage = "Không tồn tại nhà cung cấp";
                return res;
            }
            var purchaseOrder = new PurchaseOrder()
            {
                VendorId = vendor.Id,
                CreateDate = DateTime.UtcNow.UTCToIct(),
                ExpectedDate = null,
                Note = null,
                PaymentMethod = null,
                BankNumber = null,
                BankName = null,
                IsCompensationPo = false,
                CreateUserId = userId,
                Status = (int)EPurchaseOrderStatus.Pending,
            };
            var podetails = new List<Podetail>();
            var purchaseRequestLineItemIds = string.Join(",", request.PurchaseRequestLineItems.Select(t => t.Id.ToString()));
            //lấy ra những item chưa tạo xong
            var itemCreateResponse = await _unitOfWork.PurchaseRequestLineItemRepository.GetItemCreateResponse(purchaseRequestLineItemIds,0);
            itemCreateResponse = itemCreateResponse.Where(t=>t.RemainQty != 0).ToList();
            if(itemCreateResponse.Count == 0)
            {
                res.ErrorMessage = "Các mặt hàng trong yêu cầu đã đặt đủ không thể tạo PO";
                return res;
            }
            foreach (var item in itemCreateResponse)
            {
                var podetail = new Podetail() 
                {
                    ProductId = item.ProductId,
                    Quantity = item.RemainQty,
                    Price = 0,
                    ShoppingUrl = null,
                    CreateDate = DateTime.UtcNow.UTCToIct(),
                    SubProductId = null,
                    IsFromRequest = true,
                    Vat = 0
                };
                var poprLineItems = new List<PoprlineItem>();
                var poprLineItem = new PoprlineItem()
                {
                    PorequestLineItemId = item.Id,
                    Quantity = item.RemainQty,
                    QuantityReceived = 0,
                    CreateDate = DateTime.UtcNow.UTCToIct(),
                    UpdateDate = null,
                    IsReceived = false,
                };
                poprLineItems.Add(poprLineItem);
                podetail.PoprlineItems = poprLineItems;
                podetails.Add(podetail);
            }
            purchaseOrder.Podetails = podetails;
            res.Status = true;
            res.Data = purchaseOrder;
            return res;
        }

        public async Task<CombineResponseModel<PurchaseOrder>> DeleteAsync(int id,string email)
        {
            var res = new CombineResponseModel<PurchaseOrder>();
            var hrEmail = await _unitOfWork.GlobalConfigurationRepository.GetByNameAsync(Constant.FirstHrEmail);
            if (hrEmail == null || string.IsNullOrEmpty(hrEmail.Value))
            {
                res.ErrorMessage = "Người dùng chưa được config";
                return res;
            }
            var hr1Email = hrEmail.Value.Split(",").Select(t => t.Trim()).FirstOrDefault(t => t.Equals(email, StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrEmpty(hr1Email))
            {
                res.ErrorMessage = "Bạn không có quyền xóa PO";
                return res;
            }
            var purchaserOrder = await _unitOfWork.PurchaseOrderRepository.GetByIdAsync(id);
            if (purchaserOrder == null)
            {
                res.ErrorMessage = "Không tồn tại đơn đặt hàng";
                return res;
            }
            var lineItems = await _unitOfWork.POPRLineItemRepository.GetByPurchareOrderIdAsync(id);
            var isHasPrApprove = lineItems.Any(t => t.PorequestLineItem.PurchaseRequest.ReviewStatus == (int)EPurchaseRequestStatus.DirectorApproved);
            if (isHasPrApprove && !purchaserOrder.IsCompensationPo)
            {
                res.ErrorMessage = "Đã có yêu cầu được duyệt,không thể xóa";
                return res;
            }
            var poDetails = await _unitOfWork.PODetailRepository.GetByPurchaseOrderIdAsync(purchaserOrder.Id,null);
            if(lineItems.Count > 0)
            {
                await _unitOfWork.POPRLineItemRepository.DeleteRangeAsync(lineItems);
            }
            if(poDetails.Count > 0)
            {
                await _unitOfWork.PODetailRepository.DeleteRangeAsync(poDetails);
            }
            if(purchaserOrder.PurchaseOrderFiles.Count > 0)
            {
                await _unitOfWork.PurchaseOrderFileRepository.DeleteRangeAsync(purchaserOrder.PurchaseOrderFiles.ToList());
            }
            if(purchaserOrder.AdditionalPurchaseOrderRefAdditionalPurchaseOrders.Count > 0)
            {
                await _unitOfWork.AdditionalPurchaseOrderRefRepository.DeleteRangeAsync(purchaserOrder.AdditionalPurchaseOrderRefAdditionalPurchaseOrders.ToList());
            }
            if(purchaserOrder.AdditionalPurchaseOrderRefPurchaseOrders.Count > 0)
            {
                await _unitOfWork.AdditionalPurchaseOrderRefRepository.DeleteRangeAsync(purchaserOrder.AdditionalPurchaseOrderRefPurchaseOrders.ToList());
            }
            if(purchaserOrder.PaymentPlans.Count > 0)
            {
                await _unitOfWork.PaymentPlanRepository.DeleteRangeAsync(purchaserOrder.PaymentPlans.ToList());
            }
            res.Status = true;
            res.Data = purchaserOrder;
            return res;
        }

        public async Task<CombineResponseModel<List<POPRLineItemResponse>>> GetPOPRLineItemAsync(string email,int purchaseOrderDetailId)
        {
            var res = new CombineResponseModel<List<POPRLineItemResponse>>();
            var poDetail = await _unitOfWork.PODetailRepository.GetByIdAsync(purchaseOrderDetailId);
            if (poDetail == null)
            {
                res.ErrorMessage = "Không tìm thấy thông tin của chi tiết này";
                return res;
            }
            var names = new List<string>()
            {
                Constant.FirstHrEmail,
                Constant.SecondHrEmail,
                Constant.DirectorEmail
            };
            var emails = await _unitOfWork.GlobalConfigurationRepository.GetByMultiNameAsync(names);
            if (emails.Count == 0)
            {
                res.ErrorMessage = "Người dùng chưa được config";
                return res;
            }
            var isHasAllowEmail = string.Join(",", emails.Select(t => t.Value)).Split(",").Any(y => y.Equals(email, StringComparison.OrdinalIgnoreCase));
            if (!isHasAllowEmail)
            {
                res.ErrorMessage = "Bạn không có quyền xem thông tin này";
                return res;
            }
            var responseRaw = await _unitOfWork.PurchaseOrderRepository.GetPOPRLineItemAsync(poDetail.Id, poDetail.PurchaseOrderId);
            var response = responseRaw != null || responseRaw.Count > 0 ? responseRaw.Select(t =>
            {
                var item = new POPRLineItemResponse()
                {
                    Id = t.Id,
                    RequestId = t.RequestId,
                    RequestName = t.Name,
                    PODisplay = !string.IsNullOrEmpty(t.PurchaseOrderId) ? t.PurchaseOrderId.Split(",").Distinct().Select(t =>
                    {
                        var item = new IdDisplay()
                        {
                            Id = int.Parse(t),
                            StringId = CommonHelper.ToIdDisplayString("PO", int.Parse(t))
                        };
                        return item;
                    }).ToList() : new List<IdDisplay>(),
                    EBDisplay = !string.IsNullOrEmpty(t.ExportBillId) ? t.ExportBillId.Split(",").Distinct().Select(t =>
                    {
                        var item = new IdDisplay()
                        {
                            Id = int.Parse(t),
                            StringId = CommonHelper.ToIdDisplayString("XK", int.Parse(t))
                        };
                        return item;
                    }).ToList() : new List<IdDisplay>(),
                    RequestQty = t.RequestQty,
                    // nếu po nhận đủ or thiếu slcl = yêu cầu - phiếu xuất - nhận ở po hiện tại - nhận ở po khác ngược lại thì = yêu cầu - phiếu xuất - ở po khác
                    RemainQTy = t.IsReceived
                    ? t.TotalQtyReceived != 0 ? t.RequestQty - t.ExportQty - t.QuantityReceived - t.TotalQtyReceived : t.RequestQty - t.ExportQty - t.QuantityReceived
                    : t.RequestQty - t.ExportQty - t.FromOtherPOQty,
                    POQTy = t.POQTy,
                    QuantityReceived = t.QuantityReceived,
                    ReviewStatus = t.ReviewStatus,
                    StatusName = CommonHelper.GetDescription((EPurchaseRequestStatus)t.ReviewStatus),
                    IsReceived = t.IsReceived
                };
                return item;
            }).ToList() : new List<POPRLineItemResponse>();
            res.Status = true;
            res.Data = response;
            return res;
        }

        public async Task<CombineResponseModel<PurchaseOrderDetailResponse>> GetPurchaseOrderAsync(string email,int purchaseOrderId)
        {
            var domainUrl = _configuration["BlobDomainUrl"];
            var res = new CombineResponseModel<PurchaseOrderDetailResponse>();
            var purchaseOrder = await _unitOfWork.PurchaseOrderRepository.GetByIdAsync(purchaseOrderId);
            if(purchaseOrder == null)
            {
                res.ErrorMessage = "Không tồn tại đơn yêu cầu";
                return res;
            }
            var names = new List<string>()
            {
                Constant.FirstHrEmail,
                Constant.SecondHrEmail,
                Constant.DirectorEmail
            };
            var emails = await _unitOfWork.GlobalConfigurationRepository.GetByMultiNameAsync(names);
            if(emails.Count == 0)
            {
                res.ErrorMessage = "Người dùng chưa được config";
                return res;
            }
            var isHasAllowEmail = string.Join(",", emails.Select(t => t.Value)).Split(",").Any(y=>y.Equals(email,StringComparison.OrdinalIgnoreCase));
            if (!isHasAllowEmail)
            {
                res.ErrorMessage = "Bạn không có quyền xem thông tin này";
                return res;
            }
            var lineItem = await _unitOfWork.POPRLineItemRepository.GetByPurchareOrderIdAsync(purchaseOrder.Id);
            bool isHasLackReceive = false;
            if(lineItem.Count == 0)
            {
                isHasLackReceive = false;
            }
            var poTotalPrice = await _unitOfWork.PurchaseOrderRepository.GetPurchaseOrderTotalPriceAsync(purchaseOrder.Id);
            isHasLackReceive = lineItem.Any(t => (bool)t.IsReceived && t.Quantity != t.QuantityReceived);//kiểm tra có rq nào nhận thiếu hay không
            var isAllowToReceiveFull = isHasLackReceive ? false : true;
            var isShowFinishText = lineItem.All(t => t.Quantity == t.QuantityReceived);
            var sumPaymentPlans = (int)Math.Floor(purchaseOrder.PaymentPlans.Sum(t => t.PaymentAmount));
            var sumPaymentRefund = (int)Math.Floor((decimal)purchaseOrder.PaymentPlans.Sum(t => t.RefundAmount));
            var isUpdatePaymentPlan = poTotalPrice.Count > 0 ? (int)Math.Floor(poTotalPrice.FirstOrDefault().TotalPriceWithVat) != sumPaymentPlans - sumPaymentRefund ? true : false : false;
            var purchaseOrderDetailResponse = new PurchaseOrderDetailResponse()
            {
                Id = purchaseOrder.Id,
                StringId = CommonHelper.ToIdDisplayString("PO", purchaseOrder.Id),
                VendorId = purchaseOrder.VendorId,
                CreateDate = purchaseOrder.CreateDate,
                ExpectedDate = purchaseOrder.ExpectedDate,
                Note = purchaseOrder.Note,
                PaymentMethod = purchaseOrder.PaymentMethod,
                BankNumber = purchaseOrder.BankNumber,
                BankName = purchaseOrder.BankName,
                IsCompensationPO = purchaseOrder.IsCompensationPo,
                PurchaseOrdeStatus = purchaseOrder.Status,
                IsPurchased = purchaseOrder.Status == (int)EPurchaseOrderStatus.Purchased || purchaseOrder.Status == (int)EPurchaseOrderStatus.LackReceived
                || purchaseOrder.Status == (int)EPurchaseOrderStatus.FullReceived ? true : false,
                Isclose = purchaseOrder.IsClose,
                RejectReason = purchaseOrder.RejectReason ?? string.Empty,
                Vat = purchaseOrder.Vat ?? 0,
                Reason = purchaseOrder.IsCompensationPo ? purchaseOrder.Reason : string.Empty,
                IsAllowToReceiveFull = isAllowToReceiveFull,
                IsHasLackReceive = isHasLackReceive,
                IsShowFinishText = isShowFinishText,
                IsUpdatePaymentPlan = isUpdatePaymentPlan,
                TotalPriceWithVat = (int)Math.Floor((decimal)(poTotalPrice.Count > 0 ? poTotalPrice.FirstOrDefault().TotalPriceWithVat : 0)),
                TotalPriceWithoutVat = (int)Math.Floor((decimal)(poTotalPrice.Count > 0 ? poTotalPrice.FirstOrDefault().TotalPriceWithoutVat : 0)),
                IsAllowToEdit = purchaseOrder.IsCompensationPo ? 
                purchaseOrder.Status == (int)EPurchaseOrderStatus.AccountAccept 
                || purchaseOrder.Status == (int)EPurchaseOrderStatus.DirectorAccept 
                || purchaseOrder.Status == (int)EPurchaseOrderStatus.Reject ? false : true 
                : false,
            };
            var purchaseProductDetailRaw = await _unitOfWork.PurchaseOrderRepository.GetPurchaseOrderDetailAsync(purchaseOrderId);
            var departments = purchaseProductDetailRaw.Count > 0 ? 
                string.Join(",",string.Join(",", purchaseProductDetailRaw.Select(t => t.Departments)).Split(",").Distinct()) : string.Empty;
            var projects = purchaseProductDetailRaw.Count > 0 ?
                string.Join(",", string.Join(",", purchaseProductDetailRaw.Select(t => t.Projects)).Split(",").Distinct()) : string.Empty;
            purchaseOrderDetailResponse.Departments = departments;
            purchaseOrderDetailResponse.Projects = projects;
            //thông tin từ yêu cầu
            var purchaseOrderProductDetailFromRequestResponse = purchaseProductDetailRaw.Count > 0 ?
                purchaseProductDetailRaw.Select(t => new PurchaseOrderProductDetailFromRequestResponse
                {
                    Id = t.Id,
                    RequestDisplays = !string.IsNullOrEmpty(t.FromRequests) ? t.FromRequests.Split(";").Distinct().Select(t =>
                    {
                        var item = JsonConvert.DeserializeObject<IdDisplay>(t);
                        return item;
                    }).ToList() : new List<IdDisplay>(),
                    CategoryName = t.CategoryName,
                    ProductName = t.Name,
                    Detail = !string.IsNullOrEmpty(t.Detail) ? t.Detail.Split(",").ToList() : new List<string>(),
                    KitQuantity = !string.IsNullOrEmpty(t.KitQuantity) ? t.KitQuantity.Split(",").ToList() : new List<string>(),
                    TotalRequestQty = t.TotalRequestQty,
                    Description = t.Description,
                    PODisplays = !string.IsNullOrEmpty(t.FromOtherPOs) ? t.FromOtherPOs.Split(",").Distinct().Where(y => int.Parse(y) != 0 && int.Parse(y) != purchaseOrderId).Select(t =>
                    {
                        int id = int.Parse(t);
                        var item = new IdDisplay();
                        item.Id = id;
                        item.StringId = CommonHelper.ToIdDisplayString("PO", id);
                        return item;
                    }).ToList() : new List<IdDisplay>(),
                    EBDisplays = !string.IsNullOrEmpty(t.FromEBs) ? t.FromEBs.Split(",").Distinct().Select(t =>
                    {
                        int id = int.Parse(t);
                        var item = new IdDisplay()
                        {
                            Id = id,
                            StringId = CommonHelper.ToIdDisplayString("XK", id)
                        };
                        return item;
                    }).ToList() : new List<IdDisplay>(),
                    TotalQtyRemain = t.TotalRequestQty - t.QtyFromEBs - t.QtyFromOtherPOs - t.TotalQtyTrueReceived,
                    Quantity = t.Quantity,
                    Price = t.Price,
                    Departments = !string.IsNullOrEmpty(t.Departments) ? t.Departments.Split(",").Distinct().ToList() : new List<string>(),
                    Projects = !string.IsNullOrEmpty(t.Projects) ? t.Projects.Split(",").Distinct().ToList() : new List<string>(),
                    ShoppingUrl = t.ShoppingUrl ?? "",
                    TotalQtyReceived = t.TotalQtyReceived,
                    Vat = t.Vat,
                    TotalPrice = (int)Math.Floor(t.TotalPrice),
                    IsHasPrApprove = !string.IsNullOrEmpty(t.ReviewStatuses) ? 
                    t.ReviewStatuses.Split(",").Any(t=>int.Parse(t) == (int)EPurchaseRequestStatus.AccountantApproved 
                    || int.Parse(t) == (int)EPurchaseRequestStatus.DirectorApproved) : false,
                    VatPrice = t.VatPrice
                }).ToList() : new List<PurchaseOrderProductDetailFromRequestResponse>();
            purchaseOrderDetailResponse.PurchaseOrderProductDetailFromRequestResponses = purchaseOrderProductDetailFromRequestResponse;
            purchaseOrderDetailResponse.PurchaseOrderNewProductDetailResponses = new List<PurchaseOrderNewProductDetailResponse>();
            res.Status = true;
            res.Data = purchaseOrderDetailResponse;
            return res;
        }

        public async Task<CombineResponseModel<PurchaseOrderFileResponse>> GetPurchaseOrderFileAsync(string email,int purchaseOrderId)
        {
            var domainUrl = _configuration["BlobDomainUrl"];
            var res = new CombineResponseModel<PurchaseOrderFileResponse>();    
            var purchaseOrder = await _unitOfWork.PurchaseOrderRepository.GetByIdAsync(purchaseOrderId);    
            if (purchaseOrder == null)
            {
                res.ErrorMessage = "Không tìm thấy đơn mua hàng";
                return res;
            }
            var names = new List<string>()
            {
                Constant.FirstHrEmail,
                Constant.SecondHrEmail,
                Constant.DirectorEmail
            };
            var emails = await _unitOfWork.GlobalConfigurationRepository.GetByMultiNameAsync(names);
            if (emails.Count == 0)
            {
                res.ErrorMessage = "Người dùng chưa được config";
                return res;
            }
            var isHasAllowEmail = string.Join(",", emails.Select(t => t.Value)).Split(",").Any(y => y.Equals(email, StringComparison.OrdinalIgnoreCase));
            if (!isHasAllowEmail)
            {
                res.ErrorMessage = "Bạn không có quyền xem thông tin này";
                return res;
            }
            var poFiles = purchaseOrder.PurchaseOrderFiles.Select(t =>
            {
                var item = new AttachmentDisPlay()
                {
                    Id = t.Id,
                    Name = domainUrl + "/" + t.FileUrl
                };
                return item;
            }).ToList();
            var responseRaw = await _unitOfWork.PurchaseRequestRepository.GetPurchaseRequestFileAsync(purchaseOrder.Id);
            var requestFiles = responseRaw != null || responseRaw.Count > 0 ? responseRaw.Select(t =>
            {
                var item = new RequestFileResponse
                {
                    RequestDisplay = new IdDisplay()
                    {
                        Id = t.Id,
                        StringId = t.Name
                    },
                    FileUrl = t.FileUrls.Split(",").Distinct().Select(t => domainUrl + "/" + t).ToList()
                };
                return item;
            }).ToList() : new List<RequestFileResponse>();
            var purchaseOrderFileResponse = new PurchaseOrderFileResponse()
            {
                AttachmentDisplays = poFiles,
                RequestDisplays = requestFiles
            };
            res.Status = true;
            res.Data = purchaseOrderFileResponse;
            return res;
        }

        public async Task<PagingResponseModel<PurchaseOrderPagingReponse>> GetPurchaseOrderPagingResponseAsync(PurchaseOrderPagingModel model)
        {
            if (!string.IsNullOrEmpty(model.Keyword))
            {
                model.Keyword = GetIdFromStringId(model.Keyword).ToString();
            }
            var responseRaw = await _unitOfWork.PurchaseOrderRepository.GetPurchaseOrderPagingAsync(model);
            var response = responseRaw.Count > 0 ? responseRaw.Select(t =>
            {
                var item = new PurchaseOrderPagingReponse()
                {
                    CreateDate = t.CreateDate,
                    Departments = !string.IsNullOrEmpty(t.Departments) ? t.Departments.Split(",").Distinct().ToList() : new List<string>(),
                    FromRequest = !string.IsNullOrEmpty(t.FromRequest) ? t.FromRequest.Split(";").Distinct().Select(t =>
                    {
                        var item = JsonConvert.DeserializeObject<IdDisplay>(t);
                        return item;
                    }).ToList() : new List<IdDisplay>(),
                    Id = t.Id,
                    Projects = !string.IsNullOrEmpty(t.Projects) ? t.Projects.Split(",").Distinct().ToList() : new List<string>(),
                    Status = t.Status,
                    StatusName = CommonHelper.GetDescription((EPurchaseOrderStatus)t.Status),
                    IsCompensationPO = t.IsCompensationPO,
                    TotalPrice = (int)Math.Floor((decimal)(t.TotalPrice + t.TotalPrice * t.Vat / 100)),
                    VendorName = t.VendorName,
                    StringId = CommonHelper.ToIdDisplayString("PO", t.Id),
                    TotalRecord = t.TotalRecord ?? 0,
                    AdditionalPOs = !string.IsNullOrEmpty(t.AdditionalPOs) ? t.AdditionalPOs.Split(",").Distinct().Select(t => new IdDisplay()
                    {
                        Id = int.Parse(t),
                        StringId = CommonHelper.ToIdDisplayString("PO",int.Parse(t))
                    }).ToList() : new List<IdDisplay>(),
                    AdditionForPO = t.AdditionForPO != 0 ? new IdDisplay()
                    {
                        Id = t.AdditionForPO,
                        StringId = CommonHelper.ToIdDisplayString("PO",t.AdditionForPO)
                    } : new IdDisplay()
                };
                return item;
            }).ToList() : new List<PurchaseOrderPagingReponse>();
            var totalRecord = response.Count > 0 ? response.FirstOrDefault().TotalRecord : 0;
            var res = new PagingResponseModel<PurchaseOrderPagingReponse>()
            {
                Items = response,
                TotalRecord = totalRecord
            };
            return res;
        }

        public async Task<CombineResponseModel<PurchaseOrder>> ReceiveFullAsync(int id,string email)
        {
            var res = new CombineResponseModel<PurchaseOrder>();
            var hrEmail = await _unitOfWork.GlobalConfigurationRepository.GetByNameAsync(Constant.FirstHrEmail);
            if (hrEmail == null || string.IsNullOrEmpty(hrEmail.Value))
            {
                res.ErrorMessage = "Người dùng chưa được config";
                return res;
            }
            var hr1Email = hrEmail.Value.Split(",").Select(t => t.Trim()).FirstOrDefault(t => t.Equals(email, StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrEmpty(hr1Email))
            {
                res.ErrorMessage = "Bạn không có quyền cập nhật đơn đặt hàng";
                return res;
            }
            var purchaseOrder = await _unitOfWork.PurchaseOrderRepository.GetByIdAsync(id);
            if(purchaseOrder == null)
            {
                res.ErrorMessage = "Không tồn tại đơn đặt hàng";
                return res;
            }
            if(purchaseOrder.Status != (int)EPurchaseOrderStatus.Purchased || purchaseOrder.IsClose)
            {
                res.ErrorMessage = "Không thể nhận hàng cho đơn hàng có trạng thái: " + CommonHelper.GetDescription((EPurchaseOrderStatus)purchaseOrder.Status) + " hoặc đã đóng";
                return res;
            }
            var lineItems = await _unitOfWork.POPRLineItemRepository.GetByPurchareOrderIdAsync(id);
            var isAllowToReceiveFull = lineItems.Any(t => (bool)t.IsReceived && t.Quantity != t.QuantityReceived);
            if (isAllowToReceiveFull)
            {
                res.ErrorMessage = "Trong đơn hàng có sản phẩm nhận thiếu";
                return res;
            }
            if(lineItems.Count > 0)
            {
                lineItems.ForEach(t =>
                {
                    t.QuantityReceived = t.Quantity;
                    t.UpdateDate = DateTime.UtcNow.UTCToIct();
                    t.IsReceived = true;
                });
                await _unitOfWork.POPRLineItemRepository.UpdateRangeAsync(lineItems);
            }
            purchaseOrder.Status = (int)EPurchaseOrderStatus.FullReceived;
            purchaseOrder.IsClose = true;
            purchaseOrder.UpdateDate = DateTime.UtcNow.UTCToIct();
            res.Status = true;
            res.Data = purchaseOrder;
            return res;
        }

        public async Task<CombineResponseModel<PurchaseOrder>> UpdateAsync(int id,string email, PurchaseOrderRequest request)
        {
            var res = new CombineResponseModel<PurchaseOrder>();
            var hrEmail = await _unitOfWork.GlobalConfigurationRepository.GetByNameAsync(Constant.FirstHrEmail);
            if (hrEmail == null || string.IsNullOrEmpty(hrEmail.Value))
            {
                res.ErrorMessage = "Người dùng chưa được config";
                return res;
            }
            var hr1Email = hrEmail.Value.Split(",").Select(t => t.Trim()).FirstOrDefault(t => t.Equals(email, StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrEmpty(hr1Email))
            {
                res.ErrorMessage = "Bạn không có quyền cập nhật đơn đặt hàng";
                return res;
            }
            var vendorId = request.VendorId;
            if (vendorId != 0)
            {
                var vendor = await _unitOfWork.VendorRepository.GetByIdAsync(vendorId);
                if (vendor == null)
                {
                    if (!string.IsNullOrEmpty(request.VendorName))
                    {
                        var vendorCreate = new Vendor()
                        {
                            VendorName = request.VendorName,
                            CreateDate = DateTime.UtcNow.UTCToIct()
                        };
                        await _unitOfWork.VendorRepository.CreateAsync(vendorCreate);
                        await _unitOfWork.SaveChangesAsync();
                        vendorId = vendorCreate.Id;
                    }
                }
            }
            else
            {
                if (!string.IsNullOrEmpty(request.VendorName))
                {
                    var vendorCreate = new Vendor()
                    {
                        VendorName = request.VendorName,
                        CreateDate = DateTime.UtcNow.UTCToIct()
                    };
                    await _unitOfWork.VendorRepository.CreateAsync(vendorCreate);
                    await _unitOfWork.SaveChangesAsync();
                    vendorId = vendorCreate.Id;
                }
            }
            var purchaserOrder = await _unitOfWork.PurchaseOrderRepository.GetByIdAsync(id);
            if( purchaserOrder == null)
            {
                res.ErrorMessage = "Không tồn tại đơn đặt hàng";
                return res;
            }
            purchaserOrder.VendorId = vendorId;
            purchaserOrder.ExpectedDate = request.ExpectedDate;
            purchaserOrder.PaymentMethod = request.PaymentMethod;
            purchaserOrder.BankNumber = request.BankNumber ?? "";
            purchaserOrder.BankName = request.BankName ?? "";
            purchaserOrder.UpdateDate = DateTime.UtcNow.UTCToIct();
            purchaserOrder.Note = request.Note;
            purchaserOrder.Vat = request.Vat;
            res.Status = true;
            res.Data = purchaserOrder;
            return res;
        }
        public async Task<PagingResponseModel<PurchaseOrderPagingReponse>> AccountantGetPurchaseOrderPagingResponseAsync(PurchaseOrderPagingModel model)
        {
            if (!string.IsNullOrEmpty(model.Keyword))
            {
                model.Keyword = GetIdFromStringId(model.Keyword).ToString();
            }
            var responseRaw = await _unitOfWork.PurchaseOrderRepository.GetPurchaseOrderPagingAsync(model);
            var response = responseRaw.Count > 0 ? responseRaw.Select(t =>
            {
                var item = new PurchaseOrderPagingReponse()
                {
                    CreateDate = t.CreateDate,
                    Departments = !string.IsNullOrEmpty(t.Departments) ? t.Departments.Split(",").Distinct().ToList() : new List<string>(),
                    FromRequest = !string.IsNullOrEmpty(t.FromRequest) ? t.FromRequest.Split(";").Distinct().Select(t =>
                    {
                        var item = JsonConvert.DeserializeObject<IdDisplay>(t);
                        return item;
                    }).ToList() : new List<IdDisplay>(),
                    Id = t.Id,
                    Projects = !string.IsNullOrEmpty(t.Projects) ? t.Projects.Split(",").Distinct().ToList() : new List<string>(),
                    Status = t.Status,
                    StatusName = CommonHelper.GetDescription((EPurchaseOrderStatus)t.Status),
                    IsCompensationPO = t.IsCompensationPO,
                    TotalPrice = (int)Math.Floor((decimal)(t.TotalPrice + t.TotalPrice * t.Vat / 100)),
                    VendorName = t.VendorName,
                    StringId = CommonHelper.ToIdDisplayString("PO", t.Id),
                    TotalRecord = t.TotalRecord ?? 0,
                    AdditionalPOs = !string.IsNullOrEmpty(t.AdditionalPOs) ? t.AdditionalPOs.Split(",").Distinct().Select(t => new IdDisplay()
                    {
                        Id = int.Parse(t),
                        StringId = CommonHelper.ToIdDisplayString("PO", int.Parse(t))
                    }).ToList() : new List<IdDisplay>(),
                    AdditionForPO = t.AdditionForPO != 0 ? new IdDisplay()
                    {
                        Id = t.AdditionForPO,
                        StringId = CommonHelper.ToIdDisplayString("PO", t.AdditionForPO)
                    } : new IdDisplay()
                };
                return item;
            }).ToList() : new List<PurchaseOrderPagingReponse>();
            var totalRecord = response.Count > 0 ? response.FirstOrDefault().TotalRecord : 0;
            var res = new PagingResponseModel<PurchaseOrderPagingReponse>()
            {
                Items = response,
                TotalRecord = totalRecord
            };
            return res;
        }
        public async Task<CombineResponseModel<PurchaseOrder>> AccountantReviewAsync(string fullName, string email,int id, PurchaseOrderReviewModel model)
        {
            var res = new CombineResponseModel<PurchaseOrder>();
            var accountantEmails = await _unitOfWork.GlobalConfigurationRepository.GetByNameAsync(Constant.SecondHrEmail);
            var directorEmail = await _unitOfWork.GlobalConfigurationRepository.GetByNameAsync(Constant.DirectorEmail);

            if (accountantEmails == null || string.IsNullOrEmpty(accountantEmails.Value))
            {
                res.ErrorMessage = "Người dùng chưa được config";
                return res;
            }
            var accountantEmail = accountantEmails.Value.Split(",").Select(t=>t.Trim()).FirstOrDefault(y=>y.Equals(email,StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrEmpty(accountantEmail))
            {
                res.ErrorMessage = "Người dùng chưa được config";
                return res;   
            }
            else
            {
                if (!accountantEmail.Equals(email, StringComparison.OrdinalIgnoreCase))
                {
                    res.ErrorMessage = "Bạn không có quyền duyệt";
                    return res;
                }
            }
            var purchaseOrder = await _unitOfWork.PurchaseOrderRepository.GetByIdAsync(id);
            if (purchaseOrder == null)
            {
                res.ErrorMessage = "Không tồn tại đơn đặt hàng";
                return res;
            }
            if (!purchaseOrder.IsCompensationPo)
            {
                res.ErrorMessage = "Không thể duyệt PO không phải PO bù";
                return res;
            }
            if(purchaseOrder.Status == (int)EPurchaseOrderStatus.AccountAccept || purchaseOrder.Status == (int)EPurchaseOrderStatus.DirectorAccept 
                || purchaseOrder.Status == (int)EPurchaseOrderStatus.Reject)
            {
                res.ErrorMessage = "Không thể duyệt đơn đã duyệt trước đó";
                return res;
            }
            var poTotalPrice = await _unitOfWork.PurchaseOrderRepository.GetPurchaseOrderTotalPriceAsync(purchaseOrder.Id);
            var poPrice = poTotalPrice.FirstOrDefault() != null ? poTotalPrice.FirstOrDefault().TotalPriceWithVat : 0;
            var sumPaymentPlan = purchaseOrder.PaymentPlans.Sum(t => t.PaymentAmount);
            if((int)Math.Floor(sumPaymentPlan) != (int)Math.Floor(poPrice))
            {
                res.ErrorMessage = "Số tiền các đợt thanh toán chưa đầy đủ";
                return res;
            }
            if (model.IsAccept)
            {
                purchaseOrder.Status = (int)EPurchaseOrderStatus.AccountAccept;
                purchaseOrder.UpdateDate = DateTime.UtcNow.UTCToIct();
            }
            else
            {
                if (string.IsNullOrEmpty(model.RejectReason))
                {
                    res.ErrorMessage = "Lý do từ chối không được trống";
                    return res;
                }
                purchaseOrder.Status = (int)EPurchaseOrderStatus.Reject;
                purchaseOrder.UpdateDate = DateTime.UtcNow.UTCToIct();
                purchaseOrder.RejectReason = model.RejectReason;
                var poprLineItems = await _unitOfWork.POPRLineItemRepository.GetByPurchareOrderIdAsync(purchaseOrder.Id);
                await _unitOfWork.POPRLineItemRepository.DeleteRangeAsync(poprLineItems);
                await _unitOfWork.PODetailRepository.DeleteRangeAsync(purchaseOrder.Podetails.ToList());
                if (purchaseOrder.PurchaseOrderFiles.Count > 0)
                {
                    await _unitOfWork.PurchaseOrderFileRepository.DeleteRangeAsync(purchaseOrder.PurchaseOrderFiles.ToList());
                }
                if (purchaseOrder.PaymentPlans.Count > 0)
                {
                    await _unitOfWork.PaymentPlanRepository.DeleteRangeAsync(purchaseOrder.PaymentPlans.ToList());
                }
            }
            //gửi mail cho người tạo po
            var purchaseOrderSendMail = new PurchaseOrderReviewSendMail()
            {
                CreateUser = purchaseOrder.CreateUser.FullName,
                POId = CommonHelper.ToIdDisplayString("PO",purchaseOrder.Id),
                CreateDate = purchaseOrder.CreateDate,
                ReviewResult = CommonHelper.GetDescription((EPurchaseOrderStatus)purchaseOrder.Status),
                Reviewer = fullName,
                ReasonReject = purchaseOrder.RejectReason ?? string.Empty
            };

            ObjSendMail objSendMail = new()
            {
                FileName = "PurchaseOrderReviewTemplate.html",
                Mail_To = new List<string>() {purchaseOrder.CreateUser.Email},
                Title = "[Mua hàng] Kết quả duyệt cho đơn mua hàng: " + CommonHelper.ToIdDisplayString("PO", purchaseOrder.Id),
                Mail_cc = [],
                JsonObject = JsonConvert.SerializeObject(purchaseOrderSendMail)
            };
            await _sendMailDynamicTemplateService.SendMailAsync(objSendMail);
            //nếu đồng ý thì gửi cho mail cho giám đốc
            if (model.IsAccept)
            {
                var purchaseOrderDirectorSendMail = new PurchaseOrderReviewSendMail()
                {
                    CreateUser = purchaseOrder.CreateUser.FullName,
                    POId = CommonHelper.ToIdDisplayString("PO", purchaseOrder.Id),
                    CreateDate = purchaseOrder.CreateDate,
                    ReviewResult = CommonHelper.GetDescription((EPurchaseOrderStatus)purchaseOrder.Status),
                    Reviewer = fullName
                };
                ObjSendMail objDirectorSendMail = new()
                {
                    FileName = "PurchaseOrderDirectorReviewTemplate.html",
                    Mail_To =  directorEmail != null && !string.IsNullOrEmpty(directorEmail.Value) ? directorEmail.Value.Split(",").Select(t=>t.Trim()).ToList() : new List<string>() {},
                    Title = "[Mua hàng] Yêu cầu duyệt cho đơn mua hàng: " + CommonHelper.ToIdDisplayString("PO", purchaseOrder.Id),
                    Mail_cc = [],
                    JsonObject = JsonConvert.SerializeObject(purchaseOrderDirectorSendMail)
                };
                await _sendMailDynamicTemplateService.SendMailAsync(objDirectorSendMail);
            }
            res.Status = true;
            res.Data = purchaseOrder;
            return res;
        }
        public async Task<PagingResponseModel<PurchaseOrderPagingReponse>> DirectorGetPurchaseOrderPagingResponseAsync(PurchaseOrderPagingModel model)
        {
            if (!string.IsNullOrEmpty(model.Keyword))
            {
                model.Keyword = GetIdFromStringId(model.Keyword).ToString();
            }
            var responseRaw = await _unitOfWork.PurchaseOrderRepository.GetPurchaseOrderPagingAsync(model);
            var response = responseRaw.Count > 0 ? responseRaw.Select(t =>
            {
                var item = new PurchaseOrderPagingReponse()
                {
                    CreateDate = t.CreateDate,
                    Departments = !string.IsNullOrEmpty(t.Departments) ? t.Departments.Split(",").Distinct().ToList() : new List<string>(),
                    FromRequest = !string.IsNullOrEmpty(t.FromRequest) ? t.FromRequest.Split(";").Distinct().Select(t =>
                    {
                        var item = JsonConvert.DeserializeObject<IdDisplay>(t);
                        return item;
                    }).ToList() : new List<IdDisplay>(),
                    Id = t.Id,
                    Projects = !string.IsNullOrEmpty(t.Projects) ? t.Projects.Split(",").Distinct().ToList() : new List<string>(),
                    Status = t.Status,
                    StatusName = CommonHelper.GetDescription((EPurchaseOrderStatus)t.Status),
                    IsCompensationPO = t.IsCompensationPO,
                    TotalPrice = (int)Math.Floor((decimal)(t.TotalPrice + t.TotalPrice * t.Vat / 100)),
                    VendorName = t.VendorName,
                    StringId = CommonHelper.ToIdDisplayString("PO", t.Id),
                    TotalRecord = t.TotalRecord ?? 0,
                    AdditionalPOs = !string.IsNullOrEmpty(t.AdditionalPOs) ? t.AdditionalPOs.Split(",").Distinct().Select(t => new IdDisplay()
                    {
                        Id = int.Parse(t),
                        StringId = CommonHelper.ToIdDisplayString("PO", int.Parse(t))
                    }).ToList() : new List<IdDisplay>(),
                    AdditionForPO = t.AdditionForPO != 0 ? new IdDisplay()
                    {
                        Id = t.AdditionForPO,
                        StringId = CommonHelper.ToIdDisplayString("PO", t.AdditionForPO)
                    } : new IdDisplay()
                };
                return item;
            }).ToList() : new List<PurchaseOrderPagingReponse>();
            var totalRecord = response.Count > 0 ? response.FirstOrDefault().TotalRecord : 0;
            var res = new PagingResponseModel<PurchaseOrderPagingReponse>()
            {
                Items = response,
                TotalRecord = totalRecord
            };
            return res;
        }
        public async Task<CombineResponseModel<PurchaseOrder>> DirectorReviewAsync(string fullName, string email, int id, PurchaseOrderReviewModel model)
        {
            var res = new CombineResponseModel<PurchaseOrder>();
            var directorEmail = await _unitOfWork.GlobalConfigurationRepository.GetByNameAsync(Constant.DirectorEmail);
            if (directorEmail == null || string.IsNullOrEmpty(directorEmail.Value))
            {
                res.ErrorMessage = "Người dùng chưa được config";
            }
            var accountantEmail = directorEmail.Value.Split(",").Select(t => t.Trim()).FirstOrDefault(y => y.Equals(email, StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrEmpty(accountantEmail))
            {
                res.ErrorMessage = "Người dùng chưa được config";
            }
            else
            {
                if (!accountantEmail.Equals(email, StringComparison.OrdinalIgnoreCase))
                {
                    res.ErrorMessage = "Bạn không có quyền duyệt";
                }
            }
            var purchaseOrder = await _unitOfWork.PurchaseOrderRepository.GetByIdAsync(id);
            if (purchaseOrder == null)
            {
                res.ErrorMessage = "Không tồn tại đơn đặt hàng";
                return res;
            }
            if (!purchaseOrder.IsCompensationPo)
            {
                res.ErrorMessage = "Không thể duyệt PO không phải PO bù";
                return res;
            }
            if (purchaseOrder.Status != (int)EPurchaseOrderStatus.AccountAccept)
            {
                res.ErrorMessage = "Không thể duyệt đơn mà Accountant chưa duyệt";
                return res;
            }
            var poTotalPrice = await _unitOfWork.PurchaseOrderRepository.GetPurchaseOrderTotalPriceAsync(purchaseOrder.Id);
            var poPrice = poTotalPrice.FirstOrDefault() != null ? poTotalPrice.FirstOrDefault().TotalPriceWithVat : 0;
            var sumPaymentPlan = purchaseOrder.PaymentPlans.Sum(t => t.PaymentAmount);
            if ((int)Math.Floor(sumPaymentPlan) != (int)Math.Floor(poPrice))
            {
                res.ErrorMessage = "Số tiền các đợt thanh toán chưa đầy đủ";
                return res;
            }
            if (model.IsAccept)
            {
                purchaseOrder.Status = (int)EPurchaseOrderStatus.DirectorAccept;
                purchaseOrder.UpdateDate = DateTime.UtcNow.UTCToIct();
            }
            else
            {
                if (string.IsNullOrEmpty(model.RejectReason))
                {
                    res.ErrorMessage = "Lý do từ chối không được trống";
                    return res;
                }
                purchaseOrder.Status = (int)EPurchaseOrderStatus.Reject;
                purchaseOrder.UpdateDate = DateTime.UtcNow.UTCToIct();
                purchaseOrder.RejectReason = model.RejectReason;
                var poprLineItems = await _unitOfWork.POPRLineItemRepository.GetByPurchareOrderIdAsync(purchaseOrder.Id);
                await _unitOfWork.POPRLineItemRepository.DeleteRangeAsync(poprLineItems);
                await _unitOfWork.PODetailRepository.DeleteRangeAsync(purchaseOrder.Podetails.ToList());
            }
            var purchaseOrderSendMail = new PurchaseOrderReviewSendMail()
            {
                CreateUser = purchaseOrder.CreateUser.FullName,
                POId = CommonHelper.ToIdDisplayString("PO", purchaseOrder.Id),
                CreateDate = purchaseOrder.CreateDate,
                ReviewResult = CommonHelper.GetDescription((EPurchaseOrderStatus)purchaseOrder.Status),
                Reviewer = fullName,
                ReasonReject = purchaseOrder.RejectReason ?? string.Empty
            };

            ObjSendMail objSendMail = new()
            {
                FileName = "PurchaseOrderReviewTemplate.html",
                Mail_To = new List<string>() { purchaseOrder.CreateUser.Email },
                Title = "[Mua hàng] Kết quả duyệt cho đơn mua hàng: " + CommonHelper.ToIdDisplayString("PO", purchaseOrder.Id),
                Mail_cc = [],
                JsonObject = JsonConvert.SerializeObject(purchaseOrderSendMail)
            };
            await _sendMailDynamicTemplateService.SendMailAsync(objSendMail);
            res.Status = true;
            res.Data = purchaseOrder;
            return res;
        }
    }
}
