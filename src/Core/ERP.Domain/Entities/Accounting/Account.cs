using ERP.Domain.Common;
using ERP.Domain.Entities.Accounting.Rules;
using ERP.Domain.Enums;
using ERP.Domain.Exceptions;
using ERP.Shared.Guards;

namespace ERP.Domain.Entities.Accounting;

/// <summary>
/// A single node in the Chart of Accounts (Prompt 5). Self-referencing
/// hierarchy: a <see cref="AccountClassification.Parent"/> account groups
/// its children for reporting/summarization and can never itself be
/// posted to; a <see cref="AccountClassification.Posting"/> (or
/// <see cref="AccountClassification.Control"/>) account is a leaf that
/// transactions post to directly. <see cref="AccountType"/> and
/// <see cref="NormalBalance"/> are fixed at the root of a branch and
/// inherited by every descendant, so an Asset account can never
/// accidentally gain a Liability child.
/// </summary>
public sealed class Account : AuditableEntity, IAggregateRoot
{
    private readonly List<Account> _children = new();

    public string Code { get; private set; } = string.Empty;

    public string NameAr { get; private set; } = string.Empty;

    public string NameEn { get; private set; } = string.Empty;

    public Guid? ParentAccountId { get; private set; }

    public AccountType AccountType { get; private set; }

    public AccountNormalBalance NormalBalance { get; private set; }

    public AccountClassification Classification { get; private set; }

    /// <summary>Hierarchy depth, root = 1. Denormalized (rather than computed by walking ParentAccountId at query time) purely for fast filtering/sorting in the Chart of Accounts screen and reports (Prompt 4 - Indexing/Performance Strategy).</summary>
    public int Level { get; private set; }

    /// <summary>Prompt 5 - Account Validation: "Inactive account." An inactive account is kept for historical reporting but rejected by <see cref="AccountMustAllowPostingRule"/> for any new posting.</summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>Prompt 4 - Chart of Accounts: "Reserved accounts." True for accounts the system itself depends on (e.g. a default Suspense account) that administrators may rename/reclassify with caution but must never delete.</summary>
    public bool IsSystemReserved { get; private set; }

    public IReadOnlyCollection<Account> Children => _children.AsReadOnly();

    private Account()
    {
        // Required by EF Core.
    }

    private Account(Guid id, string code, string nameAr, string nameEn, Guid? parentAccountId, AccountType accountType, AccountNormalBalance normalBalance, AccountClassification classification, int level)
    {
        Id = id;
        Code = code;
        NameAr = nameAr;
        NameEn = nameEn;
        ParentAccountId = parentAccountId;
        AccountType = accountType;
        NormalBalance = normalBalance;
        Classification = classification;
        Level = level;
    }

    /// <summary>Creates a root account (Level 1) - the top of one branch of the Chart of Accounts, e.g. "1000 - Assets".</summary>
    public static Account CreateRoot(string code, string nameAr, string nameEn, AccountType accountType, AccountNormalBalance normalBalance, AccountClassification classification = AccountClassification.Parent, bool isSystemReserved = false)
    {
        ValidateNameAndCode(code, nameAr, nameEn);

        return new Account(Guid.NewGuid(), code, nameAr, nameEn, null, accountType, normalBalance, classification, 1)
        {
            IsSystemReserved = isSystemReserved,
        };
    }

    /// <summary>
    /// Adds a child account beneath this one. The child always inherits
    /// this account's <see cref="AccountType"/> and <see cref="NormalBalance"/>
    /// (Prompt 5's fixed accounting taxonomy cannot change mid-branch);
    /// only its <paramref name="classification"/> (Parent/Posting/Control)
    /// is caller-specified, since that is a structural, not a taxonomic,
    /// choice.
    /// </summary>
    public Account AddChild(string code, string nameAr, string nameEn, AccountClassification classification, bool isSystemReserved = false)
    {
        ValidateNameAndCode(code, nameAr, nameEn);

        if (Classification == AccountClassification.Posting)
        {
            throw new DomainException(
                $"Account '{Code}' is a Posting account and cannot have children - only Parent (or Control) accounts may be subdivided further.");
        }

        var child = new Account(Guid.NewGuid(), code, nameAr, nameEn, Id, AccountType, NormalBalance, classification, Level + 1)
        {
            IsSystemReserved = isSystemReserved,
        };

        _children.Add(child);
        return child;
    }

    /// <summary>Prompt 6 - "Every action must respect permissions"; the permission check itself happens in the Application layer - this method only enforces the Domain-level invariant that the rule evaluates against.</summary>
    public void EnsureCanReceivePosting()
    {
        var rule = new AccountMustAllowPostingRule(Code, IsActive, Classification);
        if (!rule.IsSatisfied())
        {
            throw new BusinessRuleValidationException(rule);
        }
    }

    public void Activate() => IsActive = true;

    public void Deactivate()
    {
        if (IsSystemReserved)
        {
            throw new DomainException($"Account '{Code}' is system-reserved and cannot be deactivated.");
        }

        IsActive = false;
    }

    public void Rename(string nameAr, string nameEn)
    {
        Guard.AgainstNullOrWhiteSpace(nameAr, nameof(nameAr));
        Guard.AgainstNullOrWhiteSpace(nameEn, nameof(nameEn));
        NameAr = nameAr;
        NameEn = nameEn;
    }

    private static void ValidateNameAndCode(string code, string nameAr, string nameEn)
    {
        Guard.AgainstNullOrWhiteSpace(code, nameof(code));
        Guard.AgainstLengthGreaterThan(code, 20, nameof(code));
        Guard.AgainstNullOrWhiteSpace(nameAr, nameof(nameAr));
        Guard.AgainstNullOrWhiteSpace(nameEn, nameof(nameEn));
    }
}
