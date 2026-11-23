namespace EnterpriseOMS.Api.Contracts.Auth
{
    /// <summary>
    /// Public sign-up payload. It deliberately has NO OrganizationId and NO RoleId: registering
    /// always creates a brand-new organization and makes the registrant its Admin.
    /// </summary>
    public record RegisterRequest(
        string FullName,
        string Email,
        string Password,
        string OrganizationName);
}
