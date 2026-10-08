using Microsoft.AspNetCore.Identity;

namespace Renderset.Web.Identity;

/// <summary>
/// Usuario de RenderSet Web. El correo es el nombre de usuario.
/// </summary>
public sealed class RendersetUser
    : IdentityUser
{
    public string? DisplayName { get; set; }

    /// <summary>
    /// Tenant con el que trabajó por última vez: es con el que entra.
    /// </summary>
    public string? LastTenantId { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
