using CoreInventory.Data;
using CoreInventory.Models.Auth;
using CoreInventory.ViewModels.Account;
using Npgsql;

namespace CoreInventory.Services;

public sealed class AuthService
{
    private readonly IPostgresConnectionFactory _connectionFactory;
    private readonly PasswordService _passwordService;

    public AuthService(IPostgresConnectionFactory connectionFactory, PasswordService passwordService)
    {
        _connectionFactory = connectionFactory;
        _passwordService = passwordService;
    }

    public async Task<UserRecord?> ValidateLoginAsync(string loginId, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(loginId) || string.IsNullOrWhiteSpace(password))
        {
            return null;
        }

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            """
            select id, login_id, display_name, email, password_hash, is_active
            from app_user
            where lower(login_id) = lower(@loginId)
            limit 1;
            """,
            connection);

        command.Parameters.AddWithValue("loginId", loginId.Trim());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var user = new UserRecord
        {
            Id = reader.GetInt64(0),
            LoginId = reader.GetString(1),
            DisplayName = reader.GetString(2),
            Email = reader.GetString(3),
            PasswordHash = reader.GetString(4),
            IsActive = reader.GetBoolean(5)
        };

        if (!user.IsActive || !_passwordService.VerifyPassword(password, user.PasswordHash))
        {
            return null;
        }

        return user;
    }

    public async Task RegisterAsync(RegisterViewModel model, CancellationToken cancellationToken = default)
    {
        var loginId = model.LoginId.Trim();
        var email = model.Email.Trim();
        var displayName = model.DisplayName.Trim();

        if (string.IsNullOrWhiteSpace(loginId))
        {
            throw new InvalidOperationException("Login ID is required.");
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new InvalidOperationException("Display name is required.");
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new InvalidOperationException("Email address is required.");
        }

        if (!_passwordService.MeetsPolicy(model.Password))
        {
            throw new InvalidOperationException("Password must contain upper, lower, special characters and be at least 8 characters long.");
        }

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);

        if (await ExistsAsync(connection, "select 1 from app_user where lower(login_id) = lower(@value) limit 1;", loginId, cancellationToken))
        {
            throw new InvalidOperationException("Login ID already exists.");
        }

        if (await ExistsAsync(connection, "select 1 from app_user where lower(email) = lower(@value) limit 1;", email, cancellationToken))
        {
            throw new InvalidOperationException("Email address already exists.");
        }

        await using var command = new NpgsqlCommand(
            """
            insert into app_user (login_id, display_name, email, password_hash, is_active, created_at, updated_at)
            values (@loginId, @displayName, @email, @passwordHash, true, now(), now());
            """,
            connection);

        command.Parameters.AddWithValue("loginId", loginId);
        command.Parameters.AddWithValue("displayName", displayName);
        command.Parameters.AddWithValue("email", email);
        command.Parameters.AddWithValue("passwordHash", _passwordService.HashPassword(model.Password));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task ResetPasswordAsync(ForgotPasswordViewModel model, CancellationToken cancellationToken = default)
    {
        var loginId = model.LoginId.Trim();
        var email = model.Email.Trim();

        if (string.IsNullOrWhiteSpace(loginId))
        {
            throw new InvalidOperationException("Login ID is required.");
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new InvalidOperationException("Email address is required.");
        }

        if (!_passwordService.MeetsPolicy(model.NewPassword))
        {
            throw new InvalidOperationException("Password must contain upper, lower, special characters and be at least 8 characters long.");
        }

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            """
            update app_user
            set password_hash = @passwordHash,
                updated_at = now()
            where lower(login_id) = lower(@loginId)
              and lower(email) = lower(@email);
            """,
            connection);

        command.Parameters.AddWithValue("passwordHash", _passwordService.HashPassword(model.NewPassword));
        command.Parameters.AddWithValue("loginId", loginId);
        command.Parameters.AddWithValue("email", email);

        var affectedRows = await command.ExecuteNonQueryAsync(cancellationToken);
        if (affectedRows == 0)
        {
            throw new InvalidOperationException("No user matched that login ID and email combination.");
        }
    }

    private static async Task<bool> ExistsAsync(
        NpgsqlConnection connection,
        string sql,
        string value,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("value", value);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is not null;
    }
}
