namespace Vulscan.Application.Services;

/// <summary>
/// Parses Azure DevOps URLs into (baseUrl, collection, projectName).
/// Examples:
///   https://devops.ishj.ae/SDD/CTS                 → (https://devops.ishj.ae, SDD, CTS)
///   https://devops.ishj.ae/tfs/SDD/CTS             → (https://devops.ishj.ae/tfs, SDD, CTS)
///   https://dev.azure.com/myorg/MyProject          → (https://dev.azure.com, myorg, MyProject)
/// </summary>
internal static class AzureDevOpsUrlParser
{
    public static (string BaseUrl, string Collection, string ProjectName) Parse(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("Project URL is required.");

        url = url.TrimEnd('/');
        var uri = new Uri(url);
        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length < 2)
            throw new ArgumentException("Invalid Azure DevOps URL. Expected: https://server/collection/project");

        var baseUrl = $"{uri.Scheme}://{uri.Host}";
        if (uri.Port != 80 && uri.Port != 443 && !uri.IsDefaultPort)
            baseUrl += $":{uri.Port}";

        int collectionIndex = 0;
        if (segments.Length > 2 && segments[0].Equals("tfs", StringComparison.OrdinalIgnoreCase))
        {
            baseUrl += "/tfs";
            collectionIndex = 1;
        }

        if (segments.Length <= collectionIndex + 1)
            throw new ArgumentException("URL is missing the project segment.");

        return (baseUrl, segments[collectionIndex], segments[collectionIndex + 1]);
    }
}
