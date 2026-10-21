namespace Vulscan.Infrastructure.Intel;

public enum SourceStatus { Ok, NotFound, Error, Skipped, Disabled }

/// <summary>The outcome of asking one source. A failure is a value, never an exception.</summary>
public sealed record SourceResult<T>(SourceStatus Status, T? Value, string? Message = null)
{
    public static SourceResult<T> Ok(T value) => new(SourceStatus.Ok, value);
    public static SourceResult<T> NotFound(string? message = null) => new(SourceStatus.NotFound, default, message);
    public static SourceResult<T> Error(string message) => new(SourceStatus.Error, default, message);
    public static SourceResult<T> Skipped(string message) => new(SourceStatus.Skipped, default, message);
    public static SourceResult<T> Disabled() => new(SourceStatus.Disabled, default, "Disabled in configuration.");

    public bool IsOk => Status == SourceStatus.Ok;
}
