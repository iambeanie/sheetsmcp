using ModelContextProtocol;
using SheetsMcp.Models;
using SheetsMcp.Preview;

namespace SheetsMcp.Tests;

public sealed class PreviewStoreTests
{
    [Fact]
    public void Consume_returns_operation_once()
    {
        var store = new InMemoryBatchPreviewStore();
        var operation = store.Create("spreadsheet", [new BatchValueUpdateInput("Sheet1!A1:A1", [["value"]])]);

        Assert.Equal(operation.OperationId, store.Consume(operation.OperationId).OperationId);
        Assert.Throws<McpException>(() => store.Consume(operation.OperationId));
    }

    [Fact]
    public void Consume_rejects_expired_operations()
    {
        var time = new MutableTimeProvider(DateTimeOffset.Parse("2026-04-26T00:00:00Z"));
        var store = new InMemoryBatchPreviewStore(time);
        var operation = store.Create("spreadsheet", [new BatchValueUpdateInput("Sheet1!A1:A1", [["value"]])]);

        time.UtcNow = time.UtcNow.AddMinutes(16);

        Assert.Throws<McpException>(() => store.Consume(operation.OperationId));
    }

    private sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;

        public override DateTimeOffset GetUtcNow() => UtcNow;
    }
}
