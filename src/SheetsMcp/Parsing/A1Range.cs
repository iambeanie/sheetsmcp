using System.Text.RegularExpressions;
using SheetsMcp.Errors;

namespace SheetsMcp.Parsing;

public sealed record A1Range(
    string Original,
    string? SheetName,
    string StartColumn,
    int StartRow,
    string EndColumn,
    int EndRow)
{
    public int StartColumnIndex => A1RangeParser.ColumnToIndex(StartColumn);
    public int EndColumnIndex => A1RangeParser.ColumnToIndex(EndColumn);
    public int RowCount => EndRow - StartRow + 1;
    public int ColumnCount => EndColumnIndex - StartColumnIndex + 1;
    public int CellCount => RowCount * ColumnCount;
}

public static class A1RangeParser
{
    private static readonly Regex CellPattern = new(@"^\$?([A-Za-z]{1,3})\$?([1-9][0-9]*)$", RegexOptions.Compiled);
    private static readonly Regex PlainSheetNamePattern = new(@"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);
    private static readonly char[] InvalidSheetNameChars = ['[', ']', ':', '*', '?', '/', '\\'];

    public static A1Range ParseBounded(string range)
    {
        if (!TryParseBounded(range, out var parsed, out var error))
        {
            throw ToolError.InvalidInput(error ?? "Invalid A1 range.");
        }

        return parsed;
    }

    public static bool TryParseBounded(string range, out A1Range parsed, out string? error)
    {
        parsed = null!;
        error = null;

        if (string.IsNullOrWhiteSpace(range))
        {
            error = "A bounded A1 range is required.";
            return false;
        }

        var original = range.Trim();
        if (!TrySplitSheetName(original, out var sheetName, out var rangePart, out error))
        {
            return false;
        }

        var rangeParts = rangePart.Split(':', StringSplitOptions.TrimEntries);
        if (rangeParts.Length is < 1 or > 2)
        {
            error = "A1 ranges must be a single cell or a bounded cell range.";
            return false;
        }

        var startMatch = CellPattern.Match(rangeParts[0]);
        if (!startMatch.Success)
        {
            error = "A1 ranges must include a start cell such as A1.";
            return false;
        }

        var endPart = rangeParts.Length == 2 ? rangeParts[1] : rangeParts[0];
        var endMatch = CellPattern.Match(endPart);
        if (!endMatch.Success)
        {
            error = "Write ranges must be bounded with an end cell such as A1:B10.";
            return false;
        }

        var startColumn = startMatch.Groups[1].Value.ToUpperInvariant();
        var endColumn = endMatch.Groups[1].Value.ToUpperInvariant();
        var startRow = int.Parse(startMatch.Groups[2].Value);
        var endRow = int.Parse(endMatch.Groups[2].Value);
        var startColumnIndex = ColumnToIndex(startColumn);
        var endColumnIndex = ColumnToIndex(endColumn);

        if (endRow < startRow || endColumnIndex < startColumnIndex)
        {
            error = "A1 range end cell must be after the start cell.";
            return false;
        }

        parsed = new A1Range(original, sheetName, startColumn, startRow, endColumn, endRow);
        return true;
    }

    public static string NormalizeRangeOrSheet(string rangeOrSheet)
    {
        if (TryParseBounded(rangeOrSheet, out var parsed, out _))
        {
            return parsed.Original;
        }

        return QuoteSheetName(ValidateSheetName(rangeOrSheet));
    }

    public static string QuoteSheetName(string sheetName)
    {
        var validated = ValidateSheetName(sheetName);
        if (PlainSheetNamePattern.IsMatch(validated))
        {
            return validated;
        }

        return $"'{validated.Replace("'", "''", StringComparison.Ordinal)}'";
    }

    public static string CellReference(string? sheetName, int row, int columnIndex)
    {
        var cell = $"{IndexToColumn(columnIndex)}{row}";
        return string.IsNullOrWhiteSpace(sheetName) ? cell : $"{QuoteSheetName(sheetName)}!{cell}";
    }

    public static int ColumnToIndex(string column)
    {
        var index = 0;
        foreach (var c in column.ToUpperInvariant())
        {
            if (c is < 'A' or > 'Z')
            {
                throw ToolError.InvalidInput("Invalid A1 column.");
            }

            index = (index * 26) + c - 'A' + 1;
        }

        return index;
    }

    private static string IndexToColumn(int index)
    {
        if (index <= 0)
        {
            throw ToolError.InvalidInput("Column index must be greater than zero.");
        }

        var chars = new Stack<char>();
        while (index > 0)
        {
            index--;
            chars.Push((char)('A' + (index % 26)));
            index /= 26;
        }

        return new string(chars.ToArray());
    }

    private static string ValidateSheetName(string sheetName)
    {
        if (string.IsNullOrWhiteSpace(sheetName))
        {
            throw ToolError.InvalidInput("A sheet name or bounded A1 range is required.");
        }

        var value = sheetName.Trim();
        if (value.Length > 100 || value.Any(char.IsControl) || value.IndexOfAny(InvalidSheetNameChars) >= 0)
        {
            throw ToolError.InvalidInput("The sheet name contains unsupported characters.");
        }

        return value;
    }

    private static bool TrySplitSheetName(string value, out string? sheetName, out string rangePart, out string? error)
    {
        sheetName = null;
        rangePart = value;
        error = null;

        if (!value.Contains('!', StringComparison.Ordinal))
        {
            return true;
        }

        if (value.StartsWith('\''))
        {
            var sheet = new List<char>();
            for (var i = 1; i < value.Length; i++)
            {
                if (value[i] == '\'')
                {
                    if (i + 1 < value.Length && value[i + 1] == '\'')
                    {
                        sheet.Add('\'');
                        i++;
                        continue;
                    }

                    if (i + 1 < value.Length && value[i + 1] == '!')
                    {
                        sheetName = ValidateSheetName(new string(sheet.ToArray()));
                        rangePart = value[(i + 2)..];
                        return true;
                    }
                }

                sheet.Add(value[i]);
            }

            error = "Quoted sheet names must be followed by ! and an A1 range.";
            return false;
        }

        var bangIndex = value.IndexOf('!', StringComparison.Ordinal);
        sheetName = ValidateSheetName(value[..bangIndex]);
        rangePart = value[(bangIndex + 1)..];
        return true;
    }
}
