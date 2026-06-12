using System.Security.Claims;

namespace Api;

public static class Roles
{
    public const string Admin    = "Admin";
    public const string Gestor   = "Gestor";
    public const string Operador = "Operador";
    public const string Consulta = "Consulta";
}

public static class UserRoleExtensions
{
    // Admin ou Gestor: acesso total ao domínio agrícola, vê dados de todos os usuários
    public static bool IsManager(this ClaimsPrincipal user) =>
        user.IsInRole(Roles.Admin) || user.IsInRole(Roles.Gestor);

    // Admin, Gestor ou Operador: pode criar e atualizar dados
    public static bool CanWrite(this ClaimsPrincipal user) =>
        user.IsInRole(Roles.Admin) || user.IsInRole(Roles.Gestor) || user.IsInRole(Roles.Operador);
}
