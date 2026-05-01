using Cronos;

namespace Vulscan.Application.Common;

/// <summary>
/// Helpers for parsing/validating standard 5-field cron expressions used by the scheduler.
/// All evaluations are in UTC.
/// </summary>
public static class CronExpressionHelper
{
    /// <summary>Returns true and outputs a parsed <see cref="CronExpression"/> if valid; otherwise false.</summary>
    public static bool TryParse(string? expression, out CronExpression? cron)
    {
        cron = null;
        if (string.IsNullOrWhiteSpace(expression)) return false;
        try
        {
            cron = CronExpression.Parse(expression.Trim(), CronFormat.Standard);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Throws <see cref="ArgumentException"/> if the expression is not a valid 5-field cron.</summary>
    public static CronExpression ParseOrThrow(string expression)
    {
        if (!TryParse(expression, out var cron) || cron is null)
            throw new ArgumentException($"Invalid cron expression: '{expression}'", nameof(expression));
        return cron;
    }

    /// <summary>Compute the next occurrence (strictly after <paramref name="afterUtc"/>) in UTC, or null if none.</summary>
    public static DateTime? NextOccurrence(string expression, DateTime afterUtc)
        => TryParse(expression, out var cron) && cron is not null
            ? cron.GetNextOccurrence(DateTime.SpecifyKind(afterUtc, DateTimeKind.Utc))
            : null;

    /// <summary>Best-effort, dependency-free human description of a 5-field cron.</summary>
    public static string Describe(string expression)
    {
        if (!TryParse(expression, out _)) return "Invalid cron expression";
        var parts = expression.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 5) return expression;

        var (m, h, dom, mon, dow) = (parts[0], parts[1], parts[2], parts[3], parts[4]);

        // Common shortcut: every day at HH:MM
        if (mon == "*" && dom == "*" && dow == "*" && IsNumeric(h) && IsNumeric(m))
            return $"Every day at {int.Parse(h):D2}:{int.Parse(m):D2} UTC";

        // Every N hours
        if (mon == "*" && dom == "*" && dow == "*" && m == "0" && h.StartsWith("*/"))
            return $"Every {h[2..]} hour(s) UTC";

        // Weekly: HH:MM on weekday
        if (mon == "*" && dom == "*" && IsNumeric(h) && IsNumeric(m) && IsWeekday(dow))
            return $"Every {WeekdayName(dow)} at {int.Parse(h):D2}:{int.Parse(m):D2} UTC";

        return $"Cron: {expression} (UTC)";
    }

    private static bool IsNumeric(string s) => int.TryParse(s, out _);
    private static bool IsWeekday(string s) => int.TryParse(s, out var n) && n is >= 0 and <= 6;
    private static string WeekdayName(string s) => int.Parse(s) switch
    {
        0 => "Sunday", 1 => "Monday", 2 => "Tuesday", 3 => "Wednesday",
        4 => "Thursday", 5 => "Friday", 6 => "Saturday", _ => s,
    };
}
