using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Enums;
using InternalPortal.ApplicationCore.Helpers;
using InternalPortal.ApplicationCore.Interfaces;
using InternalPortal.ApplicationCore.Interfaces.Business;
using InternalPortal.ApplicationCore.Models;
using InternalPortal.ApplicationCore.Models.ExportBill;
using InternalPortal.ApplicationCore.Models.PurchaseOrder.PurchaseRequest;
using Newtonsoft.Json;
using static InternalPortal.ApplicationCore.ValueObjects.Global;

namespace InternalPortal.Infrastructure.Services.Business
{
    public class ExportBillService : IExportBillService
    {
        private readonly IUnitOfWork _unitOfWork;
        public ExportBillService(IUnitOfWork unitOfWork)
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
        public async Task<CombineResponseModel<ExportBill>> AddItemFromRequestAsync(int exportBillId, ItemCreateRequest request)
        {
            var res = new CombineResponseModel<ExportBill>();
            var exportBillDetails = await _unitOfWork.ExportBillDetailRepository.GetByExportBillIdAsync(exportBillId);
            var exportBillLineItems = await _unitOfWork.ExportBillLineItemRepository.GetByExportBillIdAsync(exportBillId);
            var lineItemFromRequest = await _unitOfWork.PurchaseRequestLineItemRepository.GetByPurchaseRequestIdAsync(request.RequestId);
            var lineItemIdsFromRequest = string.Join(",", lineItemFromRequest.Select(t => t.Id)).Split(",").ToList();
            var exportBill = await _unitOfWork.ExportBillRepository.GetByIdAsync(exportBillId);
            if(exportBill == null)
            {
                res.ErrorMessage = "Không tồn tại phiếu xuất";
                return res;
            }
            if(exportBill.Status != (int)EExportBillStatus.Pending && exportBill.Status == (int)EExportBillStatus.UpdateRequest)
            {
                res.ErrorMessage = "Không thể thêm sản phẩm cho phiếu xuất có trạng thái: " + CommonHelper.GetDescription((EExportBillStatus)exportBill.Status);
                return res;
            }
            if ((bool)exportBill.IsExport)
            {
                res.ErrorMessage = "Không thể cập nhật phiếu đã xuất kho";
                return res;
            }
            if (string.IsNullOrEmpty(request.LineItemIds)) //không check item nào
            {
                if (exportBillLineItems.Any())
                {
                    foreach (var id in lineItemIdsFromRequest)
                    {
                        var exportBillLineItemExist = exportBillLineItems.FirstOrDefault(t => t.PorequestLineItemId == int.Parse(id));
                        if (exportBillLineItemExist != null)
                        {
                            var exportBillDetailDelete = exportBillDetails.FirstOrDefault(t => t.Id == exportBillLineItemExist.ExportBillDetailId && t.ExportBillLineItems.Count == 1);
                            if (exportBillDetailDelete != null)//nếu exportBillDetail chỉ có 1 item 
                            {
                                await _unitOfWork.ExportBillLineItemRepository.DeleteAsync(exportBillLineItemExist);
                                await _unitOfWork.ExportBillDetailRepository.DeleteAsync(exportBillDetailDelete);
                            }
                            else
                            {
                                await _unitOfWork.ExportBillLineItemRepository.DeleteAsync(exportBillLineItemExist);
                            }
                        }
                    }
                    exportBillLineItems = exportBillLineItems.Where(t => !lineItemIdsFromRequest.Contains(t.PorequestLineItemId.ToString())).ToList();
                }
            }
            else
            {
                var selectItems = request.LineItemIds.Split(",").ToList();//Lấy các item được chọn
                var existItem = exportBillLineItems.Where(t => lineItemIdsFromRequest.Contains(t.PorequestLineItemId.ToString()));//tìm những item đã được check trước đó
                var itemCreate = new List<ItemCreateResponse>();
                if (existItem.Any())
                {
                    var lineItemsIdsExist = string.Join(",", existItem.Select(t => t.PorequestLineItemId)).Split(",").ToList();//lấy những id được check trước đó
                    var insertItems = selectItems.Where(t => !lineItemsIdsExist.Contains(t)).ToList();//lấy những item chưa check
                    var deleteItem = lineItemsIdsExist.Where(t => !selectItems.Contains(t));//lấy các item đã check trước đó nhưng giờ không check nữa
                    if (insertItems.Any())
                    {
                        var selectLineItemIds = string.Join(",", insertItems);
                        itemCreate = await _unitOfWork.PurchaseRequestLineItemRepository.GetItemCreateForEBResponse(selectLineItemIds,exportBillId);   
                        itemCreate = itemCreate.Where(t=>t.RemainQty != 0).ToList();
                        if (itemCreate.Any())
                        {
                            foreach (var item in itemCreate)
                            {
                                var detailExist = exportBillDetails.FirstOrDefault(t => t.ExportBillId == exportBillId && t.ProductId == item.ProductId);
                                var newExportBillLineItem = new ExportBillLineItem()
                                {
                                    PorequestLineItemId = item.Id,
                                    Quantity = item.RemainQty,
                                    CreateDate = DateTime.UtcNow.UTCToIct()
                                };
                                if (detailExist == null)
                                {
                                    var newExportBillDetail = new ExportBillDetail()
                                    {
                                        ExportBillId = exportBillId,
                                        ProductId = item.ProductId,
                                        Quantity = item.RemainQty,
                                        SubProductId = null,
                                        CreateDate = DateTime.UtcNow.UTCToIct(),
                                        ExportDate = DateTime.UtcNow.UTCToIct()
                                    };
                                    newExportBillDetail.ExportBillLineItems.Add(newExportBillLineItem);
                                    await _unitOfWork.ExportBillDetailRepository.CreateAsync(newExportBillDetail);
                                    exportBillDetails.Add(newExportBillDetail);
                                }
                                else
                                {
                                    detailExist.UpdateDate = DateTime.UtcNow.UTCToIct();
                                    newExportBillLineItem.ExportBillDetailId = detailExist.Id;
                                    exportBillLineItems.Add(newExportBillLineItem);
                                    await _unitOfWork.ExportBillLineItemRepository.CreateAsync(newExportBillLineItem);
                                }
                            }
                        }
                    }
                    if (deleteItem.Any())
                    {
                        foreach (var id in deleteItem)
                        {
                            var lineItemExist = exportBillLineItems.FirstOrDefault(t => t.PorequestLineItemId == int.Parse(id));
                            var exportBillDetailDelete = exportBillDetails.FirstOrDefault(t => t.Id == lineItemExist.ExportBillDetailId && t.ExportBillLineItems.Count == 1);
                            if (exportBillDetailDelete != null)//nếu detail chỉ có 1 item 
                            {
                                await _unitOfWork.ExportBillLineItemRepository.DeleteAsync(lineItemExist);
                                await _unitOfWork.ExportBillDetailRepository.DeleteAsync(exportBillDetailDelete);
                            }
                            else
                            {
                                await _unitOfWork.ExportBillLineItemRepository.DeleteAsync(lineItemExist);
                            }
                        }
                    }
                }
                else
                {
                    itemCreate = await _unitOfWork.PurchaseRequestLineItemRepository.GetItemCreateForEBResponse(request.LineItemIds,exportBillId);
                    itemCreate = itemCreate.Where(t => t.RemainQty != 0).ToList();
                    if (itemCreate.Any())
                    {
                        foreach (var item in itemCreate)
                        {
                            var detailExist = exportBillDetails.FirstOrDefault(t => t.ExportBillId == exportBillId && t.ProductId == item.ProductId);
                            var newExportBillLineItem = new ExportBillLineItem()
                            {
                                PorequestLineItemId = item.Id,
                                Quantity = item.RemainQty,
                                CreateDate = DateTime.UtcNow.UTCToIct()
                            };
                            if (detailExist == null)
                            {
                                var newExportBillDetail = new ExportBillDetail()
                                {
                                    ExportBillId = exportBillId,
                                    ProductId = item.ProductId,
                                    Quantity = item.RemainQty,
                                    SubProductId = null,
                                    CreateDate = DateTime.UtcNow.UTCToIct(),
                                    ExportDate = DateTime.UtcNow.UTCToIct()
                                };
                                newExportBillDetail.ExportBillLineItems.Add(newExportBillLineItem);
                                await _unitOfWork.ExportBillDetailRepository.CreateAsync(newExportBillDetail);
                                exportBillDetails.Add(newExportBillDetail);
                            }
                            else
                            {
                                detailExist.UpdateDate = DateTime.UtcNow.UTCToIct();
                                newExportBillLineItem.ExportBillDetailId = detailExist.Id;
                                exportBillLineItems.Add(newExportBillLineItem);
                                await _unitOfWork.ExportBillLineItemRepository.CreateAsync(newExportBillLineItem);
                            }
                        }
                    }
                }
            }
            var group = exportBillLineItems.GroupBy(t => t.ExportBillDetailId).Select(t=> new
            {
                Id = t.Key,
                TotalQty = t.ToList().Sum(t=>t.Quantity)
            });
            foreach(var item in group)
            {
                var exportBillDetailExists = exportBillDetails.FirstOrDefault(t=>t.Id == item.Id);
                exportBillDetailExists.Quantity = item.TotalQty;
            }
            await _unitOfWork.ExportBillLineItemRepository.UpdateRangeAsync(exportBillLineItems);
            await _unitOfWork.ExportBillDetailRepository.UpdateRangeAsync(exportBillDetails);
            await _unitOfWork.SaveChangesAsync();
            res.Status = true;
            return res;
        }

