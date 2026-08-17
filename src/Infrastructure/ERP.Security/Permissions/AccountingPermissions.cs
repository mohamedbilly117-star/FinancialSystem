namespace ERP.Security.Permissions;

/// <summary>
/// Prompt 6 applied concretely to the Accounting Engine modules built so
/// far (Prompt 5 / Prompt 13's "Accounting Engine" phase). Each resource
/// constant below is combined with <see cref="PermissionActions"/>' fixed
/// verbs via <see cref="PermissionActions.For"/> to build the exact
/// strings used both in <c>[Authorize(Policy = "...")]</c> and as
/// <c>Permission.Code</c> seed values.
///
/// This is the concrete answer to Prompt 6's "Module, screen, and action-
/// level permissions" for Accounting specifically - one resource constant
/// per screen/aggregate (JournalEntry, FiscalYear, AccountingPeriod,
/// ChartOfAccounts, AccountingRule, DistributionTemplate), each combined
/// with only the actions that are actually meaningful for it (a
/// FiscalYear is never "Posted"; a JournalEntry is never "Locked").
/// </summary>
public static class AccountingPermissionResources
{
    public const string JournalEntry = "JournalEntry";
    public const string FiscalYear = "FiscalYear";
    public const string AccountingPeriod = "AccountingPeriod";
    public const string ChartOfAccounts = "ChartOfAccounts";
    public const string AccountingRule = "AccountingRule";
    public const string DistributionTemplate = "DistributionTemplate";
}

/// <summary>
/// The specific permission strings that gate every sensitive Accounting
/// Engine operation - directly answering this milestone's explicit
/// requirement to protect "approval, posting, reversal, fiscal-period
/// actions, and configuration changes". Each constant corresponds 1:1 to
/// a domain method already implemented:
/// <see cref="ERP.Domain.Entities.Accounting.JournalEntry"/>'s
/// Submit/Approve/Reject/Post/CreateReversal/Cancel, and the
/// Activate/Deactivate/CreateNewVersion methods shared by
/// <see cref="ERP.Domain.Entities.Accounting.RuleEngine.AccountingRule"/>
/// and
/// <see cref="ERP.Domain.Entities.Accounting.Distribution.DistributionTemplate"/>
/// (both are "configuration changes" per Prompt 6's Configuration
/// Security: "Only authorized users may modify configuration").
/// </summary>
public static class AccountingPermissions
{
    // ----- Journal Entry (the Automatic Journal Engine) -----
    public static readonly string JournalEntryView = PermissionActions.For(AccountingPermissionResources.JournalEntry, PermissionActions.View);
    public static readonly string JournalEntryCreate = PermissionActions.For(AccountingPermissionResources.JournalEntry, PermissionActions.Create);
    public static readonly string JournalEntryEdit = PermissionActions.For(AccountingPermissionResources.JournalEntry, PermissionActions.Edit);
    public static readonly string JournalEntryDelete = PermissionActions.For(AccountingPermissionResources.JournalEntry, PermissionActions.Delete);
    public static readonly string JournalEntryApprove = PermissionActions.For(AccountingPermissionResources.JournalEntry, PermissionActions.Approve);
    public static readonly string JournalEntryReject = PermissionActions.For(AccountingPermissionResources.JournalEntry, PermissionActions.Reject);
    public static readonly string JournalEntryPost = PermissionActions.For(AccountingPermissionResources.JournalEntry, PermissionActions.Post);
    public static readonly string JournalEntryReverse = PermissionActions.For(AccountingPermissionResources.JournalEntry, PermissionActions.Reverse);
    public static readonly string JournalEntryPrint = PermissionActions.For(AccountingPermissionResources.JournalEntry, PermissionActions.Print);
    public static readonly string JournalEntryExport = PermissionActions.For(AccountingPermissionResources.JournalEntry, PermissionActions.Export);

    // ----- Fiscal Year -----
    public static readonly string FiscalYearView = PermissionActions.For(AccountingPermissionResources.FiscalYear, PermissionActions.View);
    public static readonly string FiscalYearCreate = PermissionActions.For(AccountingPermissionResources.FiscalYear, PermissionActions.Create);
    public static readonly string FiscalYearClose = PermissionActions.For(AccountingPermissionResources.FiscalYear, PermissionActions.ClosePeriod);
    public static readonly string FiscalYearReopen = PermissionActions.For(AccountingPermissionResources.FiscalYear, PermissionActions.ReopenPeriod);

    // ----- Accounting Period -----
    public static readonly string AccountingPeriodView = PermissionActions.For(AccountingPermissionResources.AccountingPeriod, PermissionActions.View);
    public static readonly string AccountingPeriodClose = PermissionActions.For(AccountingPermissionResources.AccountingPeriod, PermissionActions.ClosePeriod);
    public static readonly string AccountingPeriodReopen = PermissionActions.For(AccountingPermissionResources.AccountingPeriod, PermissionActions.ReopenPeriod);

    // ----- Chart of Accounts -----
    public static readonly string ChartOfAccountsView = PermissionActions.For(AccountingPermissionResources.ChartOfAccounts, PermissionActions.View);
    public static readonly string ChartOfAccountsCreate = PermissionActions.For(AccountingPermissionResources.ChartOfAccounts, PermissionActions.Create);
    public static readonly string ChartOfAccountsEdit = PermissionActions.For(AccountingPermissionResources.ChartOfAccounts, PermissionActions.Edit);

