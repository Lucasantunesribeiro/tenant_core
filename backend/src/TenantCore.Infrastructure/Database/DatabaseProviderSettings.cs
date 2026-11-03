using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace TenantCore.Infrastructure.Database;

public enum DatabaseProvider
{
    SqlServer,
    PostgreSQL,
}

/// <summary>
/// Escolha do banco relacional por configuração (<c>Database:Provider</c>).
/// SQL Server é o padrão e o alvo principal; PostgreSQL existe para rodar a demo em
/// hospedagem gratuita (Neon, Supabase, Render). Cada provider tem as próprias migrations:
/// SQL Server neste assembly, PostgreSQL em <c>TenantCore.Infrastructure.Postgres</c>.
/// </summary>
public static class DatabaseProviderSettings
{
    public const string ProviderKey = "Database:Provider";
    public const string SqlServerConnectionName = "SqlServer";
    public const string PostgresConnectionName = "Postgres";
    public const string PostgresMigrationsAssembly = "TenantCore.Infrastructure.Postgres";

    public static DatabaseProvider GetProvider(IConfiguration configuration)
    {
        var raw = configuration[ProviderKey];
        if (string.IsNullOrWhiteSpace(raw))
        {
            return DatabaseProvider.SqlServer;
        }

        return raw.Trim().ToLowerInvariant() switch
        {
            "sqlserver" or "mssql" => DatabaseProvider.SqlServer,
            "postgresql" or "postgres" or "npgsql" => DatabaseProvider.PostgreSQL,
            _ => throw new InvalidOperationException(
                $"{ProviderKey} '{raw}' is not supported. Use 'SqlServer' or 'PostgreSQL'."),
        };
    }

    public static string GetConnectionString(IConfiguration configuration, DatabaseProvider provider)
    {
        var name = provider == DatabaseProvider.PostgreSQL ? PostgresConnectionName : SqlServerConnectionName;
        var value = configuration.GetConnectionString(name);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Connection string '{name}' is missing.");
        }

        return provider == DatabaseProvider.PostgreSQL ? NormalizePostgresConnectionString(value) : value;
    }

    public static DbContextOptionsBuilder UseConfiguredDatabase(
        this DbContextOptionsBuilder options,
        DatabaseProvider provider,
        string connectionString,
        bool enableRetryOnFailure = false)
    {
        return provider switch
        {
            DatabaseProvider.PostgreSQL => options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsAssembly(PostgresMigrationsAssembly);
                if (enableRetryOnFailure)
                {
                    npgsql.EnableRetryOnFailure();
                }
            }),
            _ => options.UseSqlServer(connectionString, sql =>
            {
                sql.MigrationsAssembly(typeof(TenantCoreDbContext).Assembly.FullName);
                if (enableRetryOnFailure)
                {
                    sql.EnableRetryOnFailure();
                }
            }),
        };
    }

    /// <summary>Conexão ADO.NET crua para consultas Dapper no provider configurado.</summary>
    public static DbConnection CreateConnection(IConfiguration configuration)
    {
        var provider = GetProvider(configuration);
        var connectionString = GetConnectionString(configuration, provider);
        return provider == DatabaseProvider.PostgreSQL
            ? new NpgsqlConnection(connectionString)
            : new SqlConnection(connectionString);
    }

    /// <summary>
    /// Aceita tanto o formato chave=valor do Npgsql quanto a URL <c>postgres://user:senha@host:porta/banco?sslmode=require</c>
    /// que Neon, Supabase e Render exibem. A URL é convertida para o formato do Npgsql.
    /// </summary>
    public static string NormalizePostgresConnectionString(string value)
    {
        var trimmed = value.Trim();
        if (!trimmed.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) &&
            !trimmed.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        var uri = new Uri(trimmed);
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort || uri.Port <= 0 ? 5432 : uri.Port,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
        };

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            var separator = uri.UserInfo.IndexOf(':');
            builder.Username = Uri.UnescapeDataString(separator < 0 ? uri.UserInfo : uri.UserInfo[..separator]);
            if (separator >= 0)
            {
                builder.Password = Uri.UnescapeDataString(uri.UserInfo[(separator + 1)..]);
            }
        }

        foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            var key = Uri.UnescapeDataString(parts[0]).ToLowerInvariant();
            var parameterValue = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty;

            switch (key)
            {
                case "sslmode":
                    builder.SslMode = Enum.Parse<SslMode>(parameterValue.Replace("-", string.Empty), ignoreCase: true);
                    break;
                case "channel_binding":
                    builder.ChannelBinding = Enum.Parse<ChannelBinding>(parameterValue, ignoreCase: true);
                    break;
            }
        }

        return builder.ConnectionString;
    }
}
