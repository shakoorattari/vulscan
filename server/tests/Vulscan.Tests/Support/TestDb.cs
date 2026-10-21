using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vulscan.Infrastructure.Data;

namespace Vulscan.Tests.Support;

/// <summary>In-memory SQLite database with the real model. Disposing closes (and so deletes) the database.</summary>
public sealed class TestDb : IDisposable
{
    private readonly SqliteConnection _connection;

    public TestDb()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        using var ctx = NewContext();
        ctx.Database.EnsureCreated();
    }

    public SqliteConnection Connection => _connection;

    public VulscanDbContext NewContext() =>
        new(new DbContextOptionsBuilder<VulscanDbContext>().UseSqlite(_connection).Options);

    public void Dispose() => _connection.Dispose();
}
