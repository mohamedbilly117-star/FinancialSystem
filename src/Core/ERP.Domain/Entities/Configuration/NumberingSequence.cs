using ERP.Domain.Common;
using ERP.Domain.Enums;
using ERP.Domain.Exceptions;
using ERP.Shared.Guards;

namespace ERP.Domain.Entities.Configuration;

/// <summary>
/// Prompt 4 - Numbering Strategy: "Support configurable numbering
/// policies." Prompt 11 - Numbering Configuration: "Administrators must
/// configure numbering formats without code changes." This is the
/// configuration data <c>ERP.Shared.Constants.NumberingSequenceKeys</c>'s
/// own doc comment already anticipated - <see cref="SequenceKey"/> matches
/// one of that class's constants (e.g. "JOURNAL"), and this entity
/// supplies the actual prefix/padding/current-value/reset-policy that
/// determines what <see cref="JournalEntry.Post"/>'s caller (a later
/// Application-layer milestone) passes as the final journal number.
///
/// <see cref="GenerateNext"/> is NOT concurrency-safe by itself - two
/// simultaneous callers could both read the same <see cref="CurrentValue"/>
/// before either saves. Correctness instead relies on
/// <see cref="AuditableEntity.RowVersion"/>'s optimistic concurrency
/// check: the second of two racing SaveChangesAsync calls fails with
/// <c>DbUpdateConcurrencyException</c>, and the Application-layer caller
/// (not yet built) is expected to reload and retry. This is a deliberate,
/// simple design - a dedicated database sequence/lock-based approach would
/// be more throughput-efficient but is unnecessary complexity for a
/// LAN-deployed, modest-concurrent-user government system.
/// </summary>
public sealed class NumberingSequence : AuditableEntity, IAggregateRoot
{
    public string SequenceKey { get; private set; } = string.Empty;

    /// <summary>Single source of truth for Prefix's maximum length - referenced by both the Guard check in <see cref="Create"/> and <c>NumberingSequenceConfiguration</c>'s EF Core column mapping, so the two constraints cannot silently drift apart.</summary>
    public const int MaxPrefixLength = 20;

    public string Prefix { get; private set; } = string.Empty;

    public int PaddingLength { get; private set; }

    public int CurrentValue { get; private set; }

    public NumberingResetPolicy ResetPolicy { get; private set; }

    /// <summary>Only meaningful (and required) when <see cref="ResetPolicy"/> is <see cref="NumberingResetPolicy.Yearly"/> - which fiscal year's numbering this sequence row belongs to. A Yearly-reset sequence needs one row PER fiscal year (a new row starting at 0, not this same row resetting in place), so last year's issued numbers remain permanently attributable to last year's sequence.</summary>
    public Guid? FiscalYearId { get; private set; }

    public bool IsActive { get; private set; }

    private NumberingSequence()
    {
        // Required by EF Core.
    }

    private NumberingSequence(Guid id, string sequenceKey, string prefix, int paddingLength, NumberingResetPolicy resetPolicy, Guid? fiscalYearId, int startingValue)
    {
        Id = id;
        SequenceKey = sequenceKey;
        Prefix = prefix;
        PaddingLength = paddingLength;
        ResetPolicy = resetPolicy;
        FiscalYearId = fiscalYearId;
        CurrentValue = startingValue;
        IsActive = true;
    }

    public static NumberingSequence Create(string sequenceKey, string prefix, int paddingLength, NumberingResetPolicy resetPolicy, Guid? fiscalYearId = null, int startingValue = 0)
    {
        Guard.AgainstNullOrWhiteSpace(sequenceKey, nameof(sequenceKey));
        Guard.AgainstLengthGreaterThan(sequenceKey, 30, nameof(sequenceKey));

        // Prefix may legitimately be empty (some numbering schemes have no
        // prefix at all), so it is not Guard-validated as required - but
        // it must still respect the same maximum length as the database
        // column (EF configuration: HasMaxLength(20)), checked here at
        // the Domain level rather than left to fail only at save time.
        prefix ??= string.Empty;
        Guard.AgainstLengthGreaterThan(prefix, MaxPrefixLength, nameof(prefix));

        if (paddingLength is < 1 or > 10)
        {
            throw new DomainException("Numbering sequence padding length must be between 1 and 10.");
        }

        if (resetPolicy == NumberingResetPolicy.Yearly && fiscalYearId is null)
        {
            throw new DomainException("A Yearly reset policy requires an associated FiscalYearId.");
        }

        if (startingValue < 0)
        {
            throw new DomainException("Numbering sequence starting value cannot be negative.");
        }

        return new NumberingSequence(Guid.NewGuid(), sequenceKey, prefix, paddingLength, resetPolicy, fiscalYearId, startingValue);
    }

    /// <summary>Advances and returns the next formatted number (e.g. "JV-000042"). See class remarks for the concurrency caveat.</summary>
    public string GenerateNext()
    {
        if (!IsActive)
        {
            throw new DomainException($"Numbering sequence '{SequenceKey}' is not active.");
        }

        CurrentValue++;
        return Format();
    }

    public string Format() => $"{Prefix}{CurrentValue.ToString().PadLeft(PaddingLength, '0')}";

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    /// <summary>Returns the counter to zero. This entity has no notion of "the year/month changed" - deciding WHEN to call this is Application/Workflow-layer orchestration (Prompt 10's Month-End/Year-End Process), not something this entity schedules itself.</summary>
    public void ResetSequence() => CurrentValue = 0;
}
