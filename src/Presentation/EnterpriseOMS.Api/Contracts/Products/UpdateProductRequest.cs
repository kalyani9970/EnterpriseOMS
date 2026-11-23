using EnterpriseOMS.Domain.Entities.Catalog;

namespace EnterpriseOMS.Api.Contracts.Products
{
    /// <summary>
    /// Body of PUT /api/products/{id}. The id comes from the URL (so the two can never disagree) and the
    /// SKU is absent because it never changes. Version is the value from the last time the product was read.
    /// </summary>
    public record UpdateProductRequest(
        Guid Version,
        string Name,
        string? Description,
        string? Category,
        UnitOfMeasure UnitOfMeasure,
        decimal UnitPrice,
        decimal CostPrice,
        int ReorderLevel);
}
