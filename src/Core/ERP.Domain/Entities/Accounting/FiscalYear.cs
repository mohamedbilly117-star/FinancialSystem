using ERP.Domain.Common;
using ERP.Domain.Enums;
using ERP.Domain.Exceptions;
using ERP.Shared.Guards;

namespace ERP.Domain.Entities.Accounting;

/// <summary>
/// Prompt 5 / Prompt 10 - Accounting Period Management. The aggregate root
/// for a government fiscal year and its subdivided accounting periods
/// (typically 12 monthly periods, optionally plus one or more Adjustment
/// periods per Prompt 5/10's "Adjustment period"). Every
/// <c>JournalEntry</c> references exactly one <see cref="AccountingPeriod"/>
/// within exactly one FiscalYear, and posting is only permitted while both
/// the year and the specific period are open (Prompt 5 - "Fiscal Year
/// Validation", "Period Validation").
/// </summary>
public sealed class FiscalYear : AuditableEntity, IAggregateRoot
{
    private readonly List<AccountingPeriod> _periods = new();

    public string Code { get; private set; } = string.Empty;

    public string NameAr { get; private set; } = string.Empty;

    public string NameEn { get; private set; } = string.Empty;

    public DateOnly StartDate { get; private set; }

    public DateOnly EndDate { get; private set; }

    public FiscalYearStatus Status { get; private set; }

    public IReadOnlyCollection<AccountingPeriod> Periods => _periods.AsReadOnly();

    private FiscalYear()
    {
        // Required by EF Core.
    }

    private FiscalYear(Guid id, string code, string nameAr, string nameEn, DateOnly startDate, DateOnly endDate)
    {
        Id = id;
        Code = code;
        NameAr = nameAr;
        NameEn = nameEn;
        StartDate = startDate;
        EndDate = endDate;
        Status = FiscalYearStatus.Open;
    }

    /// <summary>
    /// Creates a new fiscal year. Periods are added separately via
    /// <see cref="AddMonthlyPeriods"/> or <see cref="AddPeriod"/> rather
    /// than being assumed here, since Prompt 11's Fiscal Year Settings
    /// leave the number/shape of periods administrator-configurable
    /// rather than hardcoded to exactly twelve calendar months.
    /// </summary>
    public static FiscalYear Create(string code, string nameAr, string nameEn, DateOnly startDate, DateOnly endDate)
    {
        Guard.AgainstNullOrWhiteSpace(code, nameof(code));
        Guard.AgainstLengthGreaterThan(code, 20, nameof(code));
        Guard.AgainstNullOrWhiteSpace(nameAr, nameof(nameAr));
        Guard.AgainstNullOrWhiteSpace(nameEn, nameof(nameEn));

        if (endDate <= startDate)
        {
            throw new DomainException($"Fiscal year '{code}' end date must be after its start date.");
        }

        return new FiscalYear(Guid.NewGuid(), code, nameAr, nameEn, startDate, endDate);
    }

    /// <summary>Convenience factory: subdivides the year into twelve consecutive monthly periods - the common case - without forcing every caller to build them by hand.</summary>
    public void AddMonthlyPeriods(Func<string> nameArFactory, Func<string> nameEnFactory)
    {
        // Kept intentionally simple (month-aligned only) for this
        // milestone; a government fiscal year that does not align to
        // calendar months would use AddPeriod(...) directly instead.
        var cursor = StartDate;
        var periodNumber = 1;

        while (cursor < EndDate)
        {
            var periodEnd = new DateOnly(cursor.Year, cursor.Month, DateTime.DaysInMonth(cursor.Year, cursor.Month));
            if (periodEnd > EndDate)
            {
                periodEnd = EndDate;
            }

            AddPeriod(periodNumber, $"{nameArFactory()} {periodNumber}", $"{nameEnFactory()} {periodNumber}", cursor, periodEnd, isAdjustmentPeriod: false);

            cursor = periodEnd.AddDays(1);
            periodNumber++;
        }
    }

    public AccountingPeriod AddPeriod(int periodNumber, string nameAr, string nameEn, DateOnly startDate, DateOnly endDate, bool isAdjustmentPeriod)
    {
        if (_periods.Any(p => p.PeriodNumber == periodNumber))
        {
            throw new DomainException($"Fiscal year '{Code}' already has a period numbered {periodNumber}.");
        }

        var period = AccountingPeriod.Create(Id, periodNumber, nameAr, nameEn, startDate, endDate, isAdjustmentPeriod);
        _periods.Add(period);
        return period;
    }

    /// <summary>Finds the single period a given date falls into, or null if the date is outside every defined period (e.g. periods not yet fully configured).</summary>
    public AccountingPeriod? FindPeriodFor(DateOnly date)
        => _periods.SingleOrDefault(p => date >= p.StartDate && date <= p.EndDate);

