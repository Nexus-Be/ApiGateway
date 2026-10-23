namespace Nexus.Gateway.Dtos.Users;

public sealed record UserProfileDto(string Id, string Username, string Email, string DisplayName, string[] Roles, string Status);
