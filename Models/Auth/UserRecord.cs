namespace CoreInventory.Models.Auth;

public sealed class UserRecord
{
    public long Id { get; init; }

    public string LoginId { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string PasswordHash { get; init; } = string.Empty;

    public bool IsActive { get; init; }
}
