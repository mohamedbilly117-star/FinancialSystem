namespace ERP.Shared.Constants;

/// <summary>
/// Names of the configurable numbering sequences approved in Prompt 4
/// ("Numbering Strategy") and Prompt 11 ("Numbering Configuration") -
/// Journal, Voucher, Check, Transfer, Contract, Advance, Aid, Card,
/// Notification numbers. These are just the *keys* used to look up each
/// sequence's configured format/prefix/reset-policy from the Configuration
/// module (built in a later milestone) - the actual formats are
/// administrator-configurable data, never hardcoded strings, per the
/// explicit rule in Prompt 4: "Support configurable numbering policies."
/// </summary>
public static class NumberingSequenceKeys
{
    public const string JournalNumber = "JOURNAL";
    public const string VoucherNumber = "VOUCHER";
    public const string ReceiptNumber = "RECEIPT";
    public const string PaymentNumber = "PAYMENT";
    public const string TransferNumber = "TRANSFER";
    public const string CheckNumber = "CHECK";
    public const string ContractNumber = "CONTRACT";
    public const string AdvanceNumber = "ADVANCE";
    public const string AidNumber = "AID";
    public const string CardNumber = "CARD";
    public const string WorkflowNumber = "WORKFLOW";
    public const string NotificationNumber = "NOTIFICATION";
}
