namespace Nexus.Gateway.Common;

using System;
using System.Collections.Generic;

/// <summary>
/// Gateway-owned validation for the F5 wallet surface.
/// Matches real Wallet.Domain.Enums contracts.
/// </summary>
public static class WalletValidation
{
    public static readonly string[] TransactionTypes =
        ["Deposit", "Withdrawal", "Payment", "Refund", "Hold", "Release"];

    public static readonly string[] WalletStatuses =
        ["Active", "Locked", "Closed"];

    public static readonly string[] TransactionStatuses =
        ["Pending", "Success", "Failed", "Cancelled"];

    public static void EnsureOptionalTransactionType(string? type, string field = "type")
    {
        if (string.IsNullOrWhiteSpace(type)) return;
        EnumValidationHelper.EnsureOneOf(type, field, TransactionTypes);
    }
}
