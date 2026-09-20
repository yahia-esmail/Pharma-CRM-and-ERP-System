using Microsoft.EntityFrameworkCore;
using PharmaERP.Application.Common;
using PharmaERP.Application.Common.Interfaces;
using PharmaERP.Domain.Entities;
using PharmaERP.Shared.Common;

namespace PharmaERP.Application.Products;

public class ProductService(IAppDbContext db) : IProductService
{
    public async Task<PagedResult<ProductListItemDto>> GetListAsync(PagedRequest request, CancellationToken ct = default)
    {
        var query = db.Products.AsNoTracking().Where(p => !p.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.Search))
            query = query.Where(p => p.Name.Contains(request.Search) || p.Sku.Contains(request.Search));

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderBy(p => p.Name)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(p => new ProductListItemDto(p.Id, p.Sku, p.Name, p.Category, p.UnitOfMeasure, p.UnitPrice))
            .ToListAsync(ct);

        return new PagedResult<ProductListItemDto>
        {
            Items = items, TotalCount = totalCount, PageNumber = request.PageNumber, PageSize = request.PageSize
        };
    }

    public async Task<ProductDetailDto> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var p = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Product), id);

        return new ProductDetailDto(p.Id, p.Sku, p.Name, p.Category, p.UnitOfMeasure, p.IsBatchTracked,
            p.IsExpiryTracked, p.UnitCost, p.UnitPrice, p.ReorderLevel);
    }

    public async Task<int> CreateAsync(ProductSaveRequest request, CancellationToken ct = default)
    {
        var skuTaken = await db.Products.AnyAsync(p => p.Sku == request.Sku && !p.IsDeleted, ct);
        if (skuTaken) throw new ValidationFailedException($"SKU '{request.Sku}' is already in use.");

        var product = new Product();
        Apply(product, request);
        db.Products.Add(product);
        await db.SaveChangesAsync(ct);
        return product.Id;
    }

    public async Task UpdateAsync(int id, ProductSaveRequest request, CancellationToken ct = default)
    {
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Product), id);

        var skuTaken = await db.Products.AnyAsync(p => p.Sku == request.Sku && p.Id != id && !p.IsDeleted, ct);
        if (skuTaken) throw new ValidationFailedException($"SKU '{request.Sku}' is already in use.");

        Apply(product, request);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeactivateAsync(int id, CancellationToken ct = default)
    {
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Product), id);

        product.IsDeleted = true;
        await db.SaveChangesAsync(ct);
    }

    private static void Apply(Product product, ProductSaveRequest request)
    {
        product.Sku = request.Sku;
        product.Name = request.Name;
        product.Category = request.Category;
        product.UnitOfMeasure = request.UnitOfMeasure;
        product.IsBatchTracked = request.IsBatchTracked;
        product.IsExpiryTracked = request.IsExpiryTracked;
        product.UnitCost = request.UnitCost;
        product.UnitPrice = request.UnitPrice;
        product.ReorderLevel = request.ReorderLevel;
    }
}
