using System.Collections.Concurrent;
using SheetsMcp.Errors;
using SheetsMcp.Models;

namespace SheetsMcp.Preview;

public sealed class InMemoryBatchPreviewStore(TimeProvider? timeProvider = null) : IBatchPreviewStore
{
    private static readonly TimeSpan OperationTtl = TimeSpan.FromMinutes(15);
    private readonly ConcurrentDictionary<string, BatchPreviewOperation> _operations = new();
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public BatchPreviewOperation Create(string spreadsheetId, IReadOnlyList<BatchValueUpdateInput> updates)
    {
        PruneExpired();
        var operationId = $"batch_{Guid.NewGuid():N}";
        var operation = new BatchPreviewOperation(
            operationId,
            spreadsheetId,
            _timeProvider.GetUtcNow().Add(OperationTtl),
            updates);
        _operations[operationId] = operation;
        return operation;
    }

    public BatchPreviewOperation Consume(string operationId)
    {
        if (string.IsNullOrWhiteSpace(operationId))
        {
            throw ToolError.InvalidInput("An operationId is required.");
        }

        if (!_operations.TryRemove(operationId.Trim(), out var operation))
        {
            throw ToolError.InvalidInput("The batch update operation ID is unknown or already used.");
        }

        if (operation.ExpiresAt <= _timeProvider.GetUtcNow())
        {
            throw ToolError.InvalidInput("The batch update operation ID has expired.");
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
