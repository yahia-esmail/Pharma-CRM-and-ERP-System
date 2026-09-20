using PharmaERP.Shared.Common;

namespace PharmaERP.Application.Products;

public interface IProductService
{
    Task<PagedResult<ProductListItemDto>> GetListAsync(PagedRequest request, CancellationToken ct = default);
    Task<ProductDetailDto> GetByIdAsync(int id, CancellationToken ct = default);
    Task<int> CreateAsync(ProductSaveRequest request, CancellationToken ct = default);
    Task UpdateAsync(int id, ProductSaveRequest request, CancellationToken ct = default);
    Task DeactivateAsync(int id, CancellationToken ct = default);
}
