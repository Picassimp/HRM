using Dapper;
using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Helpers;
using InternalPortal.ApplicationCore.Interfaces.Repositories;
using InternalPortal.ApplicationCore.Models.Product;
using InternalPortal.ApplicationCore.Models.PurchaseOrder.PurchaseRequest;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace InternalPortal.Infrastructure.Persistence.Repositories
{
    public class ProductRepository : EfRepository<Product>, IProductRepository
    {
        public ProductRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<List<Product>> GetByIdsAsync(List<int> ids)
        {
            return await DbSet.Where(o => ids.Contains(o.Id)).ToListAsync();
        }

        public async Task<List<ProductDropdownResponse>> GetDropdownAsync()
        {
            return await DbSet.Select(o => new ProductDropdownResponse
            {
                Id = o.Id,
                ProductName = o.Name,
                ProductDescription = o.Description ?? "",
                ProductCategoryId = o.ProductCategoryId,
                SubProductModels = o.ProductKitProducts.Select(z => new PurchaseRequestSubProductModel
                {
                    SubProductId = z.SubProductId,
                    SubProductName = z.SubProduct.Name,
                    SubProductDescription = z.SubProduct.Description ?? "",
                    KitQuantity = z.Quantity
                }).OrderBy(o => o.SubProductName).ToList()
            }).OrderBy(o => o.ProductName).ToListAsync();
        }

        public async Task<List<SubProductDtoModel>> GetSubProductsByProductIdsAsync(List<int> productIds)
        {
            string query = "Internal_PurchaseRequest_GetSubProduct";

            var parameters = new DynamicParameters(
                new
                {
                    @ids = productIds.JoinComma(true)
                }
            );

            var res = await Context.Database.GetDbConnection().QueryAsync<SubProductDtoModel>(query, parameters, commandType: CommandType.StoredProcedure);
            return res.ToList();
        }
    }
}