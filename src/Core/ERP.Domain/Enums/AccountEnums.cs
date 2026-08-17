namespace ERP.Domain.Enums;

/// <summary>
/// The five fundamental account categories of double-entry accounting
/// (Prompt 5 - Accounting Model: "Revenue Accounts, Expense Accounts, Asset
/// Accounts, Liability Accounts, Equity Accounts"). Unlike business/
/// distribution rules, this taxonomy is universal accounting theory, not an
/// administrator-configurable policy, so it is intentionally a fixed enum
/// rather than reference/master data.
/// </summary>
public enum AccountType
{
    Asset = 1,
    Liability = 2,
    Equity = 3,
    Revenue = 4,
    Expense = 5,
}

/// <summary>
/// Which side of a journal entry increases this account's balance. Derived
/// conceptually from <see cref="AccountType"/> (Assets/Expenses are
/// normally Debit; Liabilities/Equity/Revenue are normally Credit) but
/// stored explicitly on <c>Account</c> rather than computed, so a
/// Suspense Account (Prompt 5: "Suspense Accounts (if required)") or any
/// future exception can override the default without special-casing code.
/// </summary>
public enum AccountNormalBalance
{
    Debit = 1,
    Credit = 2,
}

/// <summary>
/// Prompt 5 - Chart of Accounts Design: "Parent Accounts, Child Accounts,
/// Posting Accounts, Control Accounts". A Parent account exists purely to
/// group/summarize its children in reports and can never itself receive a
/// journal line; a Posting account is a normal leaf account transactions
/// post to; a Control account is a posting account whose balance must also
/// reconcile against a subsidiary ledger (Prompt 5 "Sub-Ledgers" - Banks,
/// Treasury, Advances, Aid, Cards, Contracts, Activities).
/// </summary>
public enum AccountClassification
{
    Parent = 1,
    Posting = 2,
    Control = 3,
}
