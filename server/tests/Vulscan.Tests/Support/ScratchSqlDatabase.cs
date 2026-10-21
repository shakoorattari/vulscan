using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Vulscan.Infrastructure.Data;

namespace Vulscan.Tests.Support;

/// <summary>A uniquely named throwaway database on the server from <c>VULSCAN_TEST_SQL</c>, dropped on dispose.</summary>
public sealed class ScratchSqlDatabase : IAsyncDisposable
{
    private readonly string _serverConnection;

    public ScratchSqlDatabase()
    {
        var builder = new SqlConnectionStringBuilder(SqlServerFactAttribute.ConnectionString);
        _serverConnection = new SqlConnectionStringBuilder(builder.ConnectionString) { InitialCatalog = "master" }.ConnectionString;
        Name = $"vulscan_it_{Guid.NewGuid():N}";
        ConnectionString = new SqlConnectionStringBuilder(builder.ConnectionString) { InitialCatalog = Name }.ConnectionString;
    }

    public string Name { get; }
    public string ConnectionString { get; }

    public VulscanDbContext NewContext() =>
        new(new DbContextOptionsBuilder<VulscanDbContext>().UseSqlServer(ConnectionString).Options);

    public async Task<List<string>> ScalarsAsync(string sql)
    {
        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        var results = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync()) results.Add(Convert.ToString(reader.GetValue(0)) ?? "");
        return results;
    }

    public async ValueTask DisposeAsync()
    {
        await using var conn = new SqlConnection(_serverConnection);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(
            $"IF DB_ID(N'{Name}') IS NOT NULL BEGIN ALTER DATABASE [{Name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{Name}]; END", conn);
        await cmd.ExecuteNonQueryAsync();
    }
}
