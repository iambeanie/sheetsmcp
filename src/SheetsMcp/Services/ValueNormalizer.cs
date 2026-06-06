using System.Text.Json;
using SheetsMcp.Errors;

namespace SheetsMcp.Services;

public static class ValueNormalizer
{
    public static IReadOnlyList<IReadOnlyList<object?>> NormalizeRows(IReadOnlyList<IReadOnlyList<object?>> rows, string argumentName)
    {
        if (rows.Count == 0)
        {
            throw ToolError.InvalidInput($"{argumentName} must contain at least one row.");
        }

        var normalized = new List<IReadOnlyList<object?>>(rows.Count);
        foreach (var row in rows)
        {
            var normalizedRow = new List<object?>(row.Count);
            foreach (var value in row)
            {
                normalizedRow.Add(NormalizeCellValue(value));
            }

            normalized.Add(normalizedRow);
        }

        return normalized;
    }

    public static int MaxColumnCount(IReadOnlyList<IReadOnlyList<object?>> rows) => rows.Count == 0 ? 0 : rows.Max(row => row.Count);

    private static object? NormalizeCellValue(object? value)
    {
        if (value is null)
        {
            return string.Empty;
        }

        if (value is string or bool or int or long or double or decimal)
        {
            return value;
        }

        if (value is JsonElement json)
        {
            return json.ValueKind switch
            {
                JsonValueKind.String => json.GetString(),
                JsonValueKind.Number when json.TryGetInt64(out var longValue) => longValue,
                JsonValueKind.Number when json.TryGetDouble(out var doubleValue) => doubleValue,
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => string.Empty,
                _ => throw ToolError.InvalidInput("Cell values must be strings, numbers, booleans, or null.")
            };
        }

        return value.ToString();
    }
}
