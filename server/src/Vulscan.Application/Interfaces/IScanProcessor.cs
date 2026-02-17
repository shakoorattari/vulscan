namespace Vulscan.Application.Interfaces;

/// <summary>
/// Processes scan runs asynchronously.
/// </summary>
public interface IScanProcessor
{
    /// <summary>
    /// Processes a queued scan run - fetches repos, scans dependencies, detects vulnerabilities.
    /// </summary>
    Task ProcessScanAsync(int scanRunId, CancellationToken ct = default);
}
