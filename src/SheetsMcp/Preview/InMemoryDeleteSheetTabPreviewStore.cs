using System.Collections.Concurrent;
using SheetsMcp.Errors;

namespace SheetsMcp.Preview;

public sealed class InMemoryDeleteSheetTabPreviewStore(TimeProvider? timeProvider = null) : IDeleteSheetTabPreviewStore
{
    private static readonly TimeSpan OperationTtl = TimeSpan.FromMinutes(15);
    private readonly ConcurrentDictionary<string, DeleteSheetTabPreviewOperation> _operations = new();
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public DeleteSheetTabPreviewOperation Create(string spreadsheetId, int sheetId, string title)
    {
        PruneExpired();
        var operationId = $"delete_sheet_tab_{Guid.NewGuid():N}";
        var operation = new DeleteSheetTabPreviewOperation(
            operationId,
            spreadsheetId,
            _timeProvider.GetUtcNow().Add(OperationTtl),
            sheetId,
            title);
        _operations[operationId] = operation;
        return operation;
    }

    public DeleteSheetTabPreviewOperation Consume(string operationId)
    {
        if (string.IsNullOrWhiteSpace(operationId))
        {
            throw ToolError.InvalidInput("An operationId is required.");
        }

        if (!_operations.TryRemove(operationId.Trim(), out var operation))
        {
            throw ToolError.InvalidInput("The delete sheet tab operation ID is unknown or already used.");
        }

        if (operation.ExpiresAt <= _timeProvider.GetUtcNow())
        {
            throw ToolError.InvalidInput("The delete sheet tab operation ID has expired.");
        }

        return operation;
    }

    private void PruneExpired()
    {
        var now = _timeProvider.GetUtcNow();
        foreach (var operation in _operations)
        {
            if (operation.Value.ExpiresAt <= now)
            {
                _operations.TryRemove(operation.Key, out _);
            }
        }
    }
}