        public async Task<CombineResponseModel<ExportBill>> DeleteAsync(int id, string email)
        {
            var res = new CombineResponseModel<ExportBill>();
            var hrEmail = await _unitOfWork.GlobalConfigurationRepository.GetByNameAsync(Constant.FirstHrEmail);
            if (hrEmail == null || string.IsNullOrEmpty(hrEmail.Value))
            {
                res.ErrorMessage = "Người dùng chưa được config";
                return res;
            }
            var hr1Email = hrEmail.Value.Split(",").Select(t => t.Trim()).FirstOrDefault(t => t.Equals(email, StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrEmpty(hr1Email))
            {
                res.ErrorMessage = "Bạn không có quyền xóa phiếu xuất";
                return res;
            }
            var exportBill = await _unitOfWork.ExportBillRepository.GetByIdAsync(id);
            if(exportBill == null)
            {
                res.ErrorMessage = "Không tồn tại phiếu xuất";
                return res;
            }
            var isExport = exportBill.IsExport ?? false;
            if (isExport)
            {
                res.ErrorMessage = "Không thể xóa phiếu đã xuất kho";
                return res;
            }
            var lineItems = await _unitOfWork.ExportBillLineItemRepository.GetByExportBillIdAsync(exportBill.Id);
            var exportBillDetails = await _unitOfWork.ExportBillDetailRepository.GetByExportBillIdAsync(exportBill.Id);
            if(lineItems.Count > 0)
            {
                await _unitOfWork.ExportBillLineItemRepository.DeleteRangeAsync(lineItems);
            }
            if(exportBillDetails.Count > 0)
            {
                await _unitOfWork.ExportBillDetailRepository.DeleteRangeAsync(exportBillDetails);
            }
            res.Status = true;
            res.Data = exportBill;
            return res;
        }

        public async Task<CombineResponseModel<ExportBill>> CreateAsync(int userId, string email,ExportBillRequest request)
        {
            var res = new CombineResponseModel<ExportBill>();
            var hrEmail = await _unitOfWork.GlobalConfigurationRepository.GetByNameAsync(Constant.FirstHrEmail);
            if (hrEmail == null || string.IsNullOrEmpty(hrEmail.Value))
            {
                res.ErrorMessage = "Người dùng chưa được config";
                return res;
            }
            var hr1Email = hrEmail.Value.Split(",").Select(t => t.Trim()).FirstOrDefault(t => t.Equals(email, StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrEmpty(hr1Email))
            {
                res.ErrorMessage = "Bạn không có quyền tạo phiếu xuất";
                return res;
            }
            var exportBill = new ExportBill()
            {
                CreateUserId = userId,
                CreateDate = DateTime.UtcNow.UTCToIct(),
                ExportDate = DateTime.UtcNow.UTCToIct(),
                Note = request.Note ?? string.Empty,
                Status = (int)EExportBillStatus.Pending,
                IsExport = false
            };
            res.Status = true;
            res.Data =exportBill;
            return res;
        }

        public async Task<CombineResponseModel<ExportBill>> CreateExportBillFromRequestAsync(int id, int userId, string email)
        {
            var res = new CombineResponseModel<ExportBill>();
            var request = await _unitOfWork.PurchaseRequestRepository.GetByIdAsync(id);
            if (request == null)
            {
                res.ErrorMessage = "Không tồn tại yêu cầu";
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
                res.ErrorMessage = "Bạn không có quyền tạo phiếu xuất";
                return res;
            }
            var exportBill = new ExportBill()
            {
                CreateUserId = userId,
                CreateDate = DateTime.UtcNow.UTCToIct(),
                ExportDate = DateTime.UtcNow.UTCToIct(),
                Status = (int)EExportBillStatus.Pending,
                IsExport = false
            };
            var exportBillDetails = new List<ExportBillDetail>();
            var purchaseRequestLineItemIds = string.Join(",", request.PurchaseRequestLineItems.Select(t => t.Id.ToString()));
            //lấy ra những item chưa tạo xong
            var itemCreateResponse = await _unitOfWork.PurchaseRequestLineItemRepository.GetItemCreateForEBResponse(purchaseRequestLineItemIds, 0);
            itemCreateResponse = itemCreateResponse.Where(t => t.RemainQty != 0).ToList();
            if (itemCreateResponse.Count == 0)
            {
                res.ErrorMessage = "Các mặt hàng trong yêu cầu đã đặt đủ không thể tạo phiếu xuất";
                return res;
            }
            foreach(var item in itemCreateResponse)
            {
                var exportBillDetail = new ExportBillDetail()
                {
                    ProductId = item.ProductId,
                    Quantity = item.RemainQty,
                    CreateDate = DateTime.UtcNow.UTCToIct(),
                    ExportDate = DateTime.UtcNow.UTCToIct(),
                    SubProductId = null,
                };
                var exportBillLineItems = new List<ExportBillLineItem>();
                var exportBillLineItem = new ExportBillLineItem()
                {
                    PorequestLineItemId = item.Id,
                    Quantity = item.RemainQty,
                    CreateDate = DateTime.UtcNow.UTCToIct()
                };
                exportBillLineItems.Add(exportBillLineItem);
                exportBillDetail.ExportBillLineItems = exportBillLineItems;
                exportBillDetails.Add(exportBillDetail);
            }
            exportBill.ExportBillDetails = exportBillDetails;
            res.Status = true;
            res.Data = exportBill;
            return res;
        }

        public async Task<PagingResponseModel<ExportBillPagingResponse>> GetAllWithPagingAsync(ExportBillPagingModel model)
        {
            if (!string.IsNullOrEmpty(model.Keyword))
            {
                model.Keyword = GetIdFromStringId(model.Keyword).ToString();
            }
            var responseRaw = await _unitOfWork.ExportBillRepository.GetAllWithPagingAsync(model);
            var response = responseRaw.Count > 0 ? responseRaw.Select(t=> new ExportBillPagingResponse
            {
                Id = t.Id,
                StringId = CommonHelper.ToIdDisplayString("XK",t.Id),
                CreateDate = t.CreateDate,  
                Departments = t.Departments,
                FullName = t.FullName,
                Projects = t.Projects,
                Status = t.Status,  
                StatusName = CommonHelper.GetDescription((EExportBillStatus)t.Status),
                IsExport = (bool)(t.IsExport != null ? t.IsExport : false),
                TotalQuantity = t.TotalQuantity,    
                TotalRecord = t.TotalRecord,
                FromRequests = !string.IsNullOrEmpty(t.FromRequests) ? t.FromRequests.Split(";").Distinct().Select(t =>
                {
                    var item = JsonConvert.DeserializeObject<IdDisplay>(t);
                    return item;
                }).ToList() : new List<IdDisplay>()
            }).ToList() : new List<ExportBillPagingResponse>();
            var totalRecord = response.Count > 0 ? response.FirstOrDefault().TotalRecord : 0;
            var res = new PagingResponseModel<ExportBillPagingResponse>()
            {
                Items = response,
                TotalRecord = totalRecord
            };
            return res;
        }

        public async Task<CombineResponseModel<ExportBillRespone>> GetExportBillDetailAsync(string email,int exportBillId)
        {
            var res = new CombineResponseModel<ExportBillRespone>();
            var exportBill = await _unitOfWork.ExportBillRepository.GetByIdAsync(exportBillId);
            if(exportBill == null)
            {
                res.ErrorMessage = "Không tồn tại phiếu xuất";
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
            var exportBillReponse = new ExportBillRespone()
            {
                Id = exportBill.Id,
                StringId = CommonHelper.ToIdDisplayString("XK",exportBill.Id),
                CreateDate = exportBill.CreateDate,
                FullName = exportBill.CreateUser.FullName ?? exportBill.CreateUser.Name,
                Note = exportBill.Note,
                Status = exportBill.Status,
                IsExport = exportBill.IsExport != null ? exportBill.IsExport : false,
            };
            var exportBillResponses = await _unitOfWork.ExportBillRepository.GetExportBillDetailAsync(exportBill.Id);
            var departments = exportBillResponses.Count > 0 ? string.Join(",", string.Join(",", exportBillResponses.Select(t => t.Departments)).Split(",").Distinct()) : string.Empty;
            var projects = exportBillResponses.Count > 0 ? string.Join(",", string.Join(",", exportBillResponses.Select(t => t.Projects)).Split(",").Distinct()) : string.Empty;
            exportBillReponse.Departments = departments;
            exportBillReponse.Projects = projects;
            var exportBillDetailResponses = exportBillResponses.Count > 0 ? exportBillResponses.Select(t => new ExportBillDetailResponse()
            {
                Id = t.Id,
                FromRequests = !string.IsNullOrEmpty(t.FromRequests) ? t.FromRequests.Split(";").Distinct().Select(t =>
                {
                    var item = JsonConvert.DeserializeObject<IdDisplay>(t);
                    return item;
                }).ToList() : new List<IdDisplay>(),
                CategoryName = t.CategoryName,
                Name = t.Name,
                Detail = !string.IsNullOrEmpty(t.Detail) ? t.Detail.Split(",").ToList() : new List<string>(),
                KitQuantity = !string.IsNullOrEmpty(t.KitQuantity) ? t.KitQuantity.Split(",").ToList() : new List<string>(),
                TotalRequestQty = t.TotalRequestQty,
                Description = t.Description,
                FromPOs = !string.IsNullOrEmpty(t.FromPOs) ? t.FromPOs.Split(",").Distinct().Select(t =>
                {
                    var item = new IdDisplay()
                    {
                        Id = int.Parse(t),
                        StringId = CommonHelper.ToIdDisplayString("PO", int.Parse(t))
                    };
                    return item;
                }).ToList() : new List<IdDisplay>(),
                FromOtherEBs = !string.IsNullOrEmpty(t.FromOtherEBs) ? t.FromOtherEBs.Split(",").Distinct().Select(t =>
                {
                    var item = new IdDisplay()
                    {
                        Id = int.Parse(t),
                        StringId = CommonHelper.ToIdDisplayString("XK", int.Parse(t))
                    };
                    return item;
                }).ToList() : new List<IdDisplay>(),
                RemainQty = t.TotalRequestQty - t.TotalQtyFromPO - t.TotalQtyFromOtherEB,
                Quantity = t.Quantity,
                Departments = !string.IsNullOrEmpty(t.Departments) ? string.Join(",",t.Departments.Split(",").Distinct()) : string.Empty,
                Projects = !string.IsNullOrEmpty(t.Projects) ? string.Join(",",t.Projects.Split(",").Distinct()) : string.Empty,
                IsHasPrApprove = !string.IsNullOrEmpty(t.ReviewStatuses) ? t.ReviewStatuses.Split(",").Any(t=>int.Parse(t) == (int)EPurchaseRequestStatus.AccountantApproved
                || int.Parse(t) == (int)EPurchaseRequestStatus.DirectorApproved) : false
            }).ToList() : new List<ExportBillDetailResponse>();
            exportBillReponse.ExportBillDetailResponses = exportBillDetailResponses;
            res.Status = true;
            res.Data = exportBillReponse;
            return res;
        }

        public async Task<CombineResponseModel<List<ExportBillLineItemResponse>>> GetExportBillLineItemAsync(string email,int exportBillDetailId)
        {
            var res = new CombineResponseModel<List<ExportBillLineItemResponse>>();
            var exportBillDetail = await _unitOfWork.ExportBillDetailRepository.GetByIdAsync(exportBillDetailId);
            if(exportBillDetail == null)
            {
                res.ErrorMessage = "Không tồn tại chi tiết của phiếu xuất";
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
            var exportBillLineItemRaw = await _unitOfWork.ExportBillRepository.GetExportBillLineItemAsync(exportBillDetail.Id, exportBillDetail.ExportBillId);
            var exportBillLineItemResponse = exportBillLineItemRaw.Count > 0 ? exportBillLineItemRaw.Select(t => new ExportBillLineItemResponse()
            {
                Id = t.Id,
                RequestId = t.RequestId,
                Name = t.Name,
                RequestQty = t.RequestQty,
                PODisplay = !string.IsNullOrEmpty(t.FromPOs) ? t.FromPOs.Split(",").Distinct().Select(t =>
                {
                    var item = new IdDisplay()
                    {
                        Id = int.Parse(t),
                        StringId = CommonHelper.ToIdDisplayString("PO",int.Parse(t))
                    };
                    return item;
                }).ToList() : new List<IdDisplay>(),
                OtherEBDisplay = !string.IsNullOrEmpty(t.FromOtherEBs) ? t.FromOtherEBs.Split(",").Distinct().Select(t =>
                {
                    var item = new IdDisplay()
                    {
                        Id = int.Parse(t),
                        StringId = CommonHelper.ToIdDisplayString("XK", int.Parse(t))
                    };
                    return item;
                }).ToList() : new List<IdDisplay>(),
                RemainQty = t.RequestQty - t.FromPOQty - t.ExportQty,
                Quantity = t.Quantity,
                ReviewStatus = t.ReviewStatus,
                StatusName = CommonHelper.GetDescription((EPurchaseRequestStatus)t.ReviewStatus)
            }).ToList() : new List<ExportBillLineItemResponse>();
            res.Status = true;
            res.Data = exportBillLineItemResponse;
            return res;
        }

        public async Task<CombineResponseModel<ExportBill>> UpdateAsync(int id,string email, ExportBillRequest request)
        {
            var res = new CombineResponseModel<ExportBill>();
            var hrEmail = await _unitOfWork.GlobalConfigurationRepository.GetByNameAsync(Constant.FirstHrEmail);
            if (hrEmail == null || string.IsNullOrEmpty(hrEmail.Value))
            {
                res.ErrorMessage = "Người dùng chưa được config";
                return res;
            }
            var hr1Email = hrEmail.Value.Split(",").Select(t => t.Trim()).FirstOrDefault(t => t.Equals(email, StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrEmpty(hr1Email))
            {
                res.ErrorMessage = "Bạn không có quyền cập nhật phiếu xuất";
                return res;
            }
            var exportBill = await _unitOfWork.ExportBillRepository.GetByIdAsync(id);
            if (exportBill == null)
            {
                res.ErrorMessage = "Không tồn tại phiếu xuất";
                return res;
            }
            var isExport = exportBill.IsExport ?? false;
            if (isExport)
            {
                res.ErrorMessage = "Không thể cập nhật phiếu đã xuất kho";
                return res;
            }
            exportBill.Note = request.Note ?? string.Empty;
            exportBill.UpdateDate = DateTime.UtcNow.UTCToIct();
            res.Status = true;
            res.Data = exportBill;
            return res;
        }
        public async Task<PagingResponseModel<ExportBillPagingResponse>> AccountantGetAllWithPagingAsync(ExportBillPagingModel model)
        {
            if (!string.IsNullOrEmpty(model.Keyword))
            {
                model.Keyword = GetIdFromStringId(model.Keyword).ToString();
            }
            var responseRaw = await _unitOfWork.ExportBillRepository.GetAllWithPagingAsync(model);
            var response = responseRaw.Count > 0 ? responseRaw.Select(t => new ExportBillPagingResponse
            {
                Id = t.Id,
                StringId = CommonHelper.ToIdDisplayString("XK", t.Id),
                CreateDate = t.CreateDate,
                Departments = t.Departments,
                FullName = t.FullName,
                Projects = t.Projects,
                Status = t.Status,
                StatusName = CommonHelper.GetDescription((EExportBillStatus)t.Status),
                TotalQuantity = t.TotalQuantity,
                TotalRecord = t.TotalRecord,
                IsExport = (bool)(t.IsExport != null ? t.IsExport : false),
                FromRequests = !string.IsNullOrEmpty(t.FromRequests) ? t.FromRequests.Split(";").Distinct().Select(t =>
                {
                    var item = JsonConvert.DeserializeObject<IdDisplay>(t);
                    return item;
                }).ToList() : new List<IdDisplay>()
            }).ToList() : new List<ExportBillPagingResponse>();
            var totalRecord = response.Count > 0 ? response.FirstOrDefault().TotalRecord : 0;
            var res = new PagingResponseModel<ExportBillPagingResponse>()
            {
                Items = response,
                TotalRecord = totalRecord
            };
            return res;
        }
        public async Task<PagingResponseModel<ExportBillPagingResponse>> DirectorGetAllWithPagingAsync(ExportBillPagingModel model)
        {
            if (!string.IsNullOrEmpty(model.Keyword))
            {
                model.Keyword = GetIdFromStringId(model.Keyword).ToString();
            }
            var responseRaw = await _unitOfWork.ExportBillRepository.GetAllWithPagingAsync(model);
            var response = responseRaw.Count > 0 ? responseRaw.Select(t => new ExportBillPagingResponse
            {
                Id = t.Id,
                StringId = CommonHelper.ToIdDisplayString("XK", t.Id),
                CreateDate = t.CreateDate,
                Departments = t.Departments,
                FullName = t.FullName,
                Projects = t.Projects,
                Status = t.Status,
                StatusName = CommonHelper.GetDescription((EExportBillStatus)t.Status),
                IsExport = (bool)(t.IsExport != null ? t.IsExport : false),
                TotalQuantity = t.TotalQuantity,
                TotalRecord = t.TotalRecord,
                FromRequests = !string.IsNullOrEmpty(t.FromRequests) ? t.FromRequests.Split(";").Distinct().Select(t =>
                {
                    var item = JsonConvert.DeserializeObject<IdDisplay>(t);
                    return item;
                }).ToList() : new List<IdDisplay>()
            }).ToList() : new List<ExportBillPagingResponse>();
            var totalRecord = response.Count > 0 ? response.FirstOrDefault().TotalRecord : 0;
            var res = new PagingResponseModel<ExportBillPagingResponse>()
            {
                Items = response,
                TotalRecord = totalRecord
            };
            return res;
        }

        public async Task<CombineResponseModel<ExportBill>> ExportAsync(int id,string email)
        {
            var res = new CombineResponseModel<ExportBill>();
            var hrEmail = await _unitOfWork.GlobalConfigurationRepository.GetByNameAsync(Constant.FirstHrEmail);
            if (hrEmail == null || string.IsNullOrEmpty(hrEmail.Value))
            {
                res.ErrorMessage = "Người dùng chưa được config";
                return res;
            }
            var hr1Email = hrEmail.Value.Split(",").Select(t => t.Trim()).FirstOrDefault(t => t.Equals(email, StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrEmpty(hr1Email))
            {
                res.ErrorMessage = "Bạn không có quyền cập nhật phiếu xuất";
                return res;
            }
            var exportBill = await _unitOfWork.ExportBillRepository.GetByIdAsync(id);
            if (exportBill == null)
            {
                res.ErrorMessage = "Không tồn tại phiếu xuất";
                return res;
            }
            var isExport = exportBill.IsExport != null ? exportBill.IsExport : false;
            if ((bool)isExport)
            {
                res.ErrorMessage = "Phiếu đã xuất kho trước đó";
                return res;
            }
            var lineItems = await _unitOfWork.ExportBillLineItemRepository.GetByExportBillIdAsync(id);
            var isHasPrNotApprove = lineItems.Any(t => t.PorequestLineItem.PurchaseRequest.ReviewStatus != (int)EPurchaseRequestStatus.DirectorApproved 
            && t.PorequestLineItem.PurchaseRequest.ReviewStatus !=(int)EPurchaseRequestStatus.Delivered);
            if (isHasPrNotApprove)
            {
                res.ErrorMessage = "Có yêu cầu chưa được giám đốc duyệt";
                return res;
            }
            exportBill.IsExport = true;
            exportBill.UpdateDate = DateTime.UtcNow.UTCToIct();
            res.Status = true;
            res.Data = exportBill;
            return res;
        }
    }
}
