namespace EnterpriseOMS.Api.Contracts.Products
{
    /// <summary>Body of the activate and deactivate endpoints: just the version the caller last saw.</summary>
    public record ProductVersionRequest(Guid Version);
}
