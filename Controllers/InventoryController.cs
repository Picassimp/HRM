using AutoMapper;
using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Helpers;
using InternalPortal.ApplicationCore.Interfaces;
using InternalPortal.ApplicationCore.Models.Inventory;
using InternalPortal.ApplicationCore.ValueObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InternalPortal.API.Controllers
{
    public class InventoryController : BaseApiController
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        public InventoryController(IUnitOfWork unitOfWork,
            IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }
        [HttpGet("mobile/scan/{barcode}")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> ScanBarcode([FromRoute] string barcode)
        {
            if (string.IsNullOrEmpty(barcode) || string.IsNullOrWhiteSpace(barcode))
            {
                return ErrorResult("Mã sản phẩm không được bỏ trống");
            }

            var inventory = await _unitOfWork.InventoryRepository.GetByBarcodeAsync(barcode.Trim());
            if (inventory == null)
            {
                return ErrorResult($"Sản phẩm '{barcode}' không tồn tại hoặc chưa cập nhật vào hệ thống. Vui lòng liên hệ nhân sự");
            }
            return SuccessResult(_mapper.Map<Inventory, InventoryResponse>(inventory));
        }
        [HttpPost("mobile/scan/submit")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> Submit([FromBody] SubmitRequest model)
        {
            var user = GetCurrentUser();
            if (model.Quantity <= 0)
            {
                return ErrorResult("Số lượng không hợp lệ");
            }

            var inventory = await _unitOfWork.InventoryRepository.GetByIdAsync(model.Id);
            if (inventory == null)
            {
                return ErrorResult("Sản phẩm không tồn tại hoặc chưa nhập vào hệ thống. Vui lòng liên hệ nhân sự");
            }

            var inventoryTransaction = new InventoryTransaction
            {
                CreatedDate = DateTime.UtcNow.UTCToIct(),
                DiscountedPrice = inventory.DiscountedPrice,
                InventoryId = inventory.Id,
                Quantity = model.Quantity,
                TotalPrice = model.Quantity * inventory.DiscountedPrice,
                UserId = user.Id
            };
            if (inventory.Quantity - model.Quantity < 0)
            {
                inventory.Quantity = 0;
            }
            else
            {
                inventory.Quantity -= model.Quantity;
            }
            await _unitOfWork.InventoryRepository.UpdateAsync(inventory);
            await _unitOfWork.InventoryTransactionRepository.CreateAsync(inventoryTransaction);
            await _unitOfWork.SaveChangesAsync();
            return SuccessResult("Mua hàng thành công. Chúc bạn một ngày làm việc vui vẻ");
        }
        [HttpGet("mobile/transactions/{month}/{year}")]
        [Authorize(Policy = AuthorizationPolicies.UserRoleRequired)]
        public async Task<IActionResult> GetTransactions([FromRoute] int month, [FromRoute] int year)
        {
            var user = GetCurrentUser();
            var transactions = await _unitOfWork.InventoryTransactionRepository.GetByUserIdAsync(user.Id, month, year);
            //group data 
            var resultGroup = transactions.GroupBy(p => p.CreatedDate.Date).OrderByDescending(p => p.Key);
            var data = new List<TransactionGroupResponse>();
            foreach (var result in resultGroup)
            {
                data.Add(new TransactionGroupResponse
                {
                    Date = result.Key.ToString("dd/MM/yyyy"),
                    Transactions = result.ToList()
                });
            }
            return SuccessResult(data);
        }
    }
}
