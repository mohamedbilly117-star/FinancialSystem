namespace ERP.Domain.Common;

/// <summary>
/// Base class for every entity that requires the mandatory audit trail
/// fields defined in Prompt 4 ("Audit Data" - Created By/Date/Time,
/// Modified By/Date/Time) and Prompt 6 ("Audit Framework" - every action
/// records User, Date, Time). All values are populated automatically by
/// <c>AuditableEntitySaveChangesInterceptor</c> in ERP.Persistence - no
/// module is ever responsible for setting these fields itself, which
/// guarantees the audit trail cannot be bypassed or forgotten by a future
/// developer implementing a new module.
///
/// Almost every transactional and master-data entity in the approved
/// Entity Catalogue (Prompt 4) will derive from this class. A small number
/// of purely technical/log tables (e.g. the audit log itself) intentionally
/// do NOT derive from this, to avoid infinite audit-of-audit recursion.
/// </summary>
public abstract class AuditableEntity : BaseEntity, IAuditableEntity
{
    /// <summary>Id of the ApplicationUser who created the record.</summary>
    public Guid CreatedBy { get; set; }

    /// <summary>UTC date/time the record was created.</summary>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>Id of the ApplicationUser who last modified the record (null if never modified).</summary>
    public Guid? ModifiedBy { get; set; }

    /// <summary>UTC date/time the record was last modified.</summary>
    public DateTime? ModifiedAtUtc { get; set; }

    /// <summary>
    /// Optimistic concurrency token (Prompt 4 - Audit Data: "Version Number
    /// (for optimistic concurrency if appropriate)"). Mapped to SQL
    /// Server's native ROWVERSION type (see
    /// <c>ApplicationDbContext.OnModelCreating</c>'s generic convention),
    /// which auto-increments on every UPDATE at the database level. This
    /// is what turns "two users approve/post the same journal entry at the
    /// same instant" from a silent lost-update bug into an explicit
    /// <c>DbUpdateConcurrencyException</c> the Application layer can
    /// surface to the second user, rather than one approval silently
    /// overwriting the other (Prompt 6 - every financial action must be
    /// safe to audit and trust).
    /// </summary>
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

/// <summary>
/// Extracted as an interface so the Persistence-layer SaveChanges
/// interceptor can populate audit fields via reflection-free, compile-time
/// checked code (<c>is IAuditableEntity</c>) without taking a dependency on
/// the abstract base class hierarchy.
/// </summary>
public interface IAuditableEntity
{
    Guid CreatedBy { get; set; }

    DateTime CreatedAtUtc { get; set; }

    Guid? ModifiedBy { get; set; }

    DateTime? ModifiedAtUtc { get; set; }
}

/// <summary>
/// Implemented by any entity that must never be physically deleted
/// (Prompt 4: "No financial transaction may be physically deleted. Soft
/// Delete must be used where appropriate."). The global EF Core query
/// filter registered in ApplicationDbContext excludes soft-deleted rows
/// from normal queries automatically.
/// </summary>
public interface ISoftDelete
{
    bool IsDeleted { get; set; }

    DateTime? DeletedAtUtc { get; set; }

    Guid? DeletedBy { get; set; }
}
