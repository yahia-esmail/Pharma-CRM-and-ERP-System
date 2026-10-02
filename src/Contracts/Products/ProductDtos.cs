namespace PharmaERP.Application.Products;

public record ProductListItemDto(
    int Id,
    string Sku,
    string Name,
    string? Category,
    string UnitOfMeasure,
    decimal UnitPrice);

public record ProductDetailDto(
    int Id,
    string Sku,
    string Name,
    string? Category,
    string UnitOfMeasure,
    bool IsBatchTracked,
    bool IsExpiryTracked,
    decimal UnitCost,
    decimal UnitPrice,
    int ReorderLevel);

public class ProductSaveRequest
{
    public string Sku { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Category { get; set; }
    public string UnitOfMeasure { get; set; } = null!;
    public bool IsBatchTracked { get; set; }
    public bool IsExpiryTracked { get; set; }
    public decimal UnitCost { get; set; }
    public decimal UnitPrice { get; set; }
    public int ReorderLevel { get; set; }
}
