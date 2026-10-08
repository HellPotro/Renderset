namespace Renderset.Core.Tenancy;

/// <summary>
/// Papel de un usuario dentro de un tenant. El orden importa: un rol puede
/// todo lo que pueden los de debajo.
/// </summary>
public enum TenantRole
{
    /// <summary>Diseña, genera y comparte documentos.</summary>
    Member = 0,

    /// <summary>Además gestiona miembros, API keys y la página pública.</summary>
    Admin = 1,

    /// <summary>Además gestiona administradores. Siempre queda al menos uno.</summary>
    Owner = 2
}
