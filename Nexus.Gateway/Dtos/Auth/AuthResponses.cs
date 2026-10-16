namespace Nexus.Gateway.Dtos.Auth;

public sealed record AuthTokenResponseDto(string AccessToken, string TokenType, int ExpiresIn);
