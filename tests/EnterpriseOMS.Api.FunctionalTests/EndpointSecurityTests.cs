using System.Reflection;
using EnterpriseOMS.Application.Common.Security;
using EnterpriseOMS.Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace EnterpriseOMS.Api.FunctionalTests;

/// <summary>
/// The tenant filter treats anonymous requests as "not yet known", which is only safe while every
/// endpoint other than the sign-in flow requires authentication. These tests read the controllers
/// by reflection and fail the build if a new endpoint is added without protection.
/// </summary>
public class EndpointSecurityTests
{
    // The ONLY endpoints that may be called without signing in.
    private static readonly string[] AllowedAnonymousEndpoints =
    {
        "AuthController.Login",
        "AuthController.Logout",
        "AuthController.Refresh",
        "AuthController.Register"
    };

    private static List<MethodInfo> Endpoints() => typeof(Program).Assembly.GetTypes()
        .Where(t => t.IsClass && !t.IsAbstract && typeof(ControllerBase).IsAssignableFrom(t))
        .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        .Where(m => m.GetCustomAttributes<HttpMethodAttribute>(inherit: true).Any())
        .ToList();

    private static string Name(MethodInfo method) => $"{method.DeclaringType!.Name}.{method.Name}";

    private static bool RequiresAuthorization(MethodInfo method) =>
        method.GetCustomAttributes(inherit: true).OfType<IAuthorizeData>().Any() ||
        method.DeclaringType!.GetCustomAttributes(inherit: true).OfType<IAuthorizeData>().Any();

    private static bool IsAnonymous(MethodInfo method) =>
        method.GetCustomAttributes(inherit: true).OfType<IAllowAnonymous>().Any() ||
        method.DeclaringType!.GetCustomAttributes(inherit: true).OfType<IAllowAnonymous>().Any();

    [Fact]
    public void ThereAreEndpointsToCheck()
    {
        // Guards against the other tests passing vacuously (for example after a refactor moves controllers).
        Assert.NotEmpty(Endpoints());
    }

    [Fact]
    public void EveryEndpoint_IsEitherAuthorizedOrExplicitlyAnonymous()
    {
        var unprotected = Endpoints()
            .Where(m => !RequiresAuthorization(m) && !IsAnonymous(m))
            .Select(Name)
            .ToList();

        Assert.Empty(unprotected);
    }

    [Fact]
    public void OnlyTheSignInFlow_IsAnonymous()
    {
        var anonymous = Endpoints()
            .Where(IsAnonymous)
            .Select(Name)
            .OrderBy(n => n)
            .ToList();

        Assert.Equal(AllowedAnonymousEndpoints, anonymous);
    }

    [Fact]
    public void EveryHasPermission_NamesAPermissionFromTheCatalog()
    {
        var catalog = typeof(Permissions).GetNestedTypes(BindingFlags.Public)
            .SelectMany(t => t.GetFields(BindingFlags.Public | BindingFlags.Static))
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToHashSet();

        var usedPolicies = Endpoints()
            .SelectMany(m => m.GetCustomAttributes<HasPermissionAttribute>(inherit: true)
                .Concat(m.DeclaringType!.GetCustomAttributes<HasPermissionAttribute>(inherit: true)))
            .Select(a => a.Policy!)
            .Distinct()
            .ToList();

        Assert.NotEmpty(usedPolicies);

        // A typo such as [HasPermission("roles.vew")] would otherwise lock everyone out silently.
        var unknown = usedPolicies
            .Select(p => p[PermissionPolicyProvider.PolicyPrefix.Length..])
            .Where(code => !catalog.Contains(code))
            .ToList();

        Assert.Empty(unknown);
    }
}