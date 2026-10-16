namespace Nexus.Gateway.Dtos.Wallet;

public sealed record WalletResponseDto(string Id, string UserId, string Balance, string Currency);
public sealed record WalletTransactionResponseDto(string Id, string WalletId, string Type, string Amount, string Status, string CreatedAt);
