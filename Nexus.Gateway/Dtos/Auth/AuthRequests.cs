namespace Nexus.Gateway.Dtos.Auth;

using System.Text.Json.Serialization;

public sealed record RegisterRequestDto(
    string? Username,
    string Email,
    string Password,
    string? DisplayName = null,
    string? FirstName = null,
    string? LastName = null,
    string? PhoneNumber = null);

public sealed record LoginRequestDto(
    string? Username,
    string Password,
    string? Identifier = null,
    string? DeviceInfo = null,
    string? IpAddress = null)
{
    [JsonIgnore]
    public string EffectiveUsername => !string.IsNullOrWhiteSpace(Username) ? Username : (Identifier ?? string.Empty);
}

/// <summary>
/// Body for the OTP verification endpoint. The Gateway contract historically used <c>otp</c>,
/// while the Excel F1 contract uses <c>otp_code</c>; both are accepted and <see cref="ResolvedOtp"/>
/// picks whichever the client supplied.
/// </summary>
public sealed record VerifyOtpRequestDto(
    string Email,
    string? Otp = null,
    [property: JsonPropertyName("otp_code")] string? OtpCode = null)
{
    [JsonIgnore]
    public string? ResolvedOtp => !string.IsNullOrWhiteSpace(Otp) ? Otp : OtpCode;
}

public sealed record ResendOtpRequestDto(
    string Email,
    string? Type = null);

// Compatibility aliases
public sealed record RegisterBody(
    string? Username,
    string Email,
    string Password,
    string? DisplayName = null,
    string? FirstName = null,
    string? LastName = null,
    string? PhoneNumber = null);

public sealed record LoginBody(
    string Username,
    string Password);
