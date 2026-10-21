using System.Text.Json;

namespace Vulscan.Tests.Intel;

/// <summary>Recorded responses from the real OSV, NVD, EPSS and CISA APIs (see Fixtures/).</summary>
public static class Fixtures
{
    public static string Text(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Intel", "Fixtures", name));

    public static T Json<T>(string name) =>
        JsonSerializer.Deserialize<T>(Text(name))!;
}
