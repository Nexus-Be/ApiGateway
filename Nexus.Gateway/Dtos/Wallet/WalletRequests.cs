namespace Nexus.Gateway.Dtos.Wallet;

public sealed record WalletDepositRequestDto(string Amount, string? Currency, string? PaymentMethod);
public sealed record WalletWithdrawRequestDto(string Amount, string? Currency, string BankAccount);
public sealed record WalletTransferRequestDto(string RecipientUserId, string Amount, string? Note);

// Compatibility aliases
public sealed record WalletDepositBody(string Amount, string? Currency, string? PaymentMethod);
public sealed record WalletWithdrawBody(string Amount, string? Currency, string BankAccount);
public sealed record WalletTransferBody(string RecipientUserId, string Amount, string? Note);
