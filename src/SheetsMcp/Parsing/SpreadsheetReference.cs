using System.Text.RegularExpressions;
using SheetsMcp.Errors;

namespace SheetsMcp.Parsing;

public static partial class SpreadsheetReference
{
    private static readonly Regex SpreadsheetIdPattern = new("^[A-Za-z0-9_-]{20,128}$", RegexOptions.Compiled);

    public static string Normalize(string spreadsheet)
    {
        if (string.IsNullOrWhiteSpace(spreadsheet))
        {
            throw ToolError.InvalidInput("A spreadsheet URL or spreadsheet ID is required.");
        }

        var value = spreadsheet.Trim();
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return ExtractFromUri(uri);
        }

        if (!SpreadsheetIdPattern.IsMatch(value))
        {
            throw ToolError.InvalidInput("The spreadsheet argument must be a Google Sheets URL or a valid spreadsheet ID.");
        }

        return value;
    }

    private static string ExtractFromUri(Uri uri)
    {
        if (!uri.Host.Equals("docs.google.com", StringComparison.OrdinalIgnoreCase))
        {
            throw ToolError.InvalidInput("Only docs.google.com spreadsheet URLs are supported.");
        }

        var parts = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < parts.Length - 2; i++)
        {
            if (parts[i].Equals("spreadsheets", StringComparison.OrdinalIgnoreCase) &&
                parts[i + 1].Equals("d", StringComparison.OrdinalIgnoreCase))
            {
                var id = Uri.UnescapeDataString(parts[i + 2]);
                if (SpreadsheetIdPattern.IsMatch(id))
                {
                    return id;
                }
            }
        }

        throw ToolError.InvalidInput("The spreadsheet URL must contain /spreadsheets/d/{spreadsheetId}.");
    }
}
