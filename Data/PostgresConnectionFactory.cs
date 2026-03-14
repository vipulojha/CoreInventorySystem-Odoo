using CoreInventory.Options;
using Microsoft.Extensions.Options;
using Npgsql;

namespace CoreInventory.Data;

public sealed class PostgresConnectionFactory : IPostgresConnectionFactory
{
    private readonly DatabaseOptions _options;

    public PostgresConnectionFactory(IOptions<DatabaseOptions> options)
    {
        _options = options.Value;
    }

    public async Task<NpgsqlConnection> OpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ConnectionString))
        {
            throw new InvalidOperationException("Database connection string is not configured.");
        }

        var connection = new NpgsqlConnection(_options.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
