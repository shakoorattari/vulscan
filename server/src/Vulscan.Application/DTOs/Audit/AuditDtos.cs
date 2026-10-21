namespace Vulscan.Application.DTOs.Audit;

public sealed record AuditLogDto
{
    public Guid Id { get; init; }
    public DateTime Timestamp { get; init; }
    public Guid? UserId { get; init; }
    public string? Username { get; init; }
    public string Action { get; init; } = string.Empty;
    public string EntityType { get; init; } = string.Empty;
    public string? EntityId { get; init; }
    public string Outcome { get; init; } = string.Empty;
    public string? IpAddress { get; init; }
    public string? Details { get; init; }
}

public sealed record AuditLogFilterDto
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
    public string? Action { get; init; }
    public string? Username { get; init; }
    public string? EntityType { get; init; }
    public string? Outcome { get; init; }
    public string? Search { get; init; }
}
