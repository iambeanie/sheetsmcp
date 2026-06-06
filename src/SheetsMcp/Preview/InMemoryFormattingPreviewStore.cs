using System.Collections.Concurrent;
using SheetsMcp.Errors;
using SheetsMcp.Models;

namespace SheetsMcp.Preview;

public sealed class InMemoryFormattingPreviewStore(TimeProvider? timeProvider = null) : IFormattingPreviewStore
{
    private static readonly TimeSpan OperationTtl = TimeSpan.FromMinutes(15);
    private readonly ConcurrentDictionary<string, FormattingPreviewOperation> _operations = new();
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public FormattingPreviewOperation Create(string spreadsheetId, IReadOnlyList<FormattingUpdateInput> updates, IReadOnlyList<string> fields)
    {
        PruneExpired();
        var operationId = $"format_{Guid.NewGuid():N}";
        var operation = new FormattingPreviewOperation(
            operationId,
            spreadsheetId,
            _timeProvider.GetUtcNow().Add(OperationTtl),
            updates,
            fields);
        _operations[operationId] = operation;
        return operation;
    }

    public FormattingPreviewOperation Consume(string operationId)
    {
        if (string.IsNullOrWhiteSpace(operationId))
        {
            throw ToolError.InvalidInput("An operationId is required.");
        }

        if (!_operations.TryRemove(operationId.Trim(), out var operation))
        {
            throw ToolError.InvalidInput("The formatting operation ID is unknown or already used.");
        }

        if (operation.ExpiresAt <= _timeProvider.GetUtcNow())
        {
            throw ToolError.InvalidInput("The formatting operation ID has expired.");
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
