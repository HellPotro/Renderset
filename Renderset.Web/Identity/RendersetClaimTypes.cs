namespace Renderset.Web.Identity;

/// <summary>
/// Claims propios en la cookie de sesión. El tenant elegido va aquí; el rol
/// no, se lee siempre de TenantMembers para que un cambio de rol (o una
/// baja) se note sin esperar a que caduque la cookie.
/// </summary>
public static class RendersetClaimTypes
{
    public const string TenantId = "rs:tenant";

    public const string TenantName = "rs:tenant_name";

    public const string DisplayName = "rs:display_name";
}
