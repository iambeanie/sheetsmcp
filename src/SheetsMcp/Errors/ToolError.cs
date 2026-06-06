using ModelContextProtocol;

namespace SheetsMcp.Errors;

public static class ToolError
{
    public static McpException InvalidInput(string message) => new(message);

    public static McpException OperationFailed(string message) => new(message);
}
