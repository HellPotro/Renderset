namespace Renderset.Infrastructure.Persistence.Entities;

public sealed class DocumentBundleEntity
{
    public Guid BundleId { get; set; }

    public string TenantId { get; set; } = default!;

    public string Title { get; set; } = default!;

    public string? Message { get; set; }

    public string Culture { get; set; } = default!;

    /// <summary>
    /// SHA-256 del token del enlace. El token en claro no se guarda.
    /// </summary>
    public byte[] TokenHash { get; set; } = default!;

    /// <summary>
    /// Token cifrado con Data Protection, para poder volver a abrir el enlace
    /// desde la gestión. Nulo en bundles anteriores a esta columna.
    /// </summary>
    public string? ProtectedToken { get; set; }

    public DateTime TokenIssuedAtUtc { get; set; }

    public bool AllowDataDownload { get; set; }

    public DateTime ExpiresAtUtc { get; set; }

    public DateTime? RevokedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public int AccessCount { get; set; }

    public DateTime? FirstAccessedAtUtc { get; set; }

    public DateTime? LastAccessedAtUtc { get; set; }

    public ICollection<DocumentBundleItemEntity> Items { get; set; } = [];

    public TenantEntity Tenant { get; set; } = default!;
}


public sealed class DocumentBundleItemEntity
{
    public Guid BundleId { get; set; }

    public int Position { get; set; }

    /// <summary>
    /// Repetido respecto al bundle porque forma parte de la clave de
    /// RenderedDocuments: es lo que permite la FK y que la base de datos
    /// impida meter en un bundle un documento de otro tenant.
    /// </summary>
    public string TenantId { get; set; } = default!;

    public string DocumentId { get; set; } = default!;

    public string DisplayName { get; set; } = default!;

    public DocumentBundleEntity Bundle { get; set; } = default!;
}


public sealed class DocumentBundleAccessEntity
{
    public long AccessId { get; set; }

    public Guid BundleId { get; set; }

    public string TenantId { get; set; } = default!;

    public string Kind { get; set; } = default!;

    public int? Position { get; set; }

    public DateTime AccessedAtUtc { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }
}