    public void Close()
    {
        if (Status != FiscalYearStatus.Open)
        {
            throw new DomainException($"Fiscal year '{Code}' must be Open to be closed (current status: {Status}).");
        }

        if (_periods.Any(p => p.Status is not (AccountingPeriodStatus.Closed or AccountingPeriodStatus.Locked)))
        {
            throw new DomainException(
                $"Fiscal year '{Code}' cannot be closed while one or more of its periods are still Open, Reopened or in Adjustment. " +
                "Close every period first (Prompt 10 - Year-End Process: 'Final Validation').");
        }

        Status = FiscalYearStatus.Closed;
    }

    public void Lock()
    {
        if (Status != FiscalYearStatus.Closed)
        {
            throw new DomainException($"Fiscal year '{Code}' must be Closed before it can be Locked (current status: {Status}).");
        }

        Status = FiscalYearStatus.Locked;
    }

    /// <summary>Prompt 10 - "Reopened Period" applied at the year level. Deliberately does not automatically reopen individual periods - each period must be reopened explicitly, keeping every reopening a discrete, audited decision (Prompt 6 - Audit Framework).</summary>
    public void Reopen()
    {
        if (Status != FiscalYearStatus.Closed)
        {
            throw new DomainException($"Fiscal year '{Code}' must be Closed before it can be Reopened (current status: {Status}).");
        }

        Status = FiscalYearStatus.Open;
    }
}

/// <summary>
/// A single accounting period (typically one calendar month) within a
/// <see cref="FiscalYear"/>. Modeled as a child entity of the FiscalYear
/// aggregate (not its own aggregate root) - it has no meaning or lifecycle
/// independent of the year that owns it, matching Prompt 4's relationship
/// design guidance to model clearly-owned one-to-many data as a single
/// aggregate rather than two independently-saved roots.
/// </summary>
public sealed class AccountingPeriod : AuditableEntity
{
    public Guid FiscalYearId { get; private set; }

    public int PeriodNumber { get; private set; }

    public string NameAr { get; private set; } = string.Empty;

    public string NameEn { get; private set; } = string.Empty;

    public DateOnly StartDate { get; private set; }

    public DateOnly EndDate { get; private set; }

    public AccountingPeriodStatus Status { get; private set; }

    public bool IsAdjustmentPeriod { get; private set; }

    private AccountingPeriod()
    {
        // Required by EF Core.
    }

    private AccountingPeriod(Guid id, Guid fiscalYearId, int periodNumber, string nameAr, string nameEn, DateOnly startDate, DateOnly endDate, bool isAdjustmentPeriod)
    {
        Id = id;
        FiscalYearId = fiscalYearId;
        PeriodNumber = periodNumber;
        NameAr = nameAr;
        NameEn = nameEn;
        StartDate = startDate;
        EndDate = endDate;
        IsAdjustmentPeriod = isAdjustmentPeriod;
        Status = AccountingPeriodStatus.Open;
    }

    internal static AccountingPeriod Create(Guid fiscalYearId, int periodNumber, string nameAr, string nameEn, DateOnly startDate, DateOnly endDate, bool isAdjustmentPeriod)
    {
        Guard.AgainstEmpty(fiscalYearId, nameof(fiscalYearId));
        Guard.AgainstNullOrWhiteSpace(nameAr, nameof(nameAr));
        Guard.AgainstNullOrWhiteSpace(nameEn, nameof(nameEn));

        if (periodNumber < 1)
        {
            throw new DomainException("Period number must be 1 or greater.");
        }

        if (endDate < startDate)
        {
            throw new DomainException($"Accounting period {periodNumber} end date must not be before its start date.");
        }

        return new AccountingPeriod(Guid.NewGuid(), fiscalYearId, periodNumber, nameAr, nameEn, startDate, endDate, isAdjustmentPeriod);
    }

    public void Close()
    {
        if (Status is not (AccountingPeriodStatus.Open or AccountingPeriodStatus.Adjustment))
        {
            throw new DomainException($"Period {PeriodNumber} must be Open or in Adjustment to be closed (current status: {Status}).");
        }

        Status = AccountingPeriodStatus.Closed;
    }

    public void Lock()
    {
        if (Status != AccountingPeriodStatus.Closed)
        {
            throw new DomainException($"Period {PeriodNumber} must be Closed before it can be Locked (current status: {Status}).");
        }

        Status = AccountingPeriodStatus.Locked;
    }

    public void Reopen()
    {
        if (Status is not (AccountingPeriodStatus.Closed or AccountingPeriodStatus.Locked))
        {
            throw new DomainException($"Period {PeriodNumber} must be Closed or Locked before it can be Reopened (current status: {Status}).");
        }

        Status = AccountingPeriodStatus.Reopened;
    }

    /// <summary>Explicitly resumes normal posting on a Reopened period once whatever correction it was reopened for is complete (Prompt 6 - every state change is a deliberate, audited action, never implicit).</summary>
    public void ResumeAsOpen()
    {
        if (Status != AccountingPeriodStatus.Reopened)
        {
            throw new DomainException($"Period {PeriodNumber} must be in Reopened status to resume as Open (current status: {Status}).");
        }

        Status = AccountingPeriodStatus.Open;
    }
}
