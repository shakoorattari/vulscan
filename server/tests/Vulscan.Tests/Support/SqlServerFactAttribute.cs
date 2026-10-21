namespace Vulscan.Tests.Support;

/// <summary>
/// Integration test that needs a real SQL Server. Skipped unless <c>VULSCAN_TEST_SQL</c> holds a connection string
/// to a server where the test may create and drop databases (it never touches the database named in the string).
/// </summary>
public sealed class SqlServerFactAttribute : FactAttribute
{
    public const string EnvVar = "VULSCAN_TEST_SQL";

    public SqlServerFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(EnvVar)))
            Skip = $"Set {EnvVar} to run SQL Server integration tests.";
    }

    public static string ConnectionString =>
        Environment.GetEnvironmentVariable(EnvVar) ?? throw new InvalidOperationException($"{EnvVar} is not set.");
}