    // ----- Accounting Rule Engine (Configuration Change) -----
    public static readonly string AccountingRuleView = PermissionActions.For(AccountingPermissionResources.AccountingRule, PermissionActions.View);
    public static readonly string AccountingRuleConfigure = PermissionActions.For(AccountingPermissionResources.AccountingRule, PermissionActions.Configuration);

    // ----- Distribution Engine (Configuration Change) -----
    public static readonly string DistributionTemplateView = PermissionActions.For(AccountingPermissionResources.DistributionTemplate, PermissionActions.View);
    public static readonly string DistributionTemplateConfigure = PermissionActions.For(AccountingPermissionResources.DistributionTemplate, PermissionActions.Configuration);

    /// <summary>
    /// Every Accounting permission this catalog defines, paired with the
    /// display metadata a future database seed step needs to populate the
    /// <see cref="ERP.Domain.Entities.Security.Permission"/> table. Kept
    /// here (not executed as an actual seed yet - no database connection
    /// exists in this milestone) so the seed step, whenever implemented,
    /// has a single authoritative source instead of re-deriving this list.
    /// </summary>
    public static IReadOnlyList<PermissionSeedDescriptor> GetSeedDescriptors() => new List<PermissionSeedDescriptor>
    {
        new("Accounting", AccountingPermissionResources.JournalEntry, PermissionActions.View, "عرض القيود المحاسبية", "View Journal Entries"),
        new("Accounting", AccountingPermissionResources.JournalEntry, PermissionActions.Create, "إنشاء قيد محاسبي", "Create Journal Entry"),
        new("Accounting", AccountingPermissionResources.JournalEntry, PermissionActions.Edit, "تعديل قيد محاسبي (مسودة)", "Edit Journal Entry (Draft)"),
        new("Accounting", AccountingPermissionResources.JournalEntry, PermissionActions.Delete, "حذف قيد محاسبي (منطقي)", "Delete Journal Entry (Logical)"),
        new("Accounting", AccountingPermissionResources.JournalEntry, PermissionActions.Approve, "اعتماد قيد محاسبي", "Approve Journal Entry"),
        new("Accounting", AccountingPermissionResources.JournalEntry, PermissionActions.Reject, "رفض قيد محاسبي", "Reject Journal Entry"),
        new("Accounting", AccountingPermissionResources.JournalEntry, PermissionActions.Post, "ترحيل قيد محاسبي", "Post Journal Entry"),
        new("Accounting", AccountingPermissionResources.JournalEntry, PermissionActions.Reverse, "عكس قيد محاسبي", "Reverse Journal Entry"),
        new("Accounting", AccountingPermissionResources.JournalEntry, PermissionActions.Print, "طباعة قيد محاسبي", "Print Journal Entry"),
        new("Accounting", AccountingPermissionResources.JournalEntry, PermissionActions.Export, "تصدير قيود محاسبية", "Export Journal Entries"),

        new("Accounting", AccountingPermissionResources.FiscalYear, PermissionActions.View, "عرض السنوات المالية", "View Fiscal Years"),
        new("Accounting", AccountingPermissionResources.FiscalYear, PermissionActions.Create, "إنشاء سنة مالية", "Create Fiscal Year"),
        new("Accounting", AccountingPermissionResources.FiscalYear, PermissionActions.ClosePeriod, "إقفال السنة المالية", "Close Fiscal Year"),
        new("Accounting", AccountingPermissionResources.FiscalYear, PermissionActions.ReopenPeriod, "إعادة فتح السنة المالية", "Reopen Fiscal Year"),

        new("Accounting", AccountingPermissionResources.AccountingPeriod, PermissionActions.View, "عرض الفترات المحاسبية", "View Accounting Periods"),
        new("Accounting", AccountingPermissionResources.AccountingPeriod, PermissionActions.ClosePeriod, "إقفال الفترة المحاسبية", "Close Accounting Period"),
        new("Accounting", AccountingPermissionResources.AccountingPeriod, PermissionActions.ReopenPeriod, "إعادة فتح الفترة المحاسبية", "Reopen Accounting Period"),

        new("Accounting", AccountingPermissionResources.ChartOfAccounts, PermissionActions.View, "عرض دليل الحسابات", "View Chart of Accounts"),
        new("Accounting", AccountingPermissionResources.ChartOfAccounts, PermissionActions.Create, "إنشاء حساب", "Create Account"),
        new("Accounting", AccountingPermissionResources.ChartOfAccounts, PermissionActions.Edit, "تعديل حساب", "Edit Account"),

        new("Accounting", AccountingPermissionResources.AccountingRule, PermissionActions.View, "عرض قواعد المحاسبة", "View Accounting Rules"),
        new("Accounting", AccountingPermissionResources.AccountingRule, PermissionActions.Configuration, "تهيئة قواعد المحاسبة", "Configure Accounting Rules"),

        new("Accounting", AccountingPermissionResources.DistributionTemplate, PermissionActions.View, "عرض قوالب التوزيع", "View Distribution Templates"),
        new("Accounting", AccountingPermissionResources.DistributionTemplate, PermissionActions.Configuration, "تهيئة قوالب التوزيع", "Configure Distribution Templates"),
    };
}

/// <summary>Plain data carrier for <see cref="AccountingPermissions.GetSeedDescriptors"/> - deliberately not a Domain entity itself, just the shape a future seeding script needs to call <c>Permission.Create(...)</c> for each row.</summary>
public sealed record PermissionSeedDescriptor(string Module, string Resource, string Action, string NameAr, string NameEn);
