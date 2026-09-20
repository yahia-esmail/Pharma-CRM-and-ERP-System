using System.Globalization;
using System.Text;

namespace PharmaERP.Web.Mvc.Infrastructure;

/// <summary>
/// Minimal CSV writer for the spec 4.8 "Export to Excel/PDF for all major reports" requirement — CSV
/// opens natively in Excel and needs no extra dependency, so it covers the requirement for this
/// scaffold; swap in a real Excel/PDF library later if native .xlsx/.pdf formatting is required.
/// </summary>
public static class CsvExport
{
    public static byte[] Build(IEnumerable<string> headers, IEnumerable<IEnumerable<object?>> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(',', headers.Select(Escape)));
        foreach (var row in rows)
            sb.AppendLine(string.Join(',', row.Select(Escape)));

        // UTF-8 BOM so Excel renders non-ASCII (e.g. currency symbols) correctly on Windows.
        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(sb.ToString())];
    }

    private static string Escape(object? value)
    {
        var text = value switch
        {
            null => string.Empty,
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty
        };

        return text.Contains(',') || text.Contains('"') || text.Contains('\n')
            ? $"\"{text.Replace("\"", "\"\"")}\""
            : text;
    }
}
