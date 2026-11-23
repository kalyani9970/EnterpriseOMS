using EnterpriseOMS.Application.Modules.Warehouses.DTOs;
using EnterpriseOMS.Domain.Entities.Warehousing;

namespace EnterpriseOMS.Api.Contracts.Warehouses
{
    /// <summary>
    /// Request bodies. Ids come from the URL (so a body can never disagree with its route), and the
    /// version is the one the caller last read, which proves the edit is based on current data.
    /// </summary>
    public record UpdateWarehouseRequest(Guid Version, string Name, AddressDto Address);

    /// <summary>Body of the activate and deactivate endpoints (warehouses and locations alike).</summary>
    public record WarehouseVersionRequest(Guid Version);

    public record CreateStorageLocationRequest(int? ParentId, string Code, string? Name, StorageLocationType Type);

    public record UpdateStorageLocationRequest(Guid Version, string? Name);
}
