using System.Security.Claims;

namespace Api;

public static class Roles
{
    public const string Admin = "Admin";
    public const string Agronomo = "Agronomo";
    public const string Tecnico = "Tecnico";
    public const string Produtor = "Produtor";
}

public static class UserRoleExtensions
{
    public static bool IsManager(this ClaimsPrincipal user) =>
        user.IsInRole(Roles.Admin) || user.IsInRole(Roles.Agronomo);

    public static bool CanWrite(this ClaimsPrincipal user) =>
        user.IsInRole(Roles.Admin) || user.IsInRole(Roles.Agronomo) || user.IsInRole(Roles.Tecnico);
}
