namespace EnterpriseOMS.Api.Contracts.Auth
{
    /// <summary>
    /// Public registration payload. It deliberately has NO RoleId, so a caller cannot
    /// choose their own role. Self-registered users always get the baseline "Employee" role.
    /// </summary>
    public record RegisterRequest(
        string FullName,
        string Email,
        string Password,
        int OrganizationId);
}
