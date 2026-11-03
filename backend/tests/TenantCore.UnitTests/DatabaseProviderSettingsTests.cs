using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Npgsql;
using TenantCore.Infrastructure.Database;

namespace TenantCore.UnitTests;

public sealed class DatabaseProviderSettingsTests
{
    private static IConfiguration Config(params (string Key, string? Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value)))
            .Build();

    [Fact]
    public void GetProvider_ShouldDefaultToSqlServer_WhenNotConfigured()
    {
        DatabaseProviderSettings.GetProvider(Config()).Should().Be(DatabaseProvider.SqlServer);
    }

    [Theory]
    [InlineData("PostgreSQL")]
    [InlineData("postgres")]
    [InlineData(" Npgsql ")]
    public void GetProvider_ShouldAcceptPostgresAliases(string value)
    {
        DatabaseProviderSettings.GetProvider(Config(("Database:Provider", value))).Should().Be(DatabaseProvider.PostgreSQL);
    }

    [Fact]
    public void GetProvider_ShouldRejectUnknownProvider()
    {
        var act = () => DatabaseProviderSettings.GetProvider(Config(("Database:Provider", "sqlite")));
        act.Should().Throw<InvalidOperationException>().WithMessage("*sqlite*");
    }

    [Fact]
    public void GetConnectionString_ShouldFailFast_WhenProviderConnectionIsMissing()
    {
        // Provider PostgreSQL ignora a connection string de SQL Server: não cai num banco errado em silêncio.
        var configuration = Config(
            ("Database:Provider", "PostgreSQL"),
            ("ConnectionStrings:SqlServer", "Server=localhost;Database=x"));

        var act = () => DatabaseProviderSettings.GetConnectionString(configuration, DatabaseProvider.PostgreSQL);
        act.Should().Throw<InvalidOperationException>().WithMessage("*'Postgres'*");
    }

    [Fact]
    public void NormalizePostgresConnectionString_ShouldConvertProviderUrl()
    {
        // Formato exibido por Neon/Supabase/Render; senha com caracteres codificados na URL.
        var url = "postgresql://demo_user:p%40ss%3Aword@ep-example-123.sa-east-1.aws.neon.tech/tenant_core?sslmode=require&channel_binding=require";

        var builder = new NpgsqlConnectionStringBuilder(DatabaseProviderSettings.NormalizePostgresConnectionString(url));

        builder.Host.Should().Be("ep-example-123.sa-east-1.aws.neon.tech");
        builder.Port.Should().Be(5432);
        builder.Database.Should().Be("tenant_core");
        builder.Username.Should().Be("demo_user");
        builder.Password.Should().Be("p@ss:word");
        builder.SslMode.Should().Be(SslMode.Require);
        builder.ChannelBinding.Should().Be(ChannelBinding.Require);
    }

    [Fact]
    public void NormalizePostgresConnectionString_ShouldKeepExplicitPortAndKeyValueFormat()
    {
        var fromUrl = new NpgsqlConnectionStringBuilder(
            DatabaseProviderSettings.NormalizePostgresConnectionString("postgres://u:p@db.internal:6543/app"));
        fromUrl.Port.Should().Be(6543);
        fromUrl.SslMode.Should().Be(SslMode.Prefer);

        const string keyValue = "Host=localhost;Port=5432;Database=tenant_core;Username=postgres;Password=x";
        DatabaseProviderSettings.NormalizePostgresConnectionString(keyValue).Should().Be(keyValue);
    }
}
