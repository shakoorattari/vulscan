namespace Vulscan.Application.Common;

/// <summary>
/// CSV helpers. <see cref="Escape"/> also neutralises spreadsheet formula injection: a cell that starts with
/// <c>=</c>, <c>+</c>, <c>-</c>, <c>@</c>, tab or CR is executed as a formula by Excel/LibreOffice, and several
/// exported fields (advisory text, usernames, package names) originate outside this system.
/// </summary>
public static class Csv
{
    private static readonly char[] FormulaPrefixes = ['=', '+', '-', '@', '\t', '\r'];

    public static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";

        if (Array.IndexOf(FormulaPrefixes, value[0]) >= 0)
            value = "'" + value;

        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            return $"\"{value.Replace("\"", "\"\"")}\"";

        return value;
    }

    public static string Row(params object?[] cells) =>
        string.Join(",", cells.Select(c => Escape(c switch
        {
            null => null,
            DateTime d => d.ToString("yyyy-MM-dd HH:mm:ss'Z'", System.Globalization.CultureInfo.InvariantCulture),
            IFormattable f => f.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
            _ => c.ToString()
        })));
}
